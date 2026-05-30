# Sprint 1 — PWA + Gamification Frontend Handoff

This document is the implementation guide for the `lang-art-mvp` repo. The
backend pieces are already in place and live at the URLs documented below;
**nothing in this doc requires further backend work**. The React frontend is a
separate repository — do not modify it from `lang-art-back`.

---

## What's in the backend already

All endpoints return the standard envelope: `{ success, data, message? }`.
Requests are camelCase, responses are snake_case.

### Gamification — `/api/gamification`

| Method | Path | Auth | Returns |
|---|---|---|---|
| GET | `/api/gamification/me` | any signed-in user | `GamificationProfile` |
| GET | `/api/gamification/badges` | any signed-in user | `BadgeDto[]` (full catalog, with `earned` flag) |
| GET | `/api/gamification/ledger/me?limit=20` | any signed-in user | `LedgerEntry[]` (most recent first, max 100) |
| GET | `/api/gamification/leaderboard/{groupId}` | admin/teacher always; student must be member | `Leaderboard` |

**`GamificationProfile` shape:**

```ts
{
  user_id: string;
  total_xp: number;
  level: number;            // 0 at start; LevelCalculator curve
  xp_in_level: number;      // 0..xp_for_next_level
  xp_for_next_level: number;
  current_streak: number;
  longest_streak: number;
  last_activity_date_utc: string | null;   // "YYYY-MM-DD"
  earned_badges: BadgeDto[];
  recent_ledger: LedgerEntry[];            // last 5
}

// BadgeDto
{
  id: string;
  code: string;             // stable identifier for criteria / artwork lookup
  name: string;
  description: string;
  icon_url: string | null;
  xp_reward: number;
  earned: boolean;
  earned_at_utc: string | null;
}

// LedgerEntry
{
  id: string;
  amount: number;           // signed
  reason: "lesson_completed" | "quiz_passed" | "quiz_perfect_bonus"
        | "daily_login" | "streak_bonus" | "badge_reward" | "admin_adjustment";
  source_id: string | null;
  created_at_utc: string;
}

// Leaderboard
{
  group_id: string;
  rows: {
    rank: number;
    user_id: string;
    full_name: string;
    avatar_url: string | null;
    total_xp: number;
    level: number;
    is_current_user: boolean;
  }[];
  current_user_rank: number | null;
}
```

### Push notifications — `/api/notifications/push`

| Method | Path | Body |
|---|---|---|
| POST | `/api/notifications/push/subscribe` | `{ endpoint, keys: { p256dh, auth }, userAgent? }` — upserts by endpoint |
| DELETE | `/api/notifications/push/subscribe` | `{ endpoint }` |

The backend fires a push for every newly-earned badge automatically — the
payload is `{ title, body, tag, url }`.

---

## Install `vite-plugin-pwa`

```bash
npm i -D vite-plugin-pwa
```

`vite.config.ts`:

```ts
import { VitePWA } from "vite-plugin-pwa";

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: "autoUpdate",
      strategies: "generateSW",
      includeAssets: ["favicon.ico", "apple-touch-icon.png", "icons/*.png"],
      manifest: {
        name: "LangArt LMS",
        short_name: "LangArt",
        description: "Self-hosted language-school LMS",
        theme_color: "#D4AF37",
        background_color: "#203d60",
        display: "standalone",
        start_url: "/dashboard",
        icons: [
          { src: "/icons/icon-192.png",     sizes: "192x192", type: "image/png" },
          { src: "/icons/icon-512.png",     sizes: "512x512", type: "image/png" },
          { src: "/icons/maskable-512.png", sizes: "512x512", type: "image/png", purpose: "maskable" },
        ],
      },
      workbox: {
        globPatterns: ["**/*.{js,css,html,ico,png,svg,woff2}"],
        runtimeCaching: [
          {
            // Curriculum reads — stale while revalidate is fine, lessons change rarely.
            urlPattern: ({ url }) =>
              url.pathname.startsWith("/api/courses") ||
              url.pathname.startsWith("/api/lessons"),
            handler: "StaleWhileRevalidate",
            options: {
              cacheName: "api-curriculum",
              expiration: { maxAgeSeconds: 60 * 60 * 24, maxEntries: 200 },
              cacheableResponse: { statuses: [0, 200] },
            },
          },
          {
            // Lesson media (audio + video + PDFs). CacheFirst because the same file
            // hash never changes — re-upload produces a new URL.
            urlPattern: ({ url }) => url.pathname.startsWith("/uploads/"),
            handler: "CacheFirst",
            options: {
              cacheName: "uploads-media",
              expiration: { maxAgeSeconds: 60 * 60 * 24 * 30, maxEntries: 50 },
              cacheableResponse: { statuses: [0, 200] },
              rangeRequests: true,
            },
          },
        ],
      },
    }),
  ],
});
```

### iOS standalone meta tags

Add to `index.html`:

```html
<link rel="apple-touch-icon" href="/icons/apple-touch-icon.png" />
<meta name="apple-mobile-web-app-capable" content="yes" />
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
<meta name="apple-mobile-web-app-title" content="LangArt" />
<meta name="theme-color" content="#D4AF37" />
```

### Icons checklist

Request from designer (placeholder paths above):

- `icons/icon-192.png` (192×192, square)
- `icons/icon-512.png` (512×512, square)
- `icons/maskable-512.png` (512×512, **safe zone padded** — Android adaptive icons)
- `icons/apple-touch-icon.png` (180×180, no transparency)

---

## Push subscription flow

```ts
// src/lib/push.ts
const VAPID_PUBLIC = import.meta.env.VITE_VAPID_PUBLIC_KEY as string;

export async function ensurePushSubscription(): Promise<void> {
  if (!("serviceWorker" in navigator) || !("PushManager" in window)) return;
  if (!VAPID_PUBLIC) {
    console.warn("VITE_VAPID_PUBLIC_KEY not set — push disabled.");
    return;
  }

  const permission = await Notification.requestPermission();
  if (permission !== "granted") return;

  const reg = await navigator.serviceWorker.ready;
  const existing = await reg.pushManager.getSubscription();
  const sub = existing ?? await reg.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: urlBase64ToUint8Array(VAPID_PUBLIC),
  });

  const json = sub.toJSON();
  await api.post("/notifications/push/subscribe", {
    endpoint: json.endpoint!,
    keys: { p256dh: json.keys!.p256dh, auth: json.keys!.auth },
    userAgent: navigator.userAgent,
  });
}

function urlBase64ToUint8Array(base64: string): Uint8Array {
  const padding = "=".repeat((4 - (base64.length % 4)) % 4);
  const decoded = atob((base64 + padding).replace(/-/g, "+").replace(/_/g, "/"));
  return Uint8Array.from(decoded, c => c.charCodeAt(0));
}
```

Call `ensurePushSubscription()` once after login (e.g. in `AuthProvider`'s
successful-login effect).

### Service worker handler

Add a `src/sw.ts` (or extend whatever `vite-plugin-pwa` generates with
`injectManifest` strategy) so push payloads from the backend render correctly:

```ts
self.addEventListener("push", (event: PushEvent) => {
  const data = event.data?.json() ?? { title: "LangArt" };
  event.waitUntil(
    self.registration.showNotification(data.title, {
      body: data.body,
      tag: data.tag,
      icon: "/icons/icon-192.png",
      badge: "/icons/icon-192.png",
      data: { url: data.url },
    }),
  );
});

self.addEventListener("notificationclick", (event: NotificationEvent) => {
  event.notification.close();
  const url = (event.notification.data as { url?: string })?.url ?? "/dashboard";
  event.waitUntil(
    self.clients.matchAll({ type: "window", includeUncontrolled: true }).then(list => {
      const existing = list.find(c => c.url.endsWith(url));
      if (existing) return existing.focus();
      return self.clients.openWindow(url);
    }),
  );
});
```

### VAPID env

```env
VITE_VAPID_PUBLIC_KEY=<paste public key here>
```

Generate the keypair with `npx web-push generate-vapid-keys`. Public goes to
the frontend env, private to `Notifications:Push:VapidPrivateKey` on the
backend (see `appsettings.Development.json` placeholder).

---

## React component sketches

All Tailwind class hints assume the existing palette (`#203d60`, `#D4AF37`, `#FAFAFA`).

### `<XpBar level xpInLevel xpForNextLevel />`

```tsx
interface XpBarProps {
  level: number;
  xpInLevel: number;
  xpForNextLevel: number;
}

export function XpBar({ level, xpInLevel, xpForNextLevel }: XpBarProps) {
  const pct = xpForNextLevel === 0 ? 0 : Math.min(100, (xpInLevel / xpForNextLevel) * 100);
  return (
    <div className="flex items-center gap-3">
      <span className="text-xs font-bold text-[#203d60]">Lv {level}</span>
      <div className="flex-1 h-2 bg-[#E5E5E5] rounded-full overflow-hidden">
        <div
          className="h-full bg-gradient-to-r from-[#D4AF37] to-[#F4D06F] transition-all duration-500"
          style={{ width: `${pct}%` }}
        />
      </div>
      <span className="text-[10px] text-[#203d60]/60">
        {xpInLevel}/{xpForNextLevel} XP
      </span>
    </div>
  );
}
```

### `<StreakIndicator days isAtRisk />`

```tsx
interface StreakIndicatorProps {
  days: number;
  isAtRisk?: boolean;     // true when last_activity_date_utc < today AND streak > 0
}

export function StreakIndicator({ days, isAtRisk }: StreakIndicatorProps) {
  if (days === 0) return null;
  return (
    <div
      className={`flex items-center gap-1.5 px-2 py-1 rounded-full text-xs font-medium ${
        isAtRisk ? "bg-amber-50 text-amber-700" : "bg-orange-50 text-orange-700"
      }`}
      title={isAtRisk ? "Your streak resets at midnight UTC — finish a lesson today!" : `${days}-day streak`}
    >
      <Flame size={14} className={isAtRisk ? "" : "animate-pulse"} />
      <span>{days}</span>
    </div>
  );
}
```

### `<BadgeGallery badges />`

```tsx
import type { BadgeDto } from "@/services/gamificationService";

export function BadgeGallery({ badges }: { badges: BadgeDto[] }) {
  return (
    <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-5 gap-4">
      {badges.map(b => (
        <div
          key={b.id}
          className={`p-4 border rounded-lg text-center transition-all ${
            b.earned
              ? "border-[#D4AF37] bg-[#FFFCF2]"
              : "border-[#E5E5E5] bg-gray-50 grayscale opacity-60 hover:opacity-90"
          }`}
          title={b.earned ? `Earned ${b.earned_at_utc}` : "Locked — keep going!"}
        >
          {b.icon_url ? (
            <img src={b.icon_url} alt={b.name} className="w-12 h-12 mx-auto mb-2" />
          ) : (
            <Award size={36} className="mx-auto mb-2 text-[#D4AF37]" />
          )}
          <h4 className="text-sm font-medium text-[#203d60]">{b.name}</h4>
          <p className="text-[10px] text-[#203d60]/60 mt-1">{b.description}</p>
          <span className="text-[10px] text-[#D4AF37] mt-1 block">+{b.xp_reward} XP</span>
        </div>
      ))}
    </div>
  );
}
```

### `<Leaderboard rows currentUserId />`

```tsx
import type { LeaderboardRowDto } from "@/services/gamificationService";

export function Leaderboard({ rows, currentUserId }: {
  rows: LeaderboardRowDto[];
  currentUserId: string;
}) {
  return (
    <ul className="divide-y divide-[#E5E5E5]">
      {rows.map(r => (
        <li
          key={r.user_id}
          className={`flex items-center gap-3 px-3 py-2 ${
            r.user_id === currentUserId ? "bg-[#FFFCF2] font-medium" : ""
          }`}
        >
          <span className="text-xs text-[#203d60]/40 w-6">#{r.rank}</span>
          <Avatar src={r.avatar_url} fallback={r.full_name} size={28} />
          <span className="flex-1 truncate text-sm text-[#203d60]">{r.full_name}</span>
          <span className="text-xs text-[#203d60]/60">Lv {r.level}</span>
          <span className="text-sm font-medium text-[#D4AF37]">{r.total_xp.toLocaleString()} XP</span>
        </li>
      ))}
    </ul>
  );
}
```

---

## TanStack Query hooks

```ts
// src/services/gamificationService.ts
export function useGamificationProfile() {
  return useQuery({
    queryKey: ["gamification", "me"],
    queryFn: () => api.get<GamificationProfile>("/gamification/me"),
    staleTime: 30_000,
  });
}

export function useLeaderboard(groupId: string) {
  return useQuery({
    queryKey: ["gamification", "leaderboard", groupId],
    queryFn: () => api.get<Leaderboard>(`/gamification/leaderboard/${groupId}`),
    enabled: !!groupId,
    staleTime: 60_000,
  });
}

export function useBadges() {
  return useQuery({
    queryKey: ["gamification", "badges"],
    queryFn: () => api.get<BadgeDto[]>("/gamification/badges"),
    staleTime: 60 * 60 * 1000,         // catalog rarely changes
  });
}

/**
 * Polls /ledger/me every 30 s, diffs against the last seen `id`, and
 * fires a toast for each new badge_reward event. When push is wired up
 * this can be downgraded to a 5-min sanity poll.
 */
export function useEarnedBadgesToast() {
  const { addToast } = useToastStore();
  const { data: badges } = useBadges();
  const seenIds = useRef<Set<string>>(new Set());

  useQuery({
    queryKey: ["gamification", "ledger", "watch"],
    queryFn: () => api.get<LedgerEntry[]>("/gamification/ledger/me?limit=20"),
    refetchInterval: 30_000,
    select: (entries) => {
      for (const e of entries) {
        if (e.reason === "badge_reward" && e.source_id && !seenIds.current.has(e.id)) {
          seenIds.current.add(e.id);
          const badge = badges?.find(b => b.id === e.source_id);
          if (badge) {
            addToast(`🏆 You earned "${badge.name}"!`, "success");
          }
        }
      }
      return entries;
    },
  });
}
```

### Where to mount

- `<XpBar>` + `<StreakIndicator>` in the dashboard sidebar / header — visible everywhere
- `<BadgeGallery>` on a dedicated `/profile/badges` page
- `<Leaderboard>` on `/dashboard` (default group) AND on every teacher's group page
- `useEarnedBadgesToast()` in `App.tsx` (single mount) so toasts work site-wide

---

## Testing notes

| Scenario | How |
|---|---|
| First login → see 15 XP (10 daily_login + 5 streak_bonus) | log in any seeded student |
| Complete a lesson → +20 XP + `first_lesson` badge toast | mark any lesson complete |
| Perfect quiz → +30 + +20 | submit a quiz with `score == totalQuestions` |
| Streak resets if you skip a day | bg service runs hourly; for manual test, edit `user_streaks.last_activity_date_utc` to `<yesterday>` and wait or force-tick |
| Push works | run `npx web-push generate-vapid-keys`, paste keys into frontend env + backend appsettings, trigger a badge earn, watch DevTools → Application → Service Workers → Push |

---

## Open questions for the frontend implementer

1. Where does the `<XpBar>` live exactly — sidebar bottom (alongside avatar) or top header bar? I'd vote sidebar bottom so it's always visible without competing with page nav.
2. Should locked badges show a hint about *how* to unlock them, or stay mysterious? My take: show the description always (current `BadgeDto.description` covers it) but never show progress numbers (would make the streak badges noisy on every tick).
3. Group leaderboard scope: a student can be in multiple groups. UI should let them pick which one — dropdown above the leaderboard? Or default to "first group joined"?
