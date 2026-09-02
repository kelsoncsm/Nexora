using System.Net;

namespace Nexora.Api.Configuration;

public static class ProductionConfiguration
{
    public static void ValidateProductionConfiguration(this IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction()) return;
        var errors = new List<string>();
        Require(configuration.GetConnectionString("NexoraDatabase"), "ConnectionStrings:NexoraDatabase", errors);
        Require(configuration["Identity:SigningKey"], "Identity:SigningKey", errors, 32);
        Require(configuration["Payments:MercadoPago:AccessToken"], "Payments:MercadoPago:AccessToken", errors);
        Require(configuration["Payments:MercadoPago:WebhookSecret"], "Payments:MercadoPago:WebhookSecret", errors);
        Require(configuration["Email:FromAddress"], "Email:FromAddress", errors);
        Require(configuration["Email:ResendApiKey"], "Email:ResendApiKey", errors);
        Require(configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"], "APPLICATIONINSIGHTS_CONNECTION_STRING", errors);

        if (!string.Equals(configuration["Email:Provider"], "Resend", StringComparison.OrdinalIgnoreCase))
            errors.Add("Email:Provider must be Resend in Production.");
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0 || origins.Any(x => !Uri.TryCreate(x, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || x.Contains('*')))
            errors.Add("Cors:AllowedOrigins must contain only explicit HTTPS origins in Production.");
        var allowedHosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts is "*" or "localhost" || allowedHosts.Contains("__", StringComparison.Ordinal))
            errors.Add("AllowedHosts must explicitly identify the production API host.");

        ValidateReverseProxy(configuration, errors);

        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid Production configuration: " + string.Join(" ", errors));
    }

    /// <summary>
    /// In Production the API runs behind a reverse proxy (ingress) and relies on forwarded headers for
    /// the client IP (rate limiting, audit) and scheme (HTTPS redirect). Without an explicit list of
    /// trusted proxies the forwarded-headers middleware either ignores the headers (all traffic collapses
    /// to a single IP) or, misconfigured, trusts a spoofable <c>X-Forwarded-For</c>. Require at least one
    /// trusted proxy address or network and reject entries that do not parse.
    /// </summary>
    private static void ValidateReverseProxy(IConfiguration configuration, List<string> errors)
    {
        var proxies = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
        var networks = configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [];

        if (proxies.Length == 0 && networks.Length == 0)
        {
            errors.Add("ReverseProxy:KnownProxies or ReverseProxy:KnownNetworks must list the trusted proxy in Production.");
            return;
        }

        var invalidProxies = proxies.Where(x => !IPAddress.TryParse(x, out _)).ToArray();
        if (invalidProxies.Length > 0)
            errors.Add($"ReverseProxy:KnownProxies has invalid IP addresses: {string.Join(", ", invalidProxies)}.");

        var invalidNetworks = networks.Where(x => !System.Net.IPNetwork.TryParse(x, out _)).ToArray();
        if (invalidNetworks.Length > 0)
            errors.Add($"ReverseProxy:KnownNetworks has invalid CIDR ranges: {string.Join(", ", invalidNetworks)}.");
    }

    private static void Require(string? value, string name, List<string> errors, int minimumLength = 1)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < minimumLength) errors.Add($"{name} is required.");
    }
}
