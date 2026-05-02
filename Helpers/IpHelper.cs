namespace CarHub.Helpers;

public static class IpHelper
{
    public static string GetClientIp(HttpContext context)
    {
        var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        if (!string.IsNullOrEmpty(ip))
            return ip.Split(',')[0];

        return context.Connection.RemoteIpAddress?.ToString();
    }
}