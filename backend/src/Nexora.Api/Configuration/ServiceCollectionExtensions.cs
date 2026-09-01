using Nexora.Infrastructure;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Nexora.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Nexora.Api.Tenancy;
using Nexora.Application.Administration;
using Nexora.Application.Tenancy;
using System.Text.Json.Serialization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Resources;

namespace Nexora.Api.Configuration;

public static class ServiceCollectionExtensions
{
    private const string FrontendCorsPolicy = "Frontend";

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        if (configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] is { Length: > 0 } applicationInsights)
            services.AddOpenTelemetry().ConfigureResource(resource => resource.AddService("nexora-api"))
                .UseAzureMonitor(options => options.ConnectionString = applicationInsights);
        services.AddHsts(options => { options.MaxAge = TimeSpan.FromDays(365); options.IncludeSubDomains = true; options.Preload = true; });
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            foreach (var value in configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
                if (IPAddress.TryParse(value, out var address)) options.KnownProxies.Add(address);
            foreach (var value in configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [])
                if (System.Net.IPNetwork.TryParse(value, out var network)) options.KnownIPNetworks.Add(network);
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = static (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };
            AddFixedPolicy(options, "auth", 10, TimeSpan.FromMinutes(1));
            AddFixedPolicy(options, "onboarding", 60, TimeSpan.FromMinutes(1));
            AddFixedPolicy(options, "checkout", 10, TimeSpan.FromMinutes(1));
            AddFixedPolicy(options, "webhook", 120, TimeSpan.FromMinutes(1));
        });
        services.ConfigureHttpJsonOptions(options=>options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddInfrastructure(configuration);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<IdentityOptions>>((options, identityOptions) =>
            {
                var identity = identityOptions.Value;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = identity.Issuer,
                    ValidateAudience = true,
                    ValidAudience = identity.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(identity.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        services.AddAuthorization(options => { options.AddPolicy("PlatformAdmin", policy => policy.RequireClaim("permission", PlatformPermissions.Access).RequireAssertion(context => !context.User.HasClaim(x => x.Type == "tenant_id")));
            foreach(var permission in TenantPermissions.All)options.AddPolicy(permission,p=>p.RequireClaim("permission",permission).RequireClaim("tenant_id")); });

        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicy, policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy
                        .WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            });
        });

        return services;
    }

    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) app.UseHsts();
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
            using (app.Logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = context.TraceIdentifier }))
                await next();
        });
        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseCors(FrontendCorsPolicy);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseTenantContext();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        return app;
    }

    private static void AddFixedPolicy(RateLimiterOptions options, string name, int permitLimit, TimeSpan window) =>
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window, QueueLimit = 0, AutoReplenishment = true }));
}
