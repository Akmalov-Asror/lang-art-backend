# LangArt LMS — Backend

ASP.NET Core 8 Web API powering the LangArt language-learning platform. Postgres via
EF Core 8, JWT bearer auth, all the usual LMS surfaces (courses, lessons, exercises,
groups, attendance, payments) plus a few extras: file uploads, completion certificates
(PDF), bell-icon notifications, quiz analytics, and 2FA scaffolding.

The companion React frontend lives at [lang-art-mvp](../lang-art-mvp) and is unchanged
from when it ran against the original NestJS server — this backend reproduces the same
wire contract.

## Run it locally

The easiest path is the full docker-compose stack at the repo root (Postgres, this
backend, the frontend, and an smtp4dev mail catcher). From `lang-art-structure/`:

```bash
docker compose up -d --build
```

| Service | URL |
|---|---|
| Frontend (nginx) | http://localhost:5173 |
| Backend API | http://localhost:8080/api |
| Swagger | http://localhost:8080/api/docs |
| smtp4dev (email catcher) | http://localhost:5000 |
| Postgres | localhost:5433 (`langartuser` / `devpassword`) |

Seed sample data once the stack is up:

```bash
docker compose exec backend dotnet LangArt.Api.dll reset
```

This drops + recreates a default admin, 2 teachers, 10 students, 6 courses with mixed
exercise types (quiz / listening / writing / fill-in-the-blank), 2 classroom groups,
and a few sample payments.

### Default users

| Role | Email | Password |
|---|---|---|
| Admin | `admin@langartlms.com` | `admin123` |
| Teacher | `teacher.aiko@langartlms.com` | `password123` |
| Teacher | `teacher.mateo@langartlms.com` | `password123` |
| Students | `student01@…` … `student10@langartlms.com` | `password123` |

## Run without Docker

Requires .NET 8 SDK and a Postgres 15+ instance:

```bash
cd src/LangArt.Api
dotnet run                         # http://localhost:8080
dotnet run -- seed                 # populate dev data
dotnet run -- reset                # clear + seed
```

[`appsettings.Development.json`](src/LangArt.Api/appsettings.Development.json) holds
the dev defaults — change them via environment variables in any other environment.

## Tech stack

ASP.NET Core 8 · EF Core 8 + Npgsql · MailKit + smtp4dev · QuestPDF (certificates)
· OtpNet (TOTP) · BCrypt.Net-Next · Swashbuckle · `Microsoft.AspNetCore.RateLimiting`.

## Project structure

```
src/LangArt.Api/
├── Program.cs                       bootstrap, pipeline, DI
├── Common/                          serialization, filters, middleware, auth, email
├── Data/
│   ├── Entities/                    16 EF entities mirroring the SQL schema
│   ├── Configurations/              IEntityTypeConfiguration<T> per entity
│   ├── Enums/                       Role, AttendanceStatus, ContentType, PaymentStatus
│   └── Seeders/SeedRunner.cs        CLI: seed | clear | reset
└── Features/
    ├── Auth/                        login / refresh / me / 2FA
    ├── Users/                       admin CRUD
    ├── Courses/                     courses → modules → lessons → content + resources
    ├── Classroom/                   groups + attendance
    ├── Progress/                    completions + quizzes + access gating + reports
    ├── Payments/
    ├── Uploads/                     thumbnails, lesson resources
    ├── Notifications/               bell-icon system
    ├── Analytics/                   per-question quiz stats
    ├── Certificates/                PDF download on course completion
    └── Health/                      /health, /ready, /live
```

64 HTTP routes — full list in Swagger.

## Wire contract (don't break these)

The frontend at `../lang-art-mvp` was originally written for the prior NestJS API and
is unchanged. This API reproduces three things exactly:

1. **Request bodies are camelCase, response bodies are snake_case.** Configured in
   `Program.cs` with separate JSON formatters.
2. **Every success response is wrapped** as `{ success: true, data: <T>, message? }`
   by `Common/Filters/ApiResponseFilter.cs`; failures are wrapped by
   `ExceptionHandlingMiddleware` into `{ success: false, error, message }`.
3. **JWT bearer auth** with HS256, claims `sub` / `email` / `role`. Refresh tokens are
   persisted in the `sessions` table and rotated on every `/auth/refresh`.

See [CLAUDE.md](CLAUDE.md) for the full architectural rundown, the env-var list, and
the conventions used throughout the codebase.

## Required env vars (set by docker-compose in dev)

```
DATABASE_URL                postgresql://user:pass@host:port/db?schema=public
JWT_SECRET / JWT_REFRESH_SECRET
JWT_ACCESS_EXPIRES_IN / JWT_REFRESH_EXPIRES_IN     e.g. 15m / 7d
CORS_ORIGIN
PORT  API_PREFIX
UPLOAD_DIR  MAX_FILE_SIZE  ALLOWED_FILE_TYPES
THROTTLE_TTL  THROTTLE_LIMIT
DEFAULT_ADMIN_EMAIL  DEFAULT_ADMIN_PASSWORD
SMTP_HOST  SMTP_PORT  SMTP_FROM  SMTP_FROM_NAME    (optional; falls back to console logging)
```

## License

No license yet — code is © 2026 LangArt. Add one before publishing more broadly.
