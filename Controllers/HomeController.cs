using CarHub.Models;
using CarHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CarHub.Controllers
{
    public class HomeController : Controller
    {
        private readonly CarCatalogService _catalog;
        private readonly LocalAccountService _accounts;
        private readonly StripeCheckoutService _stripeCheckout;
        private readonly IConfiguration _configuration;

        public HomeController(
            CarCatalogService catalog,
            LocalAccountService accounts,
            StripeCheckoutService stripeCheckout,
            IConfiguration configuration)
        {
            _catalog = catalog;
            _accounts = accounts;
            _stripeCheckout = stripeCheckout;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "Home";
            return View(_catalog.GetFeaturedCars());
        }

        public IActionResult Cars()
        {
            ViewData["Title"] = "Showcase";
            return View(_catalog.GetAllCars());
        }

        public IActionResult Car(int id = 1)
        {
            var car = _catalog.FindById(id) ?? _catalog.GetFeaturedCars().First();
            ViewData["Title"] = car.DisplayName;
            return View(car);
        }

        public IActionResult CarDetails(int id)
        {
            return Car(id);
        }

        public IActionResult Cart()
        {
            ViewData["Title"] = "Cart";
            ViewData["StripeReady"] = _stripeCheckout.IsConfigured();
            return View();
        }

        public IActionResult Car3D()
        {
            ViewData["Title"] = "3D Car Viewer";
            return View();
        }

        public IActionResult Credits()
        {
            ViewData["Title"] = "Credits";
            return View();
        }

        [HttpGet]
        public IActionResult Account()
        {
            var model = BuildAccountViewModel();
            if (TempData.TryGetValue("AccountMessage", out var message) && message is string text)
            {
                model.Message = text;
                model.MessageType = (TempData["AccountMessageType"] as string) ?? "info";
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Account(AccountViewModel model)
        {
            var action = (model.FormAction ?? "signup").Trim().ToLowerInvariant();
            var result = action switch
            {
                "signup" => await SignUp(model),
                "signin" => await SignIn(model),
                "forgot" => RequestReset(model),
                "reset" => ResetPassword(model),
                "profile" => UpdateProfile(model),
                "twofactor" => ToggleTwoFactor(model),
                _ => Placeholder("Choose a sign-up, sign-in, or reset action.")
            };

            if (result.IsSuccess && (action == "profile" || action == "twofactor"))
            {
                await SignInAsync(result);
            }

            ApplyResultToModel(model, result);
            ViewData["Title"] = "Account";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Account));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ExternalLogin(string provider, string returnUrl = "/Home/Account")
        {
            var normalizedProvider = provider?.Trim().ToLowerInvariant();
            var scheme = normalizedProvider switch
            {
                "google" => "Google",
                "github" => "GitHub",
                _ => null
            };

            if (scheme is null || !IsOAuthConfigured(normalizedProvider))
            {
                TempData["AccountMessageType"] = "error";
                TempData["AccountMessage"] = normalizedProvider switch
                {
                    "google" => "Google login is missing its client ID or secret.",
                    "github" => "GitHub login is missing its client ID or secret.",
                    _ => "Unknown login provider."
                };
                return RedirectToAction(nameof(Account));
            }

            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(ExternalLoginCallback), "Home", new { returnUrl, provider = normalizedProvider }) ?? "/Home/Account"
            };

            return Challenge(properties, scheme);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string provider, string returnUrl = "/Home/Account")
        {
            var external = await HttpContext.AuthenticateAsync("External");
            if (!external.Succeeded || external.Principal is null)
            {
                TempData["AccountMessageType"] = "error";
                TempData["AccountMessage"] = "The social login handshake did not complete.";
                return RedirectToAction(nameof(Account));
            }

            var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
            var providerKey = external.Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var email = external.Principal.FindFirstValue(ClaimTypes.Email);
            var displayName = external.Principal.FindFirstValue(ClaimTypes.Name)
                ?? external.Principal.FindFirstValue("urn:github:login")
                ?? external.Principal.FindFirstValue(ClaimTypes.GivenName)
                ?? email;
            var pictureUrl = external.Principal.FindFirstValue("urn:google:picture")
                ?? external.Principal.FindFirstValue("urn:github:avatar");

            if (string.IsNullOrWhiteSpace(email))
            {
                var loginName = external.Principal.FindFirstValue("urn:github:login")
                    ?? external.Principal.FindFirstValue(ClaimTypes.Name)
                    ?? external.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
                email = $"{loginName}@social.carhub.local";
            }

            await HttpContext.SignOutAsync("External");
            var result = _accounts.UpsertExternalAccount(normalizedProvider, providerKey, email, displayName, displayName, pictureUrl);
            if (!result.IsSuccess)
            {
                TempData["AccountMessageType"] = "error";
                TempData["AccountMessage"] = result.Message;
                return RedirectToAction(nameof(Account));
            }

            await SignInAsync(result);

            TempData["AccountMessageType"] = "success";
            TempData["AccountMessage"] = $"Signed in with {result.Email}.";

            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Account));
        }

        public IActionResult Privacy()
        {
            ViewData["Title"] = "Privacy Policy";
            return View();
        }

        public IActionResult Terms()
        {
            ViewData["Title"] = "Terms of Use";
            return View();
        }

        public IActionResult Eula()
        {
            ViewData["Title"] = "EULA";
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CartCheckoutRequest request, CancellationToken cancellationToken)
        {
            if (!_stripeCheckout.IsConfigured())
            {
                return BadRequest(new { error = "Stripe is not configured." });
            }

            var quantity = Math.Max(request.Quantity, 0);
            if (quantity <= 0)
            {
                return BadRequest(new { error = "Cart is empty." });
            }

            var customerEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var successUrl = $"{baseUrl}/Home/Cart?checkout=success";
            var cancelUrl = $"{baseUrl}/Home/Cart?checkout=cancel";

            try
            {
                var url = await _stripeCheckout.CreateCheckoutUrlAsync(successUrl, cancelUrl, quantity, customerEmail, cancellationToken);
                return Ok(new { url });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(int quantity, CancellationToken cancellationToken)
        {
            if (!_stripeCheckout.IsConfigured())
            {
                TempData["CheckoutError"] = "Stripe is not configured yet.";
                return RedirectToAction(nameof(Cart));
            }

            if (quantity <= 0)
            {
                TempData["CheckoutError"] = "Your cart is empty.";
                return RedirectToAction(nameof(Cart));
            }

            var customerEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var successUrl = $"{baseUrl}/Home/Cart?checkout=success";
            var cancelUrl = $"{baseUrl}/Home/Cart?checkout=cancel";

            try
            {
                var url = await _stripeCheckout.CreateCheckoutUrlAsync(successUrl, cancelUrl, quantity, customerEmail, cancellationToken);
                return Redirect(url);
            }
            catch (Exception ex)
            {
                TempData["CheckoutError"] = ex.Message;
                return RedirectToAction(nameof(Cart));
            }
        }

        [HttpGet]
        public IActionResult Error(int statusCode = 404)
        {
            // Set the response status so the browser knows this is an error
            Response.StatusCode = statusCode;

            ViewData["StatusCode"] = statusCode;
            ViewData["Title"] = statusCode switch
            {
                400 => "Bad request",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "Page not found",
                500 => "Server error",
                _ => "Error"
            };

            ViewData["Message"] = statusCode switch
            {
                400 => "The request could not be understood.",
                401 => "You need to sign in to access this page.",
                403 => "Access to that resource is blocked for security reasons.",
                404 => "We could not find the page you requested.",
                500 => "CarHub hit a server error. Please try again.",
                _ => "Something went wrong."
            };

            return View();
        }

        private AccountViewModel BuildAccountViewModel()
        {
            var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            var profile = !string.IsNullOrWhiteSpace(email) ? _accounts.GetProfile(email) : null;

            return new AccountViewModel
            {
                IsAuthenticated = User.Identity?.IsAuthenticated == true,
                SignedInEmail = profile?.Email ?? email,
                UserName = profile?.UserName ?? User.FindFirstValue(ClaimTypes.Name),
                DisplayName = profile?.DisplayName ?? User.FindFirstValue(ClaimTypes.Name),
                SignedInProvider = profile?.Provider ?? User.FindFirstValue("urn:carhub:provider"),
                TwoFactorEnabled = profile?.TwoFactorEnabled ?? false,
                PasskeyCount = profile?.PasskeyCount ?? 0
            };
        }

        private static LocalAccountService.AuthResult Placeholder(string message)
        {
            return LocalAccountService.AuthResult.Fail(message);
        }

        private bool IsOAuthConfigured(string? provider)
        {
            var sectionName = provider switch
            {
                "google" => "Google",
                "github" => "GitHub",
                _ => null
            };

            if (sectionName is null)
            {
                return false;
            }

            var clientId = _configuration[$"Authentication:{sectionName}:ClientId"];
            var clientSecret = _configuration[$"Authentication:{sectionName}:ClientSecret"];
            return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
        }

        private async Task<LocalAccountService.AuthResult> SignIn(AccountViewModel model)
        {
            var result = _accounts.ValidateLogin(model.Email, model.Password);
            if (!result.IsSuccess)
            {
                return result;
            }

            await SignInAsync(result);
            return result;
        }

        private async Task<LocalAccountService.AuthResult> SignUp(AccountViewModel model)
        {
            var result = _accounts.Register(model.Email, model.Password, model.ConfirmPassword, model.DisplayName, model.AcceptTerms);
            if (!result.IsSuccess)
            {
                return result;
            }

            await SignInAsync(result);
            return result;
        }

        private LocalAccountService.AuthResult RequestReset(AccountViewModel model)
        {
            return _accounts.RequestResetCode(model.Email);
        }

        private LocalAccountService.AuthResult ResetPassword(AccountViewModel model)
        {
            return _accounts.ResetPassword(model.Email, model.ResetCode, model.NewPassword, model.ConfirmPassword);
        }

        private LocalAccountService.AuthResult UpdateProfile(AccountViewModel model)
        {
            return _accounts.UpdateProfile(model.Email, model.DisplayName);
        }

        private LocalAccountService.AuthResult ToggleTwoFactor(AccountViewModel model)
        {
            return _accounts.ToggleTwoFactor(model.Email, model.TwoFactorEnabled);
        }

        private async Task SignInAsync(LocalAccountService.AuthResult result)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, result.UserName),
                new(ClaimTypes.Email, result.Email),
                new("urn:carhub:username", result.UserName),
                new("urn:carhub:provider", result.Provider),
                new(ClaimTypes.GivenName, result.DisplayName)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
                });
        }

        private void ApplyResultToModel(AccountViewModel model, LocalAccountService.AuthResult result)
        {
            model.Message = result.Message;
            model.MessageType = result.IsSuccess ? "success" : "error";
            model.IsAuthenticated = User.Identity?.IsAuthenticated == true || result.IsSuccess;
            model.SignedInEmail = result.IsSuccess ? result.Email : model.SignedInEmail;
            model.UserName = result.IsSuccess ? result.UserName : model.UserName;
            model.DisplayName = result.IsSuccess ? result.DisplayName : model.DisplayName;
            model.SignedInProvider = result.IsSuccess ? result.Provider : model.SignedInProvider;
            model.TwoFactorEnabled = result.IsSuccess ? result.TwoFactorEnabled : model.TwoFactorEnabled;
            model.PasskeyCount = result.IsSuccess ? result.PasskeyCount : model.PasskeyCount;
            model.ResetSender = result.Sender;
            model.ResetExpiresAt = result.ExpiresAt;
            model.GeneratedResetCode = result.ResetCode;
            model.Password = string.Empty;
            model.ConfirmPassword = string.Empty;
            model.NewPassword = string.Empty;
        }
    }
}
