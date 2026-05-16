using System.ComponentModel.DataAnnotations;
using IfsaKlasik.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IfsaKlasik.Web.Areas.Admin.Controllers;

[Area("Admin")]
public sealed class LoginController : Controller
{
    private readonly SignInManager<ApplicationUser> _signIn;

    public LoginController(SignInManager<ApplicationUser> signIn)
    {
        _signIn = signIn;
    }

    [AllowAnonymous]
    public IActionResult Index(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home", new { area = "Admin" });

        return View(new AdminLoginVm { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index([FromForm] AdminLoginVm model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _signIn.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Giriş başarısız. E-posta veya şifreyi kontrol edin.");
            return View(model);
        }

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home", new { area = "Admin" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Index));
    }
}

public sealed class AdminLoginVm
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
