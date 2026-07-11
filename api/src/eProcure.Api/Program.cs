using System.Text;
using eProcure.Api.Auth;
using eProcure.Api.Middleware;
using eProcure.Application.Abstractions;
using eProcure.Infrastructure;
using eProcure.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

const string DevCorsPolicy = "dev-web";

// --- Persistence + cross-cutting services ---
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
builder.Services.AddInfrastructure(connectionString);

// Vendor onboarding config (Slice B): magic-link + email transport. No secrets in code — config only.
builder.Services.Configure<eProcure.Application.Onboarding.OnboardingOptions>(builder.Configuration.GetSection("Onboarding"));
builder.Services.Configure<eProcure.Application.Sourcing.RfqGovernanceOptions>(builder.Configuration.GetSection("RfqGovernance"));
builder.Services.Configure<eProcure.Infrastructure.Email.EmailOptions>(builder.Configuration.GetSection("Email"));

// ICurrentUser is resolved from the request's JWT claims (lives in the Api layer).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// --- Dev auth ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection("Demo"));
builder.Services.AddSingleton<DevUserStore>();
builder.Services.AddSingleton<JwtTokenService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");
if (string.IsNullOrWhiteSpace(jwt.Key))
    throw new InvalidOperationException("Jwt:Key is not configured.");

// Two authentication schemes coexist behind a policy scheme (SEC-1/2): real JWTs when an
// Authorization: Bearer header is present, else the demo scheme (X-Demo-User → ClaimsPrincipal,
// inert outside demo mode). JWT validation is unchanged.
const string SmartScheme = "Smart";
builder.Services
    .AddAuthentication(SmartScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    })
    .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(DemoAuthenticationHandler.SchemeName, _ => { })
    .AddPolicyScheme(SmartScheme, SmartScheme, options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.Authorization.ToString()
                .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? JwtBearerDefaults.AuthenticationScheme
                    : DemoAuthenticationHandler.SchemeName;
    });

// Deny anonymous by default: every endpoint requires an authenticated principal unless it opts out
// with [AllowAnonymous] (SEC-1). New endpoints are closed unless someone deliberately opens them.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("Buyer", p => p.RequireRole("Buyer"))
    .AddPolicy("Approver", p => p.RequireRole("Approver"))
    .AddPolicy("TechEvaluator", p => p.RequireRole("TechEvaluator"))
    .AddPolicy("CommEvaluator", p => p.RequireRole("CommEvaluator"))
    .AddPolicy("Admin", p => p.RequireRole("Admin"))
    .AddPolicy("Vendor", p => p.RequireRole("Vendor"));

// Role-matrix authorization (Slice RM): "action:*" policies resolve from the declarative
// ActionCatalog (docs/AUTHORIZATION-MATRIX.md); authenticated-but-denied renders the existing
// 403 problem-details shape. The fallback deny-anonymous policy above is untouched.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, ActionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ForbiddenProblemHandler>();

// --- MVC + Swagger ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "eProcure API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT from /api/auth/dev-login (without the 'Bearer ' prefix).",
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc, null)] = new List<string>(),
    });
});

// --- CORS for the Vite dev server ---
builder.Services.AddCors(options => options.AddPolicy(DevCorsPolicy, policy =>
    policy.WithOrigins("http://localhost:5173")
          .AllowAnyHeader()
          .AllowAnyMethod()));

var app = builder.Build();

// Loud warning if X-Demo-User impersonation is live outside Development (SEC-1). In Production it is
// hard-blocked regardless of config, so this only fires for an explicit Staging/custom opt-in.
var demoOptions = app.Services.GetRequiredService<IOptions<DemoOptions>>().Value;
if (!app.Environment.IsDevelopment() && DemoOptions.IsActive(app.Environment, demoOptions))
    app.Logger.LogWarning(
        "DEMO MODE ENABLED — X-Demo-User impersonation is active. Never enable in production.");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Apply migrations and run the (idempotent) seeder hook in Development.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IDataSeeder>().SeedAsync();
}
else
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<ExceptionMiddleware>();
// CORS exists only for the Vite dev server on localhost:5173 — never open the browser trust
// boundary in a deployed environment (SEC-7).
if (app.Environment.IsDevelopment())
    app.UseCors(DevCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed for integration tests (WebApplicationFactory).
public partial class Program;
