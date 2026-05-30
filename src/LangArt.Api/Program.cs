using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LangArt.Api.Common.Auth;
using LangArt.Api.Common.Configuration;
using LangArt.Api.Common.Filters;
using LangArt.Api.Common.Middleware;
using LangArt.Api.Common.Serialization;
using LangArt.Api.Common.Validation;
using LangArt.Api.Data;
using LangArt.Api.Data.Enums;
using LangArt.Api.Data.Seeders;
using LangArt.Api.Features.Auth;
using LangArt.Api.Features.Certificates;
using LangArt.Api.Features.Classroom;
using LangArt.Api.Features.Courses;
using LangArt.Api.Features.Live;
using LangArt.Api.Features.Notifications;
using LangArt.Api.Features.Payments;
using LangArt.Api.Features.Progress;
using LangArt.Api.Features.Uploads;
using LangArt.Api.Features.Users;
using LangArt.Api.Common.Email;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// -------- Configuration binding --------
builder.Services.Configure<JwtOptions>(o =>
{
    o.Secret = builder.Configuration["JWT_SECRET"] ?? builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT_SECRET not set");
    o.RefreshSecret = builder.Configuration["JWT_REFRESH_SECRET"] ?? builder.Configuration["Jwt:RefreshSecret"] ?? throw new InvalidOperationException("JWT_REFRESH_SECRET not set");
    o.AccessExpiresIn = builder.Configuration["JWT_ACCESS_EXPIRES_IN"] ?? builder.Configuration["Jwt:AccessExpiresIn"] ?? "15m";
    o.RefreshExpiresIn = builder.Configuration["JWT_REFRESH_EXPIRES_IN"] ?? builder.Configuration["Jwt:RefreshExpiresIn"] ?? "7d";
});
builder.Services.Configure<CorsOptions>(o =>
{
    o.Origin = builder.Configuration["CORS_ORIGIN"] ?? builder.Configuration["Cors:Origin"] ?? "http://localhost:5173";
});
builder.Services.Configure<UploadsOptions>(o =>
{
    o.Dir = builder.Configuration["UPLOAD_DIR"] ?? builder.Configuration["Uploads:Dir"] ?? "./uploads";
    o.MaxBytes = long.TryParse(builder.Configuration["MAX_FILE_SIZE"], out var max) ? max : 10 * 1024 * 1024;
    o.AllowedTypes = builder.Configuration["ALLOWED_FILE_TYPES"] ?? builder.Configuration["Uploads:AllowedTypes"] ?? o.AllowedTypes;
});
builder.Services.Configure<RateLimitOptions>(o =>
{
    o.WindowSeconds = int.TryParse(builder.Configuration["THROTTLE_TTL"], out var ttl) ? ttl : 60;
    o.PermitLimit = int.TryParse(builder.Configuration["THROTTLE_LIMIT"], out var lim) ? lim : 100;
});
builder.Services.Configure<SeedOptions>(o =>
{
    o.AdminEmail = builder.Configuration["DEFAULT_ADMIN_EMAIL"] ?? "admin@langartlms.com";
    o.AdminPassword = builder.Configuration["DEFAULT_ADMIN_PASSWORD"] ?? "admin123";
});

// -------- Database --------
var rawConn = builder.Configuration["DATABASE_URL"]
              ?? builder.Configuration.GetConnectionString("Default")
              ?? throw new InvalidOperationException("DATABASE_URL is not set");
var npgsqlConn = ConnectionStringConverter.ToNpgsql(rawConn);

// In the canonical Postgres schema, only `attendance_status` and `content_type`
// are true PG enums. `role` and payments `status` are TEXT columns with CHECK
// constraints — we'll map those via EF's HasConversion<string>() below.
var dataSourceBuilder = new NpgsqlDataSourceBuilder(npgsqlConn);
dataSourceBuilder.MapEnum<AttendanceStatus>("attendance_status");
dataSourceBuilder.MapEnum<ContentType>("content_type");
var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);

builder.Services.AddDbContext<AppDbContext>(opts =>
{
    opts.UseNpgsql(dataSource);
    opts.UseSnakeCaseNamingConvention();
});

// -------- Auth --------
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UsersService>();
builder.Services.AddScoped<CoursesService>();
builder.Services.AddScoped<ClassroomService>();
builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<PaymentsService>();
builder.Services.AddSingleton<UploadsService>();
builder.Services.AddScoped<NotificationsService>();
builder.Services.AddScoped<CertificateService>();
builder.Services.AddScoped<LiveSessionsService>();
builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();

// Sprint 1: Gamification + Push
builder.Services.AddScoped<LangArt.Api.Features.Gamification.IGamificationService, LangArt.Api.Features.Gamification.GamificationService>();
builder.Services.AddHostedService<LangArt.Api.Features.Gamification.StreakResetService>();

// Phase 1: Vocabulary
builder.Services.AddScoped<LangArt.Api.Features.Vocabulary.IVocabularyService,
                           LangArt.Api.Features.Vocabulary.VocabularyService>();
builder.Services.AddScoped<LangArt.Api.Features.Vocabulary.IDailyVocabularyService,
                           LangArt.Api.Features.Vocabulary.DailyVocabularyService>();
builder.Services.AddScoped<LangArt.Api.Features.Finance.IFinanceService,
                           LangArt.Api.Features.Finance.FinanceService>();

// Phase 2: Multilingual translations
builder.Services.AddScoped<LangArt.Api.Features.Translations.TranslationsService>();

// Phase 4: LA Dollar currency
builder.Services.AddScoped<LangArt.Api.Features.LaDollar.ILaDollarService,
                           LangArt.Api.Features.LaDollar.LaDollarService>();

// Phase 3: Speaking + AI grading. If OPENAI_API_KEY is set we run real Whisper
// + GPT grading; otherwise fall back to the deterministic stub so the rest of
// the speaking pipeline (submit / review queue / teacher override) still works.
builder.Services.AddScoped<LangArt.Api.Features.Speaking.SpeakingService>();
builder.Services.AddHttpClient();
builder.Services.Configure<LangArt.Api.Features.Speaking.OpenAiOptions>(o =>
{
    o.ApiKey = builder.Configuration["OPENAI_API_KEY"] ?? builder.Configuration["OpenAi:ApiKey"] ?? string.Empty;
    o.ChatModel = builder.Configuration["OPENAI_CHAT_MODEL"] ?? builder.Configuration["OpenAi:ChatModel"] ?? "gpt-4o-mini";
    o.TranscriptionModel = builder.Configuration["OPENAI_TRANSCRIPTION_MODEL"] ?? builder.Configuration["OpenAi:TranscriptionModel"] ?? "whisper-1";
    o.TranscriptionLanguage = builder.Configuration["OPENAI_TRANSCRIPTION_LANGUAGE"] ?? builder.Configuration["OpenAi:TranscriptionLanguage"] ?? "en";
});
var openAiKey = builder.Configuration["OPENAI_API_KEY"] ?? builder.Configuration["OpenAi:ApiKey"];
if (!string.IsNullOrWhiteSpace(openAiKey))
{
    builder.Services.AddSingleton<LangArt.Api.Features.Speaking.IAiSpeechGradingService,
                                  LangArt.Api.Features.Speaking.OpenAiSpeechGradingService>();
}
else
{
    builder.Services.AddSingleton<LangArt.Api.Features.Speaking.IAiSpeechGradingService,
                                  LangArt.Api.Features.Speaking.StubAiSpeechGradingService>();
}

// Phase 8: Teacher analytics (per-student skill levels + notes).
builder.Services.AddScoped<LangArt.Api.Features.Analytics.AnalyticsService>();

// Phase 11: Achievements (monthly recognition).
builder.Services.AddScoped<LangArt.Api.Features.Achievements.AchievementsService>();

// Phase 12: Exam system (Unit Review / Progress Exam / Midterm / Final / Placement).
builder.Services.AddScoped<LangArt.Api.Features.Exams.ExamsService>();

// Phase 14: Parent portal.
builder.Services.AddScoped<LangArt.Api.Features.Parents.ParentsService>();

// Phase 16: Centralized messaging.
builder.Services.AddScoped<LangArt.Api.Features.Messaging.MessagesService>();

// Phase 18: CRM / Lead funnel.
builder.Services.AddScoped<LangArt.Api.Features.Crm.CrmService>();

// Phase 19: Multibranch.
builder.Services.AddScoped<LangArt.Api.Features.Branches.BranchesService>();

// Phase 20: Extra-curricular events.
builder.Services.AddScoped<LangArt.Api.Features.Clubs.ClubsService>();

// Phase 21: Referrals + Alumni.
builder.Services.AddScoped<LangArt.Api.Features.Referrals.ReferralsService>();

// Phase 22: Legal / Compliance.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<LangArt.Api.Features.Legal.LegalService>();

// Wisdom dictionary importer (singleton — holds in-memory job state)
builder.Services.AddSingleton<LangArt.Api.Features.Vocabulary.WisdomImportService>();

// Phase 15: AI Chat ("Ask from LA") — student tutor chat in lesson context.
if (!string.IsNullOrWhiteSpace(openAiKey))
{
    builder.Services.AddSingleton<LangArt.Api.Features.AiChat.IAiChatService,
                                  LangArt.Api.Features.AiChat.OpenAiChatService>();
}
else
{
    builder.Services.AddSingleton<LangArt.Api.Features.AiChat.IAiChatService,
                                  LangArt.Api.Features.AiChat.StubAiChatService>();
}

// Phase 7: Writing + AI grading (same key as Speaking).
builder.Services.AddScoped<LangArt.Api.Features.Writing.WritingService>();
if (!string.IsNullOrWhiteSpace(openAiKey))
{
    builder.Services.AddSingleton<LangArt.Api.Features.Writing.IAiWritingGradingService,
                                  LangArt.Api.Features.Writing.OpenAiWritingGradingService>();
}
else
{
    builder.Services.AddSingleton<LangArt.Api.Features.Writing.IAiWritingGradingService,
                                  LangArt.Api.Features.Writing.StubAiWritingGradingService>();
}
builder.Services.AddScoped<LangArt.Api.Features.Notifications.Push.IPushNotificationService, LangArt.Api.Features.Notifications.Push.PushNotificationService>();
builder.Services.Configure<LangArt.Api.Features.Notifications.Push.PushOptions>(o =>
{
    o.VapidPublicKey = builder.Configuration["Notifications:Push:VapidPublicKey"] ?? string.Empty;
    o.VapidPrivateKey = builder.Configuration["Notifications:Push:VapidPrivateKey"] ?? string.Empty;
    o.Subject = builder.Configuration["Notifications:Push:Subject"] ?? "mailto:no-reply@langartlms.com";
});

// Sprint 2: Realtime. Connection tracker is process-singleton — single-instance only
// until a Redis backplane is wired (see CLAUDE.md ## Real-time). Dispatcher + classroom
// service are stateless and scoped.
builder.Services.AddSingleton<LangArt.Api.Features.Realtime.IConnectionTracker,
                              LangArt.Api.Features.Realtime.InMemoryConnectionTracker>();
builder.Services.AddScoped<LangArt.Api.Features.Realtime.INotificationDispatcher,
                           LangArt.Api.Features.Realtime.NotificationDispatcher>();
builder.Services.AddScoped<LangArt.Api.Features.Realtime.IClassroomConnectionService,
                           LangArt.Api.Features.Realtime.ClassroomConnectionService>();
builder.Services.Configure<SmtpOptions>(o =>
{
    o.Host = builder.Configuration["SMTP_HOST"] ?? "smtp4dev";
    o.Port = int.TryParse(builder.Configuration["SMTP_PORT"], out var p) ? p : 25;
    o.Username = builder.Configuration["SMTP_USERNAME"];
    o.Password = builder.Configuration["SMTP_PASSWORD"];
    o.FromAddress = builder.Configuration["SMTP_FROM"] ?? "no-reply@langartlms.com";
    o.FromName = builder.Configuration["SMTP_FROM_NAME"] ?? "LangArt LMS";
    o.UseSsl = string.Equals(builder.Configuration["SMTP_USE_SSL"], "true", StringComparison.OrdinalIgnoreCase);
});

var jwtSecret = builder.Configuration["JWT_SECRET"] ?? builder.Configuration["Jwt:Secret"]!;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
        };
        // Browsers can't attach custom headers to a WebSocket upgrade, so SignalR's
        // convention is to pass the JWT as an `access_token` query string. Only honored
        // for /hubs/* routes to avoid widening attack surface on the REST API.
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization(o =>
{
    // Every endpoint requires auth by default; opt out with [AllowAnonymous].
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

// -------- MVC / JSON --------
// Frontend sends camelCase request bodies but expects snake_case responses,
// so we configure input and output formatters separately.
builder.Services
    .AddControllers(mvc =>
    {
        mvc.Filters.Add<ApiResponseFilter>();
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        o.InvalidModelStateResponseFactory = ValidationProblemFactory.Create;
    })
    .AddJsonOptions(o =>
    {
        // Output options: snake_case keys, enums as snake_case strings.
        JsonConfig.Configure(o.JsonSerializerOptions);
    });

// SignalR: snake_case payloads on the wire so hub messages match the REST output convention.
// Detailed errors are only safe in Development; in Production, server-side exceptions stay
// opaque to the client (which is what we want — clients reconnect on close, they don't need
// the stack trace).
builder.Services
    .AddSignalR(o =>
    {
        o.EnableDetailedErrors = builder.Environment.IsDevelopment();
        o.HandshakeTimeout = TimeSpan.FromSeconds(15);
        o.KeepAliveInterval = TimeSpan.FromSeconds(15);
    })
    .AddJsonProtocol(o =>
    {
        o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.PayloadSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    });

// Sprint 2: SignalR uses `sub` from our JWT, not the default NameIdentifier. They happen to
// be the same value today (JwtTokenService.BuildClaims writes both), but binding the user-id
// provider to `sub` explicitly is safer against future JWT-shape changes.
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider,
                              LangArt.Api.Features.Realtime.JwtSubUserIdProvider>();

// Swap the input formatter for one that accepts camelCase (no naming policy, case-insensitive).
builder.Services.AddOptions<MvcOptions>().Configure<ILoggerFactory>((mvc, lf) =>
{
    var inputJsonOptions = new Microsoft.AspNetCore.Mvc.JsonOptions();
    var inputOpts = inputJsonOptions.JsonSerializerOptions;
    inputOpts.PropertyNameCaseInsensitive = true;
    inputOpts.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    inputOpts.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));

    var existing = mvc.InputFormatters.OfType<SystemTextJsonInputFormatter>().ToList();
    foreach (var f in existing) mvc.InputFormatters.Remove(f);

    mvc.InputFormatters.Insert(0, new SystemTextJsonInputFormatter(
        inputJsonOptions,
        lf.CreateLogger<SystemTextJsonInputFormatter>()));
});

// -------- CORS --------
var corsOrigin = builder.Configuration["CORS_ORIGIN"] ?? builder.Configuration["Cors:Origin"] ?? "http://localhost:5173";
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p => p
        .WithOrigins(corsOrigin.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// -------- Rate limiting --------
var throttleTtl = int.TryParse(builder.Configuration["THROTTLE_TTL"], out var ttlSec) ? ttlSec : 60;
var throttleLimit = int.TryParse(builder.Configuration["THROTTLE_LIMIT"], out var lim2) ? lim2 : 100;
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = throttleLimit,
                Window = TimeSpan.FromSeconds(throttleTtl),
                AutoReplenishment = true,
            }));
});

// -------- Swagger --------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "LangArt LMS API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT bearer token. Paste the access token only.",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
});

// -------- Pipeline --------
var app = builder.Build();

// -------- CLI seeder branch --------
// `dotnet run -- seed | clear | reset | seed:test-english | seed:test-english:force` executes the seeder and exits.
if (args.Length > 0 && (
        args[0] == "seed" ||
        args[0] == "clear" ||
        args[0] == "reset" ||
        args[0] == "seed:test-english" ||
        args[0] == "seed:test-english:force" ||
        args[0] == "clear:non-grammar"))
{
    var exit = await SeedRunner.RunAsync(args[0], app.Services);
    Environment.Exit(exit);
}

// On every startup, apply lightweight additive schema upgrades (new columns / new tables)
// so the canonical schema.sql + later-added features stay in sync without a full EF
// migrations setup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LangArt.Api.Data.AppDbContext>();
    try
    {
        await LangArt.Api.Data.Seeders.SeedRunner.EnsureSchemaUpgradesAsync(db);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Schema upgrade step failed (continuing — first DB connection may not be ready yet).");
    }
}

// Global exception handler first.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors();

// Default wwwroot static files — used by the live-lesson hub test page (test-hub.html).
// Harmless in production; the directory has no secrets.
app.UseStaticFiles();

// Static /uploads serving.
var uploadsOpts = app.Services.GetRequiredService<IOptions<UploadsOptions>>().Value;
var uploadsAbsolute = Path.GetFullPath(uploadsOpts.Dir);
Directory.CreateDirectory(uploadsAbsolute);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsAbsolute),
    RequestPath = "/uploads",
});

// Swagger at /api/docs (kept BEFORE auth so docs are publicly browsable).
app.UseSwagger(o =>
{
    o.RouteTemplate = "api/docs/{documentName}/swagger.json";
});
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/api/docs/v1/swagger.json", "LangArt LMS API v1");
    o.RoutePrefix = "api/docs";
    o.DocumentTitle = "LangArt API Docs";
    o.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    o.EnableFilter();
    o.DisplayRequestDuration();
});

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<LangArt.Api.Features.Live.LiveLessonHub>("/hubs/live-lesson");
// Sprint 2 hubs
app.MapHub<LangArt.Api.Features.Realtime.Hubs.NotificationsHub>("/hubs/notifications");
app.MapHub<LangArt.Api.Features.Realtime.Hubs.PresenceHub>("/hubs/presence");

var port = builder.Configuration["PORT"] ?? "8080";
app.Logger.LogInformation("Server running on http://localhost:{Port}", port);
app.Logger.LogInformation("API endpoint: http://localhost:{Port}/api", port);
app.Logger.LogInformation("Swagger docs: http://localhost:{Port}/api/docs", port);

app.Run();
