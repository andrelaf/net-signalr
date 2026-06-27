using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SignalRDemo.Api.Auth;
using SignalRDemo.Api.Hubs;
using SignalRDemo.Api.Hubs.Filters;
using SignalRDemo.Api.Services;
using SignalRDemo.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuração tipada
// ---------------------------------------------------------------------------
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
var clientOrigins = builder.Configuration.GetSection("Cors:ClientOrigins").Get<string[]>()
                    ?? ["http://localhost:5173"];

// ---------------------------------------------------------------------------
// Persistência (EF Core + SQLite portátil)
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// ---------------------------------------------------------------------------
// Autenticação JWT — inclui leitura do token via query string (?access_token=)
// porque navegadores não enviam headers no handshake de WebSocket.
// ---------------------------------------------------------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ManagersOnly", p => p.RequireRole("Manager"));
    options.AddPolicy("StaffOnly", p => p.RequireRole("Agent", "Manager"));
});

// ---------------------------------------------------------------------------
// SignalR — MessagePack + HubOptions + filtros globais + backplane Redis opcional
// ---------------------------------------------------------------------------
var signalR = builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.KeepAliveInterval = TimeSpan.FromSeconds(10);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        options.HandshakeTimeout = TimeSpan.FromSeconds(15);
        options.MaximumReceiveMessageSize = 64 * 1024;          // 64 KB — relevante p/ upload em chunks
        options.StreamBufferCapacity = 12;                      // buffer de streaming client->server
        options.MaximumParallelInvocationsPerClient = 2;

        // Filtros (cross-cutting): logging/métricas e tradução de exceções.
        options.AddFilter<HubLoggingFilter>();
        options.AddFilter<HubExceptionFilter>();
    })
    .AddMessagePackProtocol();   // protocolo binário; cliente JS pode usar JSON ou MessagePack

// Scale-out horizontal: ativado por configuração (SignalR:Backplane = "Redis").
if (string.Equals(builder.Configuration["SignalR:Backplane"], "Redis", StringComparison.OrdinalIgnoreCase))
{
    var redis = builder.Configuration["SignalR:RedisConnection"] ?? "localhost:6379";
    signalR.AddStackExchangeRedis(redis, o => o.Configuration.ChannelPrefix =
        StackExchange.Redis.RedisChannel.Literal("signalrdemo"));
}

// IUserIdProvider customizado -> base para Clients.User(id) e DMs.
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();

// ---------------------------------------------------------------------------
// Serviços de aplicação
// ---------------------------------------------------------------------------
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddSingleton<PresenceTracker>();   // estado em memória de presença (por nó)
builder.Services.AddHostedService<SlaMonitorService>();

// ---------------------------------------------------------------------------
// CORS — precisa de AllowCredentials + origens explícitas para o SignalR
// ---------------------------------------------------------------------------
builder.Services.AddCors(o => o.AddPolicy("client", p => p
    .WithOrigins(clientOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// ---------------------------------------------------------------------------
// Rate limiting — protege endpoints REST e o handshake dos hubs
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(10),
                PermitLimit = 100,
                QueueLimit = 0
            }));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SignalR Demo — Help Desk", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Cole apenas o token JWT (sem o prefixo 'Bearer')."
    });
    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", null, null), new List<string>() }
    });
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Migrations + seed de dados de demonstração
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbInitializer.InitializeAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("client");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers().RequireRateLimiting("api");

// Transportes habilitados explicitamente (fallback WebSockets -> SSE -> Long Polling)
// e stateful reconnect (SignalR mantém buffer p/ retomar sem perder mensagens).
const HttpTransportType allTransports =
    HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling;

app.MapHub<WorkspaceHub>("/hubs/workspace", o =>
{
    o.Transports = allTransports;
    o.AllowStatefulReconnects = true;
});

app.MapHub<DashboardHub>("/hubs/dashboard", o =>
{
    o.Transports = allTransports;
});

app.MapGet("/", () => Results.Ok(new { service = "SignalR Demo Help Desk", docs = "/swagger" }));

app.Run();
