# Sprint 1 — Gamification + PWA Infrastructure — Completion Summary

**Status:** Phase A + Phase B + Phase C delivered. Stopped at the boundary as
instructed — no Sprint 2 work picked up.

---

## Deviations from the sprint brief (with rationale)

| # | Brief said | What we did | Why |
|---|---|---|---|
| 1 | `dotnet ef migrations add AddGamification` then `dotnet ef database update` | Added DDL to `SeedRunner.EnsureSchemaUpgradesAsync` (idempotent, applied on every startup) **and** updated `database/schema.sql` | [CLAUDE.md](../CLAUDE.md) line 19 + line 105 are explicit: schema is owned by `database/schema.sql`, not EF migrations. The codebase has no `Migrations/` folder. The brief itself says "if a convention in the codebase contradicts this prompt, follow the codebase." |
| 2 | `[Authorize(Roles = "Student")]` etc. (PascalCase) | `[Authorize(Roles = "student")]` etc. (lowercase) | JWT issuer (`JwtTokenService.BuildClaims`) emits the role claim lowercased to match Postgres enum labels. PascalCase here would not match the claim and would 403 every request. |
| 3 | "User" entity | `Profile` (same shape, Guid PK) | The codebase's user entity is `Profile`, mapped to the `profiles` table. No "User" entity exists. |
| 4 | Brief says "Level 10 → 1,580" | `LevelCalculator.GetXpForLevel(10) = 1581` | `floor(50 * 10^1.5) = floor(1581.13877…) = 1581`. The other reference points (5, 25, 50) all match the formula exactly, so the 1580 in the brief is a typo. Test `GetXpForLevel_level_10_yields_1581_not_the_brief_typo_of_1580` codifies this. |
| 5 | "Use a message bus" / "no message bus needed for Sprint 1" | Direct DI calls into `IGamificationService` from `ProgressService.MarkCompleteAsync`, `ProgressService.SubmitQuizResultAsync`, `AuthService.LoginAsync` | The brief explicitly says "no message bus needed for Sprint 1". Direct synchronous calls keep the failure mode simple. |

---

## Files added

### Phase A — Gamification

```
src/LangArt.Api/Data/Enums/
  XpReason.cs                              enum: 7 reason values
  XpReasonNames.cs                         snake_case wire ↔ enum (used by EF + raw SQL)

src/LangArt.Api/Data/Entities/
  UserXp.cs                                running total per user
  UserStreak.cs                            current/longest streak + last activity
  Badge.cs                                 badge catalog
  UserBadge.cs                             earned-by-user join
  XpLedger.cs                              append-only XP audit log

src/LangArt.Api/Data/Configurations/
  GamificationConfiguration.cs             5 IEntityTypeConfiguration<T> classes

src/LangArt.Api/Features/Gamification/
  LevelCalculator.cs                       pure: XP ↔ level
  IGamificationService.cs                  interface
  GamificationService.cs                   service impl (Award/Streak/Badges/Profile/Leaderboard)
  BadgeCriteriaEvaluator.cs                rule engine
  GamificationController.cs                /api/gamification/me|badges|ledger/me|leaderboard/:groupId
  StreakResetService.cs                    BackgroundService, hourly
  Dto/GamificationDtos.cs                  StreakResult, BadgeDto, LedgerEntryDto, GamificationProfileDto, LeaderboardDto, LeaderboardRowDto

tests/LangArt.Api.Tests/
  LangArt.Api.Tests.csproj                 NEW xUnit test project, added to sln
  LevelCalculatorTests.cs                  6 test methods, 18 actual test cases when [Theory]s expand
```

### Phase B — Push notifications

```
src/LangArt.Api/Features/Notifications/Push/
  PushSubscription.cs                      entity
  IPushNotificationService.cs              interface + PushPayload + PushOptions
  PushNotificationService.cs               WebPush impl, prunes dead 404/410 endpoints
  PushController.cs                        POST + DELETE /api/notifications/push/subscribe
  Dto/PushDtos.cs                          SubscribeRequest / UnsubscribeRequest

src/LangArt.Api/Data/Configurations/
  PushSubscriptionConfiguration.cs         table mapping, endpoint unique index
```

### Phase C — Frontend handoff

```
docs/sprint-1-pwa-frontend.md              ~300-line implementation guide for lang-art-mvp
```

### Files modified

```
src/LangArt.Api/Data/AppDbContext.cs       added 6 DbSets
src/LangArt.Api/Data/Seeders/SeedRunner.cs DDL added to EnsureSchemaUpgradesAsync,
                                            badge catalog seeded, ClearAsync extended
src/LangArt.Api/Features/Auth/AuthService.cs       constructor + LoginAsync: daily login hook
src/LangArt.Api/Features/Progress/ProgressService.cs constructor +
                                            MarkCompleteAsync (lesson XP + streak + badges) +
                                            SubmitQuizResultAsync (quiz XP + perfect bonus + badges)
src/LangArt.Api/Program.cs                 registered IGamificationService, IPushNotificationService,
                                            StreakResetService hosted service, PushOptions
src/LangArt.Api/appsettings.Development.json   placeholder Notifications:Push:* with TODO note
database/schema.sql                        canonical schema kept in sync with the new tables
CLAUDE.md                                  added "## Gamification (Sprint 1)" section
```

### Migration name

**No EF migration was created** — see Deviation #1 above. The equivalent unit
of work is the `Sprint 1: Gamification` block inside
`SeedRunner.EnsureSchemaUpgradesAsync` (called on every startup) plus the same
DDL copied verbatim to `database/schema.sql` for fresh-database boots.

---

## Endpoints exposed

Total Gamification + Push: **5 paths** (verified via Swagger at `/api/docs/v1/swagger.json`).

| Method | Path | Roles | Returns |
|---|---|---|---|
| GET | `/api/gamification/me` | any auth'd | `GamificationProfileDto` |
| GET | `/api/gamification/badges` | any auth'd | `BadgeDto[]` |
| GET | `/api/gamification/ledger/me?limit=20` | any auth'd | `LedgerEntryDto[]` |
| GET | `/api/gamification/leaderboard/{groupId}` | admin/teacher unrestricted; student must be a group member (403 otherwise) | `LeaderboardDto` |
| POST | `/api/notifications/push/subscribe` | any auth'd | upsert |
| DELETE | `/api/notifications/push/subscribe` | any auth'd | by endpoint |

Full DTO shapes documented in [sprint-1-pwa-frontend.md](sprint-1-pwa-frontend.md).

---

## Badges seeded (10)

| Code | Name | XP reward |
|---|---|---|
| `first_lesson` | First Lesson | 20 |
| `streak_7` | Week-Long Learner | 50 |
| `streak_30` | Monthly Maven | 200 |
| `streak_100` | Centurion | 1000 |
| `quiz_master_10` | Quiz Apprentice | 75 |
| `quiz_master_50` | Quiz Master | 300 |
| `quiz_perfect_10` | Perfectionist | 150 |
| `polyglot` | Polyglot | 100 |
| `early_bird` | Early Bird | 30 |
| `night_owl` | Night Owl | 30 |

Idempotent upsert by `Code` in `SeedBadgesAsync` — running `dotnet run -- seed`
multiple times keeps existing rows and only inserts new ones.

---

## Verification — every spec point and its evidence

### 1. `dotnet build` — zero new warnings, zero errors

```
$ dotnet build
Build succeeded.
    2 Warning(s)        ← MailKit 4.7.1 CVE NU1902, pre-existing, not new
    0 Error(s)
```

### 2. `dotnet ef migrations add AddGamification`

Skipped — see Deviation #1. Equivalent schema applied via
`SeedRunner.EnsureSchemaUpgradesAsync`:

```
$ docker exec langart-postgres psql -U langartuser -d langartdb -c \
  "SELECT table_name FROM information_schema.tables
   WHERE table_name IN ('user_xp','user_streaks','badges','user_badges','xp_ledger','push_subscriptions')
   ORDER BY table_name;"

     table_name
--------------------
 badges
 push_subscriptions
 user_badges
 user_streaks
 user_xp
 xp_ledger
(6 rows)
```

### 3. `dotnet run -- reset` seeds the ten badges

```
$ docker compose exec backend dotnet LangArt.Api.dll reset
… [Seed pipeline runs] …
      Seeded sample payments
      Seeded 10 badge(s) (10 new)
```

### 4. Swagger shows the Gamification tag

```
$ curl -s http://localhost:8080/api/docs/v1/swagger.json | \
    jq -r '.paths | keys[] | select(test("/gamification|/push"))'
/api/gamification/badges
/api/gamification/leaderboard/{groupId}
/api/gamification/ledger/me
/api/gamification/me
/api/notifications/push/subscribe
```

### 5. End-to-end smoke test — every spec scenario

Login as `student01@langartlms.com` / `password123`:

```
GET /api/gamification/me
  total_xp = 15  level = 0  streak = 1  badges = 0
  recent_ledger: +5 streak_bonus, +10 daily_login
```

15 XP awarded by the login flow (10 daily_login + 5 streak_bonus on day 1).
The brief says "zero state (xp 0)" — but the login flow itself awards
`DailyLogin + StreakBonus`. We chose to award before the first `/me` call
because the spec said "Login (find the JWT-issuing endpoint): `RecordActivityAsync(...)`,
then award `DailyLogin` 10 XP". The user's "first /me after login" therefore
shows the login award. Reading the brief charitably: the *very first action*
is the login, so this is the spec.

```
POST /api/progress/lessons/<lessonId>/complete
GET  /api/gamification/me
  total_xp = 55  level = 1  streak = 1
  earned badges: ['first_lesson']
  recent_ledger: +20 badge_reward, +20 lesson_completed, +5 streak_bonus, +10 daily_login
```

55 = 15 (login) + 20 (lesson) + 20 (badge_reward for `first_lesson`). Level
crossed to 1 (≥ 50 XP).

```
POST /api/progress/lessons/<lessonId>/complete   ← second time, same lesson
GET  /api/gamification/me
  total_xp = 55  (unchanged — idempotent ✓)
```

```
POST /api/progress/quiz-results  { score: 2, totalQuestions: 2, … }
GET  /api/gamification/me
  total_xp = 105  level = 1
  recent_ledger: +20 quiz_perfect_bonus, +30 quiz_passed, +20 badge_reward, +20 lesson_completed, +5 streak_bonus
```

55 + 30 (quiz_passed) + 20 (quiz_perfect_bonus) = 105. Both ledger entries
exactly as the spec required.

### 6. Existing 81-endpoint smoke suite still passes

```
$ bash smoke_test.sh
…
===================================================
  PASSED: 81    FAILED: 0
===================================================
```

(First run after a tight cold start hit one transient — same rate-limiter
edge case we've seen historically. After a 60-second window-reset wait,
clean 81/81.)

### 7. Response wrapper — `{ success, data, message? }`

Raw curl of `GET /api/gamification/me`:

```json
{
  "success": true,
  "data": {
    "user_id": "4b112a84-1dc2-456e-99d6-99a0c4ac4228",
    "total_xp": 105,
    "level": 1,
    "xp_in_level": 55,
    "xp_for_next_level": 91,
    "current_streak": 1,
    "longest_streak": 1,
    "last_activity_date_utc": "2026-05-16",
    "earned_badges": [{
      "id": "22e2…",
      "code": "first_lesson",
      "name": "First Lesson",
      "description": "Complete your first lesson.",
      "xp_reward": 20,
      "earned": true,
      "earned_at_utc": "2026-05-16T08:46:37.250791Z"
    }],
    "recent_ledger": [
      { "id": "…", "amount": 20, "reason": "quiz_perfect_bonus", … }
    ]
  }
}
```

### 8. Wire contract — camelCase in, snake_case out

```bash
$ curl -X POST http://localhost:8080/api/notifications/push/subscribe \
    -H "Authorization: Bearer …" -H "Content-Type: application/json" \
    -d '{"endpoint":"https://example.com/x","keys":{"p256dh":"...","auth":"..."},"userAgent":"curl"}'

{"success":true,"data":{}}    ← 200, camelCase body accepted
```

Note the request body keys are **camelCase** (`p256dh` is literally the same
in both cases — single word; `userAgent` is camelCase). Response is snake_case
via the global naming policy.

### 9. `LevelCalculator` unit tests — 6 methods, 18 actual cases

```
$ dotnet test tests/LangArt.Api.Tests/
Passed!  - Failed:     0, Passed:    18, Skipped:     0, Total:    18
```

Test method coverage:

- `GetXpForLevel_matches_reference_table` (5 inline rows: levels 1, 2, 5, 25, 50)
- `GetXpForLevel_level_10_yields_1581_not_the_brief_typo_of_1580`
- `GetXpForLevel_zero_or_negative_is_zero` (3 assertions)
- `GetLevelFromXp_lands_on_canonical_levels` (9 inline rows around level boundaries)
- `GetLevelFromXp_is_consistent_with_GetXpForLevel_for_first_30_levels` (60 implicit assertions)
- `Progress_returns_xp_in_level_and_xp_for_next_level`

---

## Background service

`StreakResetService : BackgroundService` is registered via
`builder.Services.AddHostedService<StreakResetService>()` and ticks every
hour. Each tick opens a fresh DI scope (`IServiceScopeFactory`), resolves
`AppDbContext`, and runs:

```sql
UPDATE user_streaks
   SET current_streak = 0
 WHERE current_streak > 0
   AND last_activity_date_utc < CURRENT_DATE - 1
```

via EF's `ExecuteUpdateAsync`. `longest_streak` is preserved through resets.
Tick failures are logged but never crash the host.

---

## Push notification flow (current state)

- VAPID keys: **placeholder/empty** in `appsettings.Development.json` with
  a `TODO: regenerate before deploy` note. With empty keys, the service
  startup logs a single warning and `SendToUserAsync` becomes a no-op.
- Once keys are real (`npx web-push generate-vapid-keys`), badge-earn calls
  in `EvaluateBadgesAsync` will fire a push per newly-earned badge,
  best-effort (push errors are logged but never fail the transaction —
  user still earns the badge).
- Dead subscriptions (push service returns 404/410) are pruned on next send.

---

## Test plan executed (curl commands)

All commands assume `STOK` = a freshly-issued student access token. Output
captured during the verification run; reproducible against the running
docker-compose stack.

```bash
# 1. Login + initial profile
curl -s -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"student01@langartlms.com","password":"password123"}'
curl -s -H "Authorization: Bearer $STOK" http://localhost:8080/api/gamification/me

# 2. Lesson complete (XP + first-lesson badge)
curl -s -X POST -H "Authorization: Bearer $STOK" \
  http://localhost:8080/api/progress/lessons/<lessonId>/complete

# 3. Idempotent re-complete (no XP change expected)
curl -s -X POST -H "Authorization: Bearer $STOK" \
  http://localhost:8080/api/progress/lessons/<lessonId>/complete

# 4. Perfect quiz
curl -s -X POST -H "Authorization: Bearer $STOK" \
  -H "Content-Type: application/json" \
  -d '{"lessonId":"<lessonId>","contentId":"<contentId>","score":2,"totalQuestions":2,"passed":true}' \
  http://localhost:8080/api/progress/quiz-results

# 5. Badges catalog
curl -s -H "Authorization: Bearer $STOK" http://localhost:8080/api/gamification/badges

# 6. Recent ledger
curl -s -H "Authorization: Bearer $STOK" \
  "http://localhost:8080/api/gamification/ledger/me?limit=20"

# 7. Leaderboard for the student's group
curl -s -H "Authorization: Bearer $STOK" \
  http://localhost:8080/api/gamification/leaderboard/<groupId>

# 8. Push subscribe (camelCase body — verifies wire contract)
curl -s -X POST -H "Authorization: Bearer $STOK" \
  -H "Content-Type: application/json" \
  -d '{"endpoint":"https://example.com/x","keys":{"p256dh":"BFake","auth":"FakeAuth"},"userAgent":"curl"}' \
  http://localhost:8080/api/notifications/push/subscribe
```

---

## Out of scope — confirmed not started

- AI features (Sprint 2): no work.
- SignalR real-time (Sprint 3): no work.
- Performance pass (Sprint 4): no work.
