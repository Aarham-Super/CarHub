using System.Text;
using System.Text.Json;

namespace CarHub.Services;

public sealed class StripeWebhookService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<StripeWebhookService> _logger;

    public StripeWebhookService(IWebHostEnvironment environment, ILogger<StripeWebhookService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public Task HandleAsync(string eventType, JsonElement dataObject, string eventId, string? accountId)
    {
        var summary = eventType switch
        {
            "checkout.session.completed" => BuildCheckoutSummary(dataObject),
            "payment_intent.succeeded" => BuildPaymentIntentSummary(dataObject, "succeeded"),
            "payment_intent.payment_failed" => BuildPaymentIntentSummary(dataObject, "failed"),
            "charge.refunded" => BuildChargeSummary(dataObject, "refunded"),
            _ => $"Unhandled Stripe event: {eventType}"
        };

        _logger.LogInformation("Stripe webhook {EventId}: {Summary}", eventId, summary);
        return AppendLogAsync(eventId, eventType, accountId, summary);
    }

    private string BuildCheckoutSummary(JsonElement dataObject)
    {
        var sessionId = dataObject.TryGetProperty("id", out var id) ? id.GetString() : null;
        var paymentStatus = dataObject.TryGetProperty("payment_status", out var paymentStatusElement)
            ? paymentStatusElement.GetString()
            : null;
        var customerEmail = dataObject.TryGetProperty("customer_email", out var emailElement)
            ? emailElement.GetString()
            : null;

        return $"Checkout session completed. Session={sessionId ?? "n/a"}, PaymentStatus={paymentStatus ?? "n/a"}, Email={customerEmail ?? "n/a"}";
    }

    private string BuildPaymentIntentSummary(JsonElement dataObject, string outcome)
    {
        var intentId = dataObject.TryGetProperty("id", out var id) ? id.GetString() : null;
        var amount = dataObject.TryGetProperty("amount", out var amountElement) ? amountElement.GetInt64().ToString() : "n/a";
        var currency = dataObject.TryGetProperty("currency", out var currencyElement) ? currencyElement.GetString() : null;

        return $"Payment intent {outcome}. Intent={intentId ?? "n/a"}, Amount={amount}, Currency={currency ?? "n/a"}";
    }

    private string BuildChargeSummary(JsonElement dataObject, string outcome)
    {
        var chargeId = dataObject.TryGetProperty("id", out var id) ? id.GetString() : null;
        var amount = dataObject.TryGetProperty("amount", out var amountElement) ? amountElement.GetInt64().ToString() : "n/a";
        var currency = dataObject.TryGetProperty("currency", out var currencyElement) ? currencyElement.GetString() : null;

        return $"Charge {outcome}. Charge={chargeId ?? "n/a"}, Amount={amount}, Currency={currency ?? "n/a"}";
    }

    private async Task AppendLogAsync(string eventId, string eventType, string? accountId, string summary)
    {
        var dataDirectory = Path.Combine(_environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);

        var logPath = Path.Combine(dataDirectory, "stripe-events.log");
        var line = $"{DateTimeOffset.UtcNow:O} | {eventId} | {eventType} | {(string.IsNullOrWhiteSpace(accountId) ? "n/a" : accountId)} | {summary}";
        await File.AppendAllTextAsync(logPath, line + Environment.NewLine, Encoding.UTF8);
    }
}
