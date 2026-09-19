using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AtiatHire.Services;
using AtiatHire.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AtiatHire.Controllers;

/// <summary>
/// Staff sign-in for the prototype: credentials come from configuration.
/// For production, replace with ASP.NET Core Identity (or your SSO provider) and hashed passwords.
/// </summary>
public class AccountController : Controller
{
    private readonly StaffOptions _staff;

    public AccountController(IOptions<StaffOptions> staff) => _staff = staff.Value;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) =>
        View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [EnableRateLimiting("forms")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = _staff.Users.FirstOrDefault(u =>
            string.Equals(u.Username, model.Username, StringComparison.OrdinalIgnoreCase));

        if (user is null || !FixedTimeEquals(user.Password, model.Password))
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Username),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Role, "Staff")
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
