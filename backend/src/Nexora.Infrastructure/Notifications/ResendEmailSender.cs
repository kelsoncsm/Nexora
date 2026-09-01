using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Nexora.Application.Notifications;

namespace Nexora.Infrastructure.Notifications;

public sealed class ResendEmailSender(HttpClient httpClient, IOptions<EmailOptions> options) : IEmailSender
{
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Add("Idempotency-Key", message.IdempotencyKey);
        request.Headers.Authorization = new("Bearer", settings.ResendApiKey);
        request.Content = JsonContent.Create(new ResendRequest(
            $"{settings.FromName} <{settings.FromAddress}>",
            [message.Recipient],
            message.Subject,
            message.HtmlBody,
            settings.ReplyTo));

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var transient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
                throw new EmailProviderException(
                    $"Resend returned HTTP {(int)response.StatusCode}.",
                    transient ? EmailFailureKind.Transient : EmailFailureKind.Permanent);
            }
            var result = await response.Content.ReadFromJsonAsync<ResendResponse>(cancellationToken);
            return new EmailSendResult(result?.Id);
        }
        catch (EmailProviderException) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new EmailProviderException("Resend request failed.", EmailFailureKind.Transient, exception);
        }
    }

    private sealed record ResendRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("reply_to")] string? ReplyTo);
    private sealed record ResendResponse([property: JsonPropertyName("id")] string Id);
}
