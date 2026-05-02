using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CarHub.Models;
using Microsoft.Extensions.Options;

namespace CarHub.Services;

public sealed class StripeCheckoutService
{
    private readonly HttpClient _httpClient;
    private readonly StripeSettings _stripeSettings;

    public StripeCheckoutService(HttpClient httpClient, IOptions<StripeSettings> stripeSettings)
    {
        _httpClient = httpClient;
        _stripeSettings = stripeSettings.Value;
    }

    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_stripeSettings.SecretKey)
            && !_stripeSettings.SecretKey.Contains("your_secret_key_here", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> CreateCheckoutUrlAsync(string successUrl, string cancelUrl, int quantity, string? customerEmail = null, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Cart is empty.");
        }

        if (!IsConfigured())
        {
            throw new InvalidOperationException("Stripe is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/checkout/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _stripeSettings.SecretKey);

        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", "payment"),
            new("success_url", successUrl),
            new("cancel_url", cancelUrl),
            new("line_items[0][price_data][currency]", "aud"),
            new("line_items[0][price_data][product_data][name]", "CarHub"),
            new("line_items[0][price_data][product_data][description]", "CarHub car cart checkout"),
            new("line_items[0][price_data][unit_amount]", "2000"),
            new("line_items[0][quantity]", quantity.ToString(CultureInfo.InvariantCulture))
        };

        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            form.Add(new KeyValuePair<string, string>("customer_email", customerEmail));
        }

        request.Content = new FormUrlEncodedContent(form);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Stripe Checkout session could not be created: {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        if (document.RootElement.TryGetProperty("url", out var urlElement))
        {
            var url = urlElement.GetString();
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }

        throw new InvalidOperationException("Stripe did not return a checkout URL.");
    }
}
