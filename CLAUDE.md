# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**LangArt LMS backend** — ASP.NET Core 8 Web API (ported from NestJS). PostgreSQL 15+ via EF Core 8 (Npgsql). Frontend lives at `../lang-art-mvp/` (React 19 + Vite) and consumes this API on port 8080.

## Commands

```bash
# Run from src/LangArt.Api/
dotnet run                # http://localhost:8080 (uses launchSettings http profile, Development env)
dotnet build              # compile
dotnet watch              # hot-reload dev loop

# Run from repo root (lang-art-back/)
dotnet build              # build whole solution
dotnet ef migrations add <Name> --project src/LangArt.Api    # NOT YET USED — schema is owned by database/schema.sql
dotnet ef database update --project src/LangArt.Api          # ditto
```

**.NET SDK is pinned to 8.x** via [global.json](global.json). Do not target newer SDKs.

Frontend dev server: `cd ../lang-art-mvp && npm run dev` (port 5173, `VITE_API_BASE_URL` defaults to `http://localhost:8080/api`).

## Tech stack

ASP.NET Core 8 (Web API, `[ApiController]` + controllers), EF Core 8 + Npgsql + EFCore.NamingConventions (snake_case tables/columns), JWT bearer auth (`Microsoft.AspNetCore.Authentication.JwtBearer`), BCrypt.Net-Next (password hashing), Swashbuckle (Swagger), built-in `Microsoft.AspNetCore.RateLimiting`, `IFormFile` for uploads.

## Solution layout

```
lang-art-back/
├── LangArt.sln
├── global.json                                    # pins .NET 8 SDK
├── Dockerfile                                     # multi-stage SDK → ASPNET runtime
├── database/schema.sql                            # canonical Postgres DDL (still authoritative)
├── backups/                                       # SQL dumps
└── src/LangArt.Api/
    ├── Program.cs                                 # bootstrap, DI, middleware pipeline
    ├── appsettings.json + appsettings.Development.json
    ├── Common/
    │   ├── Serialization/JsonConfig.cs            # snake_case naming policy for OUTPUT
    │   ├── Filters/ApiResponse.cs                 # { success, data, message?, error? } envelope
    │   ├── Filters/ApiResponseFilter.cs           # wraps every 2xx ObjectResult
    │   ├── Middleware/ExceptionHandlingMiddleware.cs  # wraps 4xx/5xx in the envelope
    │   ├── Exceptions/ApiException.cs             # BadRequest/Unauthorized/Forbidden/NotFound/Conflict
    │   ├── Validation/ValidationProblemFactory.cs # ModelState 400s match the envelope
    │   ├── Auth/JwtTokenService.cs                # access + refresh JWT issuance / validation
    │   ├── Auth/ICurrentUser.cs                   # injectable accessor (≈ @CurrentUser())
    │   ├── Auth/DurationParser.cs                 # parses "15m" / "7d" env strings → TimeSpan
    │   ├── Configuration/AppOptions.cs            # JwtOptions, CorsOptions, UploadsOptions, etc.
    │   └── Configuration/ConnectionStringConverter.cs   # postgresql://… → Npgsql key=value
    ├── Data/
    │   ├── AppDbContext.cs                        # 16 DbSets, ApplyConfigurationsFromAssembly
    │   ├── Entities/                              # 16 POCOs mirroring Prisma models
    │   ├── Configurations/                        # IEntityTypeConfiguration<T> per entity
    │   └── Enums/                                 # Role, AttendanceStatus, ContentType, PaymentStatus (with [PgName])
    └── Features/
        ├── Auth/                                  # 8 endpoints at /api/auth/*
        ├── Health/                                # /api/health, /ready, /live
        ├── Users/                                 # admin CRUD (Phase B)
        ├── Courses/                               # curriculum (Phase B)
        ├── Classroom/                             # groups + attendance (Phase C)
        ├── Progress/                              # completions + quizzes (Phase C)
        ├── Payments/                              # (Phase C)
        └── Uploads/                               # IFormFile-based (Phase D)
```

## The wire contract (DO NOT BREAK)

The React frontend at `../lang-art-mvp/` is unchanged from when it ran against NestJS, so this API must reproduce three things exactly:

1. **Request bodies are camelCase, response bodies are snake_case.** Asymmetric on purpose — set up in [Program.cs](src/LangArt.Api/Program.cs):
   - Output formatter has `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower`.
   - Input formatter is overridden with `PropertyNameCaseInsensitive = true` and **no** naming policy, so the frontend's `{ refreshToken: "..." }` still binds to C# `RefreshToken`.
   - Always set `[JsonPropertyName]` only on a property you have *measured* against the frontend payload — don't second-guess the policy.

2. **Every response is wrapped in an envelope.**
   - Success: `{ success: true, data: <T>, message?: string }` (added by [ApiResponseFilter](src/LangArt.Api/Common/Filters/ApiResponseFilter.cs); controllers just `return Ok(payload)`).
   - Failure: `{ success: false, error: string, message: string, data?: any }` (added by [ExceptionHandlingMiddleware](src/LangArt.Api/Common/Middleware/ExceptionHandlingMiddleware.cs) or [ValidationProblemFactory](src/LangArt.Api/Common/Validation/ValidationProblemFactory.cs)).
   - **Never hand-build the envelope in a controller.** Throw an `ApiException` (see `Common/Exceptions/`) and let the middleware shape the response.

3. **All routes are prefixed `/api`.** Controllers declare `[Route("api/<resource>")]` directly. There is no `UsePathBase`.

## Auth model

- Auth/role checks are applied **globally**: [Program.cs](src/LangArt.Api/Program.cs) sets `FallbackPolicy = RequireAuthenticatedUser()`. Every endpoint requires JWT unless decorated with `[AllowAnonymous]`.
- Role-based access: `[Authorize(Roles = "admin")]` or `[Authorize(Roles = "admin,teacher")]`. Roles are lowercase: `admin`, `teacher`, `student`.
- Current-user accessor: inject `ICurrentUser` — gives you `Id` (Guid), `Email`, `Role`. Reads from JWT claims.
- Tokens: HS256, claims `sub` + `email` + `role`. Access TTL from `JWT_ACCESS_EXPIRES_IN` (default `15m`), refresh from `JWT_REFRESH_EXPIRES_IN` (default `7d`).
- Refresh tokens are persisted in the `sessions` table with `user_agent` + `ip_address` and **rotated** on every `/auth/refresh` (the row is updated, not re-inserted).
- Password hashing: `BCrypt.Net.BCrypt.HashPassword(pw, 10)`. Compatible with hashes produced by NestJS's `bcrypt` npm package, so existing seeded users keep working.
- Password reset tokens: 32-byte hex via `RandomNumberGenerator`, 1-hour TTL, logged to console only in `Development`.

## Database

EF Core entities map to the **existing Postgres tables** managed by `database/schema.sql`. Important rules:

- `UseSnakeCaseNamingConvention()` is on, so `Profile.FullName` → column `full_name`, `DbSet<Profile> Profiles` → table `profiles`. Three DbSets are deliberately singular to match Prisma's `@@map` names: `LessonContent` → `lesson_content`, `Attendance` → `attendance`, `StudentLessonAccess` → `student_lesson_access`.
- Postgres native enums are mapped on the `NpgsqlDataSourceBuilder` in [Program.cs](src/LangArt.Api/Program.cs) (`MapEnum<Role>("role")`, etc.). C# enum members are PascalCase but tagged with `[PgName("admin")]` so the Postgres labels match exactly.
- UUIDs default to `gen_random_uuid()` (configured per entity via `HasDefaultValueSql`). Timestamps default to `now()`.
- JSON columns (`content_payload`, `mistakes_log`, `metadata`) are mapped as `JsonDocument` and stored as `jsonb`.
- **No `Database.Migrate()` on startup** — the schema is owned externally. If you need DDL changes, edit `database/schema.sql` and apply it manually (or via Phase D's seeder workflow).

## Required environment variables

Same names as the prior NestJS server, so deploy configs port over unchanged:

```
DATABASE_URL                    postgresql://user:pass@host:port/db?schema=public
JWT_SECRET                      HS256 access-token secret (≥32 bytes)
JWT_REFRESH_SECRET              HS256 refresh-token secret (≥32 bytes)
JWT_ACCESS_EXPIRES_IN           e.g. 15m
JWT_REFRESH_EXPIRES_IN          e.g. 7d
CORS_ORIGIN                     comma-separated allowed origins (default http://localhost:5173)
PORT                            default 8080
API_PREFIX                      default api  (note: routes hardcode /api today)
UPLOAD_DIR                      default ./uploads
MAX_FILE_SIZE                   bytes
ALLOWED_FILE_TYPES              csv extensions
THROTTLE_TTL / THROTTLE_LIMIT   rate limit window seconds + permits
DEFAULT_ADMIN_EMAIL             used by seeders (Phase D)
DEFAULT_ADMIN_PASSWORD
```

[appsettings.Development.json](src/LangArt.Api/appsettings.Development.json) provides dev defaults that match the local Postgres container on port 5433 with user `langartuser` / `devpassword`.

## Conventions & gotchas

- **Camel-case in DTOs**: write C# DTOs in PascalCase (`FullName`). The input formatter matches incoming `fullName` case-insensitively; the output formatter snake-cases to `full_name`. Do not hand-write `JsonPropertyName("full_name")`.
- **No `[Required]` on `Guid` route params** — they bind from the path. Use `[Required]` only on body DTO members.
- **Use `ExecuteUpdateAsync` / `ExecuteDeleteAsync`** (EF Core 7+) for set-based writes to avoid loading entities — see `LogoutAsync` in [AuthService.cs](src/LangArt.Api/Features/Auth/AuthService.cs) for an example.
- **JSON column reads**: `JsonDocument` is `IDisposable` — use sparingly; or read as `string` and parse where needed.
- **`Module` name collision**: `LangArt.Api.Data.Entities.Module` shares its short name with `System.Reflection.Module`. Reference via the namespace or use a `using` alias when ambiguity bites the compiler.
- **Seed/test users**: per the Phase D plan, the default password for all seeded users will be `password123`; the admin uses `DEFAULT_ADMIN_PASSWORD`. Until seeders land, you can use the Postgres `backups/` SQL dump to populate the DB.
- **Snake_case wire format gotcha for enums**: enums serialize to lowercase strings (`"admin"`, `"present"`) thanks to the `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)`. Multi-word enum members would become `attendance_late` etc.

## Adding a new feature module

1. Create `Features/<Name>/` with a controller (`[Route("api/<resource>")]`), a service, a `Dtos/` subfolder.
2. Register the service in [Program.cs](src/LangArt.Api/Program.cs) (`builder.Services.AddScoped<XxxService>();`).
3. Inject `AppDbContext` for data, `ICurrentUser` for auth context, your own service for orchestration.
4. Return raw DTOs — the global filter wraps them in the envelope. Throw `ApiException`-derived types for errors.

## Gamification (Sprint 1)

**Where it lives.** All code under [Features/Gamification/](src/LangArt.Api/Features/Gamification/);
entities under [Data/Entities/](src/LangArt.Api/Data/Entities/) (`UserXp`,
`UserStreak`, `Badge`, `UserBadge`, `XpLedger`); the snake_case-to-enum
converter at [Data/Enums/XpReasonNames.cs](src/LangArt.Api/Data/Enums/XpReasonNames.cs);
schema DDL at [Data/Seeders/SeedRunner.EnsureSchemaUpgradesAsync](src/LangArt.Api/Data/Seeders/SeedRunner.cs).

**How XP is awarded.** The single entry point is `IGamificationService.AwardXpAsync(userId, reason, amount, sourceId, ct)`.
It does two things in sequence: inserts a row in `xp_ledger` (the audit log)
and `+=`'s the denormalised `user_xp.total_xp`. Idempotency is enforced **at
the database**, not in code:

- `uq_xp_ledger_source` — unique on `(user_id, reason, source_id) WHERE source_id IS NOT NULL`.
  Re-completing the same lesson, re-submitting the same quiz attempt id, etc.
  all collide here and become silent no-ops.
- `uq_xp_ledger_daily_login` — unique on `(user_id, created_at_utc::date) WHERE reason = 'daily_login'`.
  Caps the daily-login bonus at one per UTC day even though it has no `source_id`.

When `AwardXpAsync` hits a PG `23505` (unique violation) it detaches the
failed entry and returns `false`. Callers don't need to think about it.

**XP amounts** (canonical, in code at [GamificationService.cs](src/LangArt.Api/Features/Gamification/GamificationService.cs)
and [ProgressService.cs](src/LangArt.Api/Features/Progress/ProgressService.cs)):

| Event | XP | Where |
|---|---|---|
| Lesson completed (first time) | +20 | `ProgressService.MarkCompleteAsync` |
| Quiz passed (≥70%) | +30 | `ProgressService.SubmitQuizResultAsync` |
| Quiz perfect (100%) bonus | +20 | same |
| Daily login (once/UTC day) | +10 | `AuthService.LoginAsync` |
| Streak bonus | `min(currentStreak * 5, 50)` | `GamificationService.RecordActivityAsync` |
| Badge earned | per-badge `XpReward` | `GamificationService.EvaluateBadgesAsync` |

**Streaks.** `RecordActivityAsync` is called by lesson/quiz/login flows. It's
idempotent within a UTC day. The hourly `StreakResetService` (a
`BackgroundService` registered with `AddHostedService`) zeroes
`current_streak` for users whose `last_activity_date_utc < yesterday`.
`longest_streak` is preserved through resets.

**How badges work.** Catalog seeded by `SeedRunner.SeedBadgesAsync` (upsert by
`Code`). The criteria rule for each badge is hard-coded in
[BadgeCriteriaEvaluator.IsMetAsync](src/LangArt.Api/Features/Gamification/BadgeCriteriaEvaluator.cs)
— a switch on `badge.Code`. `EvaluateBadgesAsync` is called from every
gamification-relevant flow (lesson complete, quiz submit, login); it
diffs unmet vs already-earned, inserts `user_badges` rows, awards
`XpReward`, and best-effort sends a push notification per newly-earned
badge.

### Adding a new badge

1. Add a row in `SeedBadgesAsync` — `(code, name, description, xpReward)`.
2. Add a `case` in `BadgeCriteriaEvaluator.IsMetAsync` returning `true` when
   the criteria is met for `userId`.
3. Reseed (`dotnet run -- seed` or `docker compose exec backend dotnet LangArt.Api.dll seed`)
   — idempotent, upserts existing badges, only inserts the new one.

The `badges.criteria` jsonb column is intentional headroom for moving rules
out of the C# switch into data-driven JSON later (e.g.
`{ "kind": "streak", "days": 7 }`). Today it's `{}` for every seeded badge.

### Level curve

`xpForLevel(n) = floor(50 * n^1.5)` cumulative. A user starts at level 0 with
0 XP, crosses to level 1 at 50 XP, level 2 at 141, level 5 at 559, level 10
at 1581, level 25 at 6250, level 50 at 17677. The pure functions live at
[LevelCalculator.cs](src/LangArt.Api/Features/Gamification/LevelCalculator.cs);
test coverage in [tests/LangArt.Api.Tests/LevelCalculatorTests.cs](../tests/LangArt.Api.Tests/LevelCalculatorTests.cs).

### Endpoints

```
GET  /api/gamification/me                            — XP + streak + earned badges + recent 5 ledger
GET  /api/gamification/badges                        — full catalog with per-user earned flag
GET  /api/gamification/ledger/me?limit=20            — recent XP events, max 100
GET  /api/gamification/leaderboard/{groupId}         — group ranking; student must be a member, admin/teacher unrestricted
POST /api/notifications/push/subscribe               — upsert by endpoint
DELETE /api/notifications/push/subscribe             — by endpoint
```

## Real-time (Sprint 2)

**Where it lives.** All hub + dispatcher + tracker code under
[Features/Realtime/](src/LangArt.Api/Features/Realtime/). Existing
notification flow (`Features/Notifications/NotificationsService.NotifyAsync`)
now ALSO dispatches via SignalR after the DB insert. Badge-earn
(`Features/Gamification/GamificationService.EvaluateBadgesAsync`) does
**dual delivery** — Web Push for offline users + SignalR `BadgeEarned`
for online users.

### Hub paths

| URL | Hub | Purpose |
|---|---|---|
| `/hubs/notifications` | `NotificationsHub` | bell notifications + `BadgeEarned` + `JoinClassroomAsync` / `LeaveClassroomAsync` |
| `/hubs/presence` | `PresenceHub` | online tracking + reserved for Sprint 3 UserOnline/UserOffline broadcasts |
| `/hubs/live-lesson` | `LiveLessonHub` | existing — unchanged |

### JWT in the query string

WebSocket upgrades can't carry an `Authorization` header from a browser,
so SignalR's convention is to pass the JWT as `?access_token=…`. The
[`JwtBearerEvents.OnMessageReceived`](src/LangArt.Api/Program.cs) handler
honours this **only for `/hubs/*` paths** — every other request must still
use the `Authorization` header. Don't widen this.

### Wire format

The SignalR JSON protocol is configured (in `Program.cs`'s `AddSignalR().AddJsonProtocol(...)`)
to use `JsonNamingPolicy.SnakeCaseLower`, matching the REST output formatter.
Hub payload property names go on the wire as `user_id`, `created_at_utc`,
etc. — same as REST responses. **Do not add `[JsonPropertyName]` attributes.**

### Single-instance constraint

Connection tracking lives in-memory in
[`InMemoryConnectionTracker`](src/LangArt.Api/Features/Realtime/InMemoryConnectionTracker.cs)
(process singleton). **The API must run as a single instance** until a
Redis backplane (`Microsoft.AspNetCore.SignalR.StackExchangeRedis`) is
added. Horizontal scale-out will fan messages to the wrong process
otherwise. This is a Sprint 2 conscious limitation; revisit when load
demands. The interface (`IConnectionTracker`) is intentionally
Redis-shaped already so the swap is one impl swap + one DI line.

### IUserIdProvider

[`JwtSubUserIdProvider`](src/LangArt.Api/Features/Realtime/JwtSubUserIdProvider.cs)
reads `Context.User.FindFirst("sub")` so SignalR's `Clients.User(userId)`
routing matches the JWT `sub` claim. The default would also work today
(our JWT issues `NameIdentifier` with the same value), but binding to
`sub` explicitly future-proofs against claim-shape changes.

### Sending a hub message from a service

Inject `INotificationDispatcher`:

```csharp
public class MyService(INotificationDispatcher dispatcher) {
    public async Task DoThing(Guid userId) {
        // ... do the DB work ...
        await dispatcher.SendNotificationAsync(userId, new NotificationDto {
            Id = row.Id, UserId = userId, Type = "thing_happened",
            Title = "Done", CreatedAtUtc = DateTime.UtcNow,
        });
    }
}
```

The dispatcher is **best-effort** — exceptions are caught and logged, never
re-thrown. Offline users miss the live event; they'll see it next time
they hit the polling endpoint or reconnect.

### Adding a new hub event type

1. Add the C# method signature to the strongly-typed client interface
   (`INotificationsClient` or `IPresenceClient`).
2. The method name on that interface IS the wire name the frontend
   subscribes to (`connection.on("ThingHappened", …)`). Renaming = breaking
   change.
3. Add a payload DTO under `Features/Realtime/Dto/`. PascalCase property
   names — the snake_case naming policy handles the wire format.
4. Add a method to `INotificationDispatcher` if it's a recurring server
   trigger (so call-sites stay clean), or call `IHubContext` directly
   for one-offs.
5. Update [docs/sprint-2-signalr-frontend.md](docs/sprint-2-signalr-frontend.md)
   with the new event name + payload shape so frontend can subscribe.

### Why connection tracking is also a singleton

SignalR's `IUserIdProvider` answers "who's this connection?" at handshake
time but doesn't tell you "what connections does this user have?". The
tracker fills that gap for the REST presence endpoint and any future
"is X online?" widget. Keeping it as a process singleton matches the
SignalR backplane story — when Redis lands, the tracker moves into Redis
too, and the rest of the codebase doesn't change.

## Phase status

- ✅ **Phase A**: scaffolding, entities, cross-cutting plumbing, Auth (8 endpoints), Health (3 endpoints).
- ⏳ **Phase B**: Users (6 endpoints) + Courses (~15 nested endpoints).
- ⏳ Phase C: Classroom, Progress, Payments.
- ⏳ Phase D: Uploads + C# seeder.
- ⏳ Phase E: root `docker-compose.yml` orchestrating Postgres + backend + frontend.
