using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Nexora.Application.Identity;
using Nexora.Application.Tenancy;
using Nexora.Application.Administration;
using Nexora.Application.Plans;
using Nexora.Application.Customers;
using Nexora.Application.Catalog;
using Nexora.Application.Scheduling;
using Nexora.Application.Billing;
using Nexora.Application.Reports;
using Nexora.Application.Onboarding;
using Nexora.Application.Verticals;

namespace Nexora.Api.Configuration;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private static readonly Action<ILogger, string, Exception?> LogUnhandledException =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, nameof(GlobalExceptionHandler)),
            "Unhandled exception while processing request {RequestId}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, extensions) = exception switch
        {
            IdentityValidationException validation => (StatusCodes.Status400BadRequest, "Validation failed.",
                new Dictionary<string, object?> { ["errors"] = validation.Errors }),
            IdentityConflictException => (StatusCodes.Status409Conflict, exception.Message, []),
            TenantConflictException => (StatusCodes.Status409Conflict, exception.Message, []),
            TenantValidationException => (StatusCodes.Status400BadRequest, exception.Message, []),
            TenantForbiddenException => (StatusCodes.Status403Forbidden, exception.Message, []),
            AdministrationValidationException => (StatusCodes.Status400BadRequest, exception.Message, []),
            AdministrationConflictException => (StatusCodes.Status409Conflict, exception.Message, []),
            PlanCatalogValidationException => (StatusCodes.Status400BadRequest, exception.Message, []),
            PlanCatalogConflictException => (StatusCodes.Status409Conflict, exception.Message, []),
            FeatureNotInPlanException feature => (StatusCodes.Status403Forbidden, "Feature not available in plan.",
                new Dictionary<string, object?> { ["code"] = "feature_not_in_plan", ["feature"] = feature.FeatureCode }),
            PlanLimitExceededException limit => (StatusCodes.Status409Conflict, "Plan limit reached.",
                new Dictionary<string, object?>
                {
                    ["code"] = "plan_limit_reached",
                    ["feature"] = limit.FeatureCode,
                    ["limit"] = limit.Limit,
                    ["current"] = limit.Current
                }),
            CustomerValidationException => (StatusCodes.Status400BadRequest, exception.Message, []),
            CatalogValidationException => (StatusCodes.Status400BadRequest, exception.Message, []),
            SchedulingValidationException => (StatusCodes.Status400BadRequest, exception.Message, []),
            SchedulingConflictException => (StatusCodes.Status409Conflict, exception.Message, []),
            BillingValidationException => (StatusCodes.Status400BadRequest, exception.Message, []),
            BillingConflictException => (StatusCodes.Status409Conflict, exception.Message, []),
            PaymentValidationException => (StatusCodes.Status400BadRequest, exception.Message, []),
            PaymentConflictException => (StatusCodes.Status409Conflict, exception.Message, []),
            PaymentGatewayUnavailableException => (StatusCodes.Status503ServiceUnavailable, exception.Message, []),
            ReportValidationException => (StatusCodes.Status400BadRequest, exception.Message, []),
            OnboardingValidationException => (StatusCodes.Status400BadRequest, exception.Message, []),
            OnboardingConflictException => (StatusCodes.Status409Conflict, exception.Message, []),
            VerticalSetupValidationException => (StatusCodes.Status400BadRequest, exception.Message, []),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Invalid request body.", []),
            InvalidCredentialsException or InvalidRefreshTokenException =>
                (StatusCodes.Status401Unauthorized, "Authentication failed.", []),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", [])
        };
        if (status == StatusCodes.Status500InternalServerError)
            LogUnhandledException(logger, httpContext.TraceIdentifier, exception);
        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Extensions = { ["requestId"] = httpContext.TraceIdentifier }
        };
        foreach (var extension in extensions) problemDetails.Extensions[extension.Key] = extension.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        });
    }
}
