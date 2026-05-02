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
                _ => Placeholder("Choose a sign-up, sign-in, or reset action.")
            };

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
                RedirectUri = Url.Action(nameof(ExternalLoginCallback), "Home", new { returnUrl }) ?? "/Home/Account"
            };

            return Challenge(properties, scheme);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = "/Home/Account")
        {
            var external = await HttpContext.AuthenticateAsync("External");
            if (!external.Succeeded || external.Principal is null)
            {
                TempData["AccountMessageType"] = "error";
                TempData["AccountMessage"] = "The social login handshake did not complete.";
                return RedirectToAction(nameof(Account));
            }

            var email = external.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                var loginName = external.Principal.FindFirstValue(ClaimTypes.Name)
                    ?? external.Principal.FindFirstValue("urn:github:login")
                    ?? external.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
                email = $"{loginName}@social.carhub.local";
            }

            await HttpContext.SignOutAsync("External");
            await SignInAsync(email);

            TempData["AccountMessageType"] = "success";
            TempData["AccountMessage"] = $"Signed in with {email}.";

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

        [HttpGet]
        public IActionResult Error(int statusCode = 404)
        {
            Response.StatusCode = statusCode;
            ViewData["Title"] = statusCode switch
            {
                400 => "Bad request",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "Page not found",
                500 => "Server error",
                _ => "Error"
            };

            ViewData["StatusCode"] = statusCode;
            ViewData["Message"] = statusCode switch
            {
                400 => "The request could not be understood.",
                401 => "You need to sign in to access this page.",
                403 => "Access to that resource is blocked.",
                404 => "We could not find the page you requested.",
                500 => "CarHub hit a server error. Please try again.",
                _ => "Something went wrong."
            };

            return View();
        }

        private AccountViewModel BuildAccountViewModel()
        {
            return new AccountViewModel
            {
                IsAuthenticated = User.Identity?.IsAuthenticated == true,
                SignedInEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name
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

            await SignInAsync(result.Email);
            return LocalAccountService.AuthResult.Ok(result.Email, result.Message);
        }

        private async Task<LocalAccountService.AuthResult> SignUp(AccountViewModel model)
        {
            var result = _accounts.Register(model.Email, model.Password, model.ConfirmPassword);
            if (!result.IsSuccess)
            {
                return result;
            }

            await SignInAsync(result.Email);
            return LocalAccountService.AuthResult.Ok(result.Email, result.Message);
        }

        private LocalAccountService.AuthResult RequestReset(AccountViewModel model)
        {
            return _accounts.RequestResetCode(model.Email);
        }

        private LocalAccountService.AuthResult ResetPassword(AccountViewModel model)
        {
            return _accounts.ResetPassword(model.Email, model.ResetCode, model.NewPassword, model.ConfirmPassword);
        }

        private async Task SignInAsync(string email)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, email),
                new(ClaimTypes.Email, email)
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
            model.SignedInEmail = result.IsSuccess ? (result.Email ?? model.SignedInEmail) : model.SignedInEmail;
            model.ResetSender = result.Sender;
            model.ResetExpiresAt = result.ExpiresAt;
            model.GeneratedResetCode = result.ResetCode;
            model.Password = string.Empty;
            model.ConfirmPassword = string.Empty;
            model.NewPassword = string.Empty;
        }
    }
}
