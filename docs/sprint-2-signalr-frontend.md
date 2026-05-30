# Sprint 2 — SignalR Real-time Frontend Handoff

This document is the implementation guide for the `lang-art-mvp` repo. The
backend pieces are in place and live at the URLs below; **nothing in this
doc requires further backend work**. The React frontend is a separate
repository — do not modify it from `lang-art-back`.

---

## What's in the backend already

### Hubs

| URL | Hub class | Purpose |
|---|---|---|
| `wss://…/hubs/notifications` | `NotificationsHub` | Per-user bell notifications + badge-earned events + classroom join/leave |
| `wss://…/hubs/presence` | `PresenceHub` | Online/offline tracking (Sprint 3 will wire UserOnline/UserOffline broadcasts) |
| `wss://…/hubs/live-lesson` | `LiveLessonHub` | Existing live-lesson hub (Sprint 1) — unchanged |

### REST

| Method | Path | Returns |
|---|---|---|
| `GET` | `/api/realtime/presence/classroom/{groupId}` | `{ group_id, online_user_ids: string[] }` |
| `GET` | `/api/notifications` (unchanged) | Polling endpoint — kept working as fallback |
| `GET` | `/api/notifications/unread-count` (unchanged) | Badge count |
| `POST` | `/api/notifications/read-all` (unchanged) | Mark all read |
| `POST` | `/api/notifications/{id}/read` (unchanged) | Mark one read |

### Hub events the client subscribes to

| Event name | Payload | Trigger |
|---|---|---|
| `NotificationReceived` | `NotificationDto` (below) | Any `NotificationsService.NotifyAsync` call (attendance marked, lesson unlocked, course assigned) |
| `BadgeEarned` | `BadgeEarnedDto` (below) | A badge becomes earned during `GamificationService.EvaluateBadgesAsync` |
| `ClassroomEvent` | `ClassroomEventDto` | A classroom join/leave or, in Sprint 3, a "block_advanced"/"hand_raised" event |

### Hub methods the client can call

| Method | Hub | Notes |
|---|---|---|
| `JoinClassroomAsync(groupId)` → `Task<bool>` | `NotificationsHub` | Returns `false` if the user isn't allowed in the group |
| `LeaveClassroomAsync(groupId)` → `Task` | `NotificationsHub` | Idempotent |
| `HeartbeatAsync()` → `Task` | `PresenceHub` | No-op today; reserved for future last-seen persistence |

### Payload shapes (snake_case on the wire — verified in tests)

```ts
// NotificationDto
interface NotificationDto {
  id: string;
  user_id: string;
  type: string;                // "lesson_unlocked" | "attendance_marked" | "course_assigned" | ...
  title: string;
  body: string | null;
  link_url: string | null;
  created_at_utc: string;      // ISO 8601 UTC
  metadata: Record<string, unknown> | null;
}

// BadgeEarnedDto
interface BadgeEarnedDto {
  badge: {
    id: string;
    code: string;
    name: string;
    description: string;
    icon_url: string | null;
    xp_reward: number;
  };
  earned_at_utc: string;
}

// ClassroomEventDto (Sprint 3 surface area; today only "user_joined" appears)
interface ClassroomEventDto {
  type: string;
  actor_user_id: string | null;
  at_utc: string;
  data: Record<string, unknown> | null;
}
```

---

## Install

```bash
npm i @microsoft/signalr
```

---

## Connection bootstrap

```ts
// src/lib/realtime.ts
import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { useAuthStore } from "@/store/useAuthStore";
import { refreshAccessToken } from "@/lib/apiClient";

const API = import.meta.env.VITE_API_BASE_URL ?? "/api";
const BASE = API.replace(/\/api\/?$/, "");

let connection: ReturnType<typeof HubConnectionBuilder.prototype.build> | null = null;

export function getNotificationsConnection() {
  if (connection) return connection;

  connection = new HubConnectionBuilder()
    .withUrl(`${BASE}/hubs/notifications`, {
      // IMPORTANT: factory is called on EVERY reconnect, so it must read the
      // current token from the store, not capture it once at build time.
      accessTokenFactory: () => useAuthStore.getState().getAccessToken() ?? "",
    })
    .withAutomaticReconnect([0, 2_000, 10_000, 30_000])
    .configureLogging(import.meta.env.DEV ? LogLevel.Information : LogLevel.Warning)
    .build();

  // If the server closed us with a 401, try a one-shot token refresh + restart.
  connection.onclose(async (err) => {
    if (err && /401|Unauthorized/.test(String(err))) {
      const refreshed = await refreshAccessToken();
      if (refreshed && connection) {
        await connection.start().catch(() => { /* surface via state to UI */ });
      }
    }
  });

  return connection;
}

export async function stopRealtime() {
  if (!connection) return;
  await connection.stop();
  connection = null;
}
```

### Lifecycle

- Lazy-build the connection on first authenticated layout mount.
- `await connection.start()` once; SignalR's reconnect policy handles drops.
- Call `stopRealtime()` from the logout action so a re-login gets a fresh
  connection with the new token.
- Mount ONCE at the auth-protected layout root (`<RequireAuth>` / dashboard
  layout). Subscribing the same handler multiple times = duplicate UI events.

---

## React hooks

```ts
// src/services/realtimeHooks.ts
import { useEffect, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { getNotificationsConnection } from "@/lib/realtime";
import type { HubConnectionState } from "@microsoft/signalr";
import { useToastStore } from "@/store/useToastStore";

export function useSignalRConnection(): HubConnectionState {
  const [state, setState] = useState<HubConnectionState>("Disconnected" as HubConnectionState);

  useEffect(() => {
    const conn = getNotificationsConnection();
    const sync = () => setState(conn.state);

    conn.onreconnecting(sync);
    conn.onreconnected(sync);
    conn.onclose(sync);

    conn.start()
      .then(sync)
      .catch(err => {
        console.error("hub start failed", err);
        sync();
      });

    return () => { /* leave connection alive across pages — only stopped on logout */ };
  }, []);

  return state;
}

export function useNotificationsStream() {
  const qc = useQueryClient();
  const { addToast } = useToastStore();

  useEffect(() => {
    const conn = getNotificationsConnection();

    const onNotification = (p: NotificationDto) => {
      // Invalidate so the bell + list re-fetch from the server (cheap & deterministic).
      qc.invalidateQueries({ queryKey: ["notifications"] });
      qc.invalidateQueries({ queryKey: ["notifications", "unread-count"] });
      // Optional immediate toast for high-signal types:
      if (["course_assigned", "lesson_unlocked"].includes(p.type)) {
        addToast(p.title, "info");
      }
    };

    const onBadge = (p: BadgeEarnedDto) => {
      qc.invalidateQueries({ queryKey: ["gamification", "me"] });
      qc.invalidateQueries({ queryKey: ["gamification", "badges"] });
      addToast(`🏆 You earned "${p.badge.name}"!`, "success");
    };

    conn.on("NotificationReceived", onNotification);
    conn.on("BadgeEarned", onBadge);

    return () => {
      conn.off("NotificationReceived", onNotification);
      conn.off("BadgeEarned", onBadge);
    };
  }, [qc, addToast]);
}

export function useClassroomPresence(groupId: string) {
  const [online, setOnline] = useState<string[]>([]);

  useEffect(() => {
    if (!groupId) return;

    // Cold start: REST snapshot.
    api.get<{ online_user_ids: string[] }>(`/realtime/presence/classroom/${groupId}`)
      .then(d => setOnline(d.online_user_ids))
      .catch(() => { /* presence is best-effort; ignore */ });

    // Live updates from the presence hub. Backend broadcasts UserOnline/UserOffline
    // to subscribers; in Sprint 2 the broadcasts aren't wired yet — Sprint 3 will
    // populate this hook fully.
    const conn = getNotificationsConnection();
    const onJoined = (e: ClassroomEventDto) => {
      if (e.type === "user_joined" && e.actor_user_id) {
        setOnline(prev => prev.includes(e.actor_user_id!) ? prev : [...prev, e.actor_user_id!]);
      } else if (e.type === "user_left" && e.actor_user_id) {
        setOnline(prev => prev.filter(u => u !== e.actor_user_id));
      }
    };
    conn.on("ClassroomEvent", onJoined);

    // Tell the hub we want classroom events for THIS group:
    conn.invoke<boolean>("JoinClassroomAsync", groupId).catch(() => { /* not allowed */ });

    return () => {
      conn.off("ClassroomEvent", onJoined);
      conn.invoke("LeaveClassroomAsync", groupId).catch(() => {});
    };
  }, [groupId]);

  return online;
}
```

### Where to mount

- `useSignalRConnection()` — one call in `<DashboardLayout>` so a status
  indicator can render in the corner ("Disconnected — refresh to retry").
- `useNotificationsStream()` — one call in `<DashboardLayout>`. Subscribes
  once for the whole authenticated session.
- `useClassroomPresence(groupId)` — call inside any page that shows a
  classroom roster (teacher group page).

### Token refresh interplay

The `accessTokenFactory` is called on every reconnect attempt. If your
Zustand auth store rotates the token after a `/auth/refresh`, the next
SignalR reconnect picks it up automatically — no manual wiring needed.

The explicit `onclose` 401 handler in the bootstrap is a fast-path: if
the server boots us specifically because the token expired, we refresh
once and restart immediately rather than waiting for the next reconnect
delay.

---

## UI integration checklist

- [ ] Add a `<ConnectionStatusBadge>` to `DashboardLayout` that reads
      `useSignalRConnection()` and shows nothing when connected, a yellow
      dot when reconnecting, and a red "Disconnected — refresh" banner
      when terminally closed.
- [ ] In the existing notification bell component, **keep the polling**
      `useQuery({ refetchInterval: 60_000 })` for cold-start and as a
      fallback when the hub is disconnected. Hub events just trigger an
      earlier `invalidateQueries`.
- [ ] On the teacher group page (`/teacher/group/:groupId`), use
      `useClassroomPresence(groupId)` to mark online avatars with a green
      dot in the existing roster.
- [ ] Optional: in `<BadgeGallery>`, when `useNotificationsStream` fires a
      `BadgeEarned` for badge `code`, flash that tile briefly to highlight
      the change before the React Query refetch settles.

---

## Network verification (DevTools)

When the frontend ships, opening DevTools → Network → filter "WS" and
clicking the open `/hubs/notifications` connection should show frames like:

```
{"type":1,"target":"NotificationReceived","arguments":[{"id":"…","user_id":"…","type":"lesson_unlocked","title":"…","created_at_utc":"…"}]}
```

Verify property names are **snake_case** (`user_id`, `created_at_utc`) — not
camelCase. If they're camelCase, the SignalR JSON protocol naming policy on
the server isn't being honoured and someone needs to look at
`Program.cs`'s `AddJsonProtocol` config.

---

## What this sprint does NOT yet do (intentional Sprint 3 work)

- The `PresenceHub.UserOnline` / `UserOffline` broadcasts are reserved —
  the hub is mapped and tracks connections, but it doesn't push events to
  observers yet. Sprint 3 will decide the broadcast scope (per-teacher's-students?
  per-classroom?) and wire it.
- Live classroom UI (hand-raise, screen share, breakout rooms) — Sprint 3.
- Kahoot-style live quiz — Sprint 3.
- Redis backplane for horizontal scale-out — only when load demands.
