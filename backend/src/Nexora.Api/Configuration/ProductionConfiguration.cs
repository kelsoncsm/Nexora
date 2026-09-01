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

        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid Production configuration: " + string.Join(" ", errors));
    }

    private static void Require(string? value, string name, List<string> errors, int minimumLength = 1)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < minimumLength) errors.Add($"{name} is required.");
    }
}
