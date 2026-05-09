using CarHub.Models;
using CarHub.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Net.Http.Headers;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// =========================
// 🔧 SERVICES
// =========================

builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<CarCatalogService>();
builder.Services.AddSingleton<LocalAccountService>();
builder.Services.AddHttpClient<StripeCheckoutService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie("External", options =>
    {
        options.Cookie.Name = "CarHub.External";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        options.SlidingExpiration = false;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/Account";
        options.AccessDeniedPath = "/Home/Error?statusCode=403";
        options.Cookie.Name = "CarHub.Auth";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthentication()
    .AddOAuth("Google", options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
        options.CallbackPath = "/signin-google";
        options.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        options.TokenEndpoint = "https://oauth2.googleapis.com/token";
        options.UserInformationEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";
        options.SignInScheme = "External";
        options.SaveTokens = true;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.UserAgent.ParseAdd("CarHub");

                using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();

                using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
                var root = payload.RootElement;
                if (root.TryGetProperty("id", out var googleId))
                {
                    context.Identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, googleId.GetString() ?? string.Empty));
                }
                if (root.TryGetProperty("name", out var googleName))
                {
                    context.Identity?.AddClaim(new Claim(ClaimTypes.Name, googleName.GetString() ?? string.Empty));
                }
                if (root.TryGetProperty("email", out var googleEmail))
                {
                    context.Identity?.AddClaim(new Claim(ClaimTypes.Email, googleEmail.GetString() ?? string.Empty));
                }
                if (root.TryGetProperty("picture", out var googlePicture))
                {
                    context.Identity?.AddClaim(new Claim("urn:google:picture", googlePicture.GetString() ?? string.Empty));
                }
            }
        };
    })
    .AddOAuth("GitHub", options =>
    {
        options.ClientId = builder.Configuration["Authentication:GitHub:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"] ?? string.Empty;
        options.CallbackPath = "/signin-github";
        options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
        options.TokenEndpoint = "https://github.com/login/oauth/access_token";
        options.UserInformationEndpoint = "https://api.github.com/user";
        options.SignInScheme = "External";
        options.SaveTokens = true;
        options.Scope.Add("read:user");
        options.Scope.Add("user:email");
        options.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.UserAgent.ParseAdd("CarHub");

                using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();

                using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
                var root = payload.RootElement;
                if (root.TryGetProperty("id", out var githubId))
                {
                    context.Identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, githubId.GetRawText().Trim('"')));
                }
                if (root.TryGetProperty("name", out var githubName))
                {
                    context.Identity?.AddClaim(new Claim(ClaimTypes.Name, githubName.GetString() ?? string.Empty));
                }
                if (root.TryGetProperty("login", out var githubLogin))
                {
                    context.Identity?.AddClaim(new Claim("urn:github:login", githubLogin.GetString() ?? string.Empty));
                }
                if (root.TryGetProperty("avatar_url", out var githubAvatar))
                {
                    context.Identity?.AddClaim(new Claim("urn:github:avatar", githubAvatar.GetString() ?? string.Empty));
                }
                if (root.TryGetProperty("email", out var githubEmail) && !string.IsNullOrWhiteSpace(githubEmail.GetString()))
                {
                    context.Identity?.AddClaim(new Claim(ClaimTypes.Email, githubEmail.GetString() ?? string.Empty));
                }

                var email = root.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(email))
                {
                    using var emailsRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
                    emailsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                    emailsRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    emailsRequest.Headers.UserAgent.ParseAdd("CarHub");

                    using var emailsResponse = await context.Backchannel.SendAsync(emailsRequest, context.HttpContext.RequestAborted);
                    if (emailsResponse.IsSuccessStatusCode)
                    {
                        using var emailsPayload = JsonDocument.Parse(await emailsResponse.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
                        string? verifiedEmail = null;
                        foreach (var item in emailsPayload.RootElement.EnumerateArray())
                        {
                            var isPrimary = item.TryGetProperty("primary", out var primary) && primary.GetBoolean();
                            var isVerified = item.TryGetProperty("verified", out var verified) && verified.GetBoolean();
                            if (isPrimary && isVerified && item.TryGetProperty("email", out var verifiedEmailElement))
                            {
                                verifiedEmail = verifiedEmailElement.GetString();
                                break;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(verifiedEmail))
                        {
                            context.Identity?.AddClaim(new Claim(ClaimTypes.Email, verifiedEmail));
                        }
                    }
                }
            }
        };
    });

builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));

// =========================
// 🚀 BUILD APP
// =========================

var app = builder.Build();

// =========================
// 🌐 MIDDLEWARE PIPELINE
// =========================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error?statusCode=500");
    app.UseHsts();
}

app.UseHttpsRedirection();

// 1. Important: Use StatusCodePages FIRST so it can intercept the 403 we throw below
app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseStaticFiles();
app.UseRouting();

// 2. Custom check for sensitive files
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLowerInvariant();
    if (!string.IsNullOrWhiteSpace(path) && (path.Contains("appsettings.json")))
    {
        // We set the code, and StatusCodePagesWithReExecute will handle the redirect
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return; 
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// =========================
// 🚗 ROUTES
// =========================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapControllerRoute(
    name: "car-details",
    pattern: "Cars/{brand}/{model}",
    defaults: new { controller = "Cars", action = "Details" }
);

app.Run();