using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nexora.Application.Billing;
using Nexora.Domain.Billing;

namespace Nexora.Infrastructure.Billing;

public sealed class MercadoPagoOptions{public const string SectionName="Payments:MercadoPago";public string AccessToken{get;set;}=string.Empty;public string WebhookSecret{get;set;}=string.Empty;public string BaseUrl{get;set;}="https://api.mercadopago.com";public int WebhookTimestampToleranceSeconds{get;set;}=300;}

public sealed class MercadoPagoPaymentGateway(HttpClient http,IOptions<MercadoPagoOptions> options):IPaymentGateway
{
    private readonly MercadoPagoOptions config=options.Value;public GatewayProvider Provider=>GatewayProvider.MercadoPago;
    public async Task<GatewayCheckoutResult>CreateCheckoutAsync(GatewayCheckoutRequest request,CancellationToken ct)
    {
        EnsureConfigured();var method=request.PaymentMethod switch{PaymentMethod.Pix=>"pix",PaymentMethod.Boleto=>"bolbradesco",PaymentMethod.CreditCard when !string.IsNullOrWhiteSpace(request.CardPaymentMethodId)=>request.CardPaymentMethodId,_=>throw new PaymentValidationException("Card token and payment method are required for credit card checkout.")};
        if(request.PaymentMethod==PaymentMethod.CreditCard&&string.IsNullOrWhiteSpace(request.PaymentToken))throw new PaymentValidationException("A gateway card token is required.");
        var payload=new Dictionary<string,object?>{{"transaction_amount",request.Amount},{"description",$"Nexora invoice {request.InvoiceId}"},{"external_reference",request.InvoiceId.ToString()},{"payment_method_id",method},{"payer",new{email=request.PayerEmail}}};if(request.PaymentMethod==PaymentMethod.CreditCard)payload["token"]=request.PaymentToken;
        using var message=new HttpRequestMessage(HttpMethod.Post,"/v1/payments"){Content=JsonContent.Create(payload)};message.Headers.Add("X-Idempotency-Key",request.IdempotencyKey);using var response=await SendAsync(message,ct);var json=await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken:ct);return new GatewayCheckoutResult(GetString(json,"id"),MapStatus(GetString(json,"status")),Find(json,"point_of_interaction","transaction_data","ticket_url"),Find(json,"point_of_interaction","transaction_data","qr_code"),GetDate(json,"date_last_updated"));
    }
    public async Task<GatewayPaymentResult>GetPaymentAsync(string id,CancellationToken ct){EnsureConfigured();using var response=await SendAsync(new HttpRequestMessage(HttpMethod.Get,$"/v1/payments/{Uri.EscapeDataString(id)}"),ct);var json=await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken:ct);return new GatewayPaymentResult(GetString(json,"id"),MapStatus(GetString(json,"status")),json.GetProperty("transaction_amount").GetDecimal(),GetString(json,"currency_id"),MapMethod(GetString(json,"payment_type_id")),GetDate(json,"date_last_updated"));}
    private async Task<HttpResponseMessage>SendAsync(HttpRequestMessage original,CancellationToken ct){for(var attempt=0;attempt<3;attempt++){using var request=await Clone(original,ct);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",config.AccessToken);var response=await http.SendAsync(request,ct);if(response.StatusCode!=(HttpStatusCode)429&&(int)response.StatusCode<500){if(!response.IsSuccessStatusCode)throw new PaymentValidationException("The payment gateway rejected the request.");return response;}response.Dispose();if(attempt<2)await Task.Delay(TimeSpan.FromMilliseconds(100*(1<<attempt)),ct);}throw new PaymentGatewayUnavailableException("The payment gateway is temporarily unavailable.");}
    private static async Task<HttpRequestMessage>Clone(HttpRequestMessage source,CancellationToken ct){var x=new HttpRequestMessage(source.Method,source.RequestUri);foreach(var h in source.Headers)x.Headers.TryAddWithoutValidation(h.Key,h.Value);if(source.Content is not null){var bytes=await source.Content.ReadAsByteArrayAsync(ct);x.Content=new ByteArrayContent(bytes);foreach(var h in source.Content.Headers)x.Content.Headers.TryAddWithoutValidation(h.Key,h.Value);}return x;}
    private void EnsureConfigured(){if(string.IsNullOrWhiteSpace(config.AccessToken))throw new PaymentGatewayUnavailableException("Mercado Pago sandbox credentials are not configured.");}
    private static string GetString(JsonElement x,string p)=>x.TryGetProperty(p,out var v)?v.ToString():string.Empty;private static string? Find(JsonElement x,params string[] path){foreach(var p in path){if(!x.TryGetProperty(p,out x))return null;}return x.ValueKind==JsonValueKind.String?x.GetString():null;}private static DateTimeOffset GetDate(JsonElement x,string p)=>DateTimeOffset.TryParse(GetString(x,p),CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var d)?d:DateTimeOffset.UtcNow;
    private static BillingPaymentStatus MapStatus(string x)=>x switch{"approved"=>BillingPaymentStatus.Approved,"rejected"=>BillingPaymentStatus.Rejected,"cancelled" or "canceled"=>BillingPaymentStatus.Canceled,"refunded"=>BillingPaymentStatus.Refunded,_=>BillingPaymentStatus.Pending};private static PaymentMethod MapMethod(string x)=>x switch{"credit_card"=>PaymentMethod.CreditCard,"ticket"=>PaymentMethod.Boleto,_=>PaymentMethod.Pix};
}

public sealed class MercadoPagoWebhookSignatureValidator(IOptions<MercadoPagoOptions> options,TimeProvider clock):IWebhookSignatureValidator
{
    public bool Validate(string signature,string requestId,string dataId){var config=options.Value;var secret=config.WebhookSecret;if(string.IsNullOrWhiteSpace(secret)||string.IsNullOrWhiteSpace(signature)||string.IsNullOrWhiteSpace(requestId)||string.IsNullOrWhiteSpace(dataId)||config.WebhookTimestampToleranceSeconds<=0)return false;var parts=signature.Split(',').Select(x=>x.Split('=',2)).Where(x=>x.Length==2).ToDictionary(x=>x[0],x=>x[1],StringComparer.OrdinalIgnoreCase);if(!parts.TryGetValue("ts",out var ts)||!parts.TryGetValue("v1",out var expected)||!long.TryParse(ts,NumberStyles.None,CultureInfo.InvariantCulture,out var seconds))return false;DateTimeOffset signedAt;try{signedAt=DateTimeOffset.FromUnixTimeSeconds(seconds);}catch(ArgumentOutOfRangeException){return false;}if((clock.GetUtcNow()-signedAt).Duration()>TimeSpan.FromSeconds(config.WebhookTimestampToleranceSeconds))return false;var manifest=$"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{ts};";var actual=Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual),Encoding.ASCII.GetBytes(expected.ToLowerInvariant()));}
}
