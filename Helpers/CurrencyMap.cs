namespace CarHub.Helpers;

public static class CurrencyMap
{
    public static string FromCountry(string country)
    {
        return country switch
        {
            "US" => "USD",
            "GB" => "GBP",
            "DE" => "EUR",
            "FR" => "EUR",
            "JP" => "JPY",
            "AU" => "AUD",
            _ => "AUD"
        };
    }
}