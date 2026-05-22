using IfsaKlasik.Web.Models;
using IfsaKlasik.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IfsaKlasik.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = IfsaRoles.Admin)]
public sealed class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(LoginController.Index), "Login");
        }

        MoveTempDataStatusesToViewBag();
        var page = new AdminManageAccountPageVm
        {
            CurrentEmail = user.Email ?? string.Empty,
        };
        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeEmail(
        [Bind(Prefix = nameof(AdminManageAccountPageVm.ChangeEmailForm))] AdminChangeEmailVm model)
    {
        MoveTempDataStatusesToViewBag();

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(LoginController.Index), "Login");
        }

        if (!ModelState.IsValid)
        {
            return await AccountIndexWithErrors(user, model);
        }

        var newEmail = model.NewEmail.Trim();
        if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            TempData["StatusInfo"] = "E-posta zaten güncel.";
            return RedirectToAction(nameof(Index));
        }

        if (!await _userManager.CheckPasswordAsync(user, model.CurrentPassword))
        {
            ModelState.AddModelError($"{nameof(AdminManageAccountPageVm.ChangeEmailForm)}.{nameof(AdminChangeEmailVm.CurrentPassword)}",
                "Mevcut şifre yanlış.");
            return await AccountIndexWithErrors(user, model);
        }

        var occupied = await _userManager.FindByEmailAsync(newEmail);
        if (occupied is not null && occupied.Id != user.Id)
        {
            ModelState.AddModelError($"{nameof(AdminManageAccountPageVm.ChangeEmailForm)}.{nameof(AdminChangeEmailVm.NewEmail)}",
                "Bu e-posta başka bir hesapta kayıtlı.");
            return await AccountIndexWithErrors(user, model);
        }

        var token = await _userManager.GenerateChangeEmailTokenAsync(user, newEmail);
        var mailResult = await _userManager.ChangeEmailAsync(user, newEmail, token);
        if (!mailResult.Succeeded)
        {
            foreach (var err in mailResult.Errors)
            {
                ModelState.AddModelError($"{nameof(AdminManageAccountPageVm.ChangeEmailForm)}.{nameof(AdminChangeEmailVm.NewEmail)}",
                    string.IsNullOrWhiteSpace(err.Description) ? err.Code : err.Description);
            }

            return await AccountIndexWithErrors(user, model);
        }

        var nameResult = await _userManager.SetUserNameAsync(user, newEmail);
        if (!nameResult.Succeeded)
        {
            foreach (var err in nameResult.Errors)
            {
                ModelState.AddModelError($"{nameof(AdminManageAccountPageVm.ChangeEmailForm)}.{nameof(AdminChangeEmailVm.NewEmail)}",
                    string.IsNullOrWhiteSpace(err.Description) ? err.Code : err.Description);
            }

            return await AccountIndexWithErrors(user, model);
        }

        user = await _userManager.FindByIdAsync(user.Id)
               ?? await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(LoginController.Index), "Login");
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["StatusOk"] = "E-posta ve giriş adı güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        [Bind(Prefix = nameof(AdminManageAccountPageVm.ChangePasswordForm))]
        AdminChangePasswordVm model)
    {
        MoveTempDataStatusesToViewBag();

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(LoginController.Index), "Login");
        }

        if (!ModelState.IsValid)
        {
            return await AccountIndexPasswordErrors(user, model);
        }

        var result = await _userManager.ChangePasswordAsync(user,
            model.CurrentPassword,
            model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
            {
                var msg = string.IsNullOrWhiteSpace(err.Description) ? err.Code : err.Description;
                if (err.Code == "PasswordMismatch")
                {
                    ModelState.AddModelError($"{nameof(AdminManageAccountPageVm.ChangePasswordForm)}.{nameof(AdminChangePasswordVm.CurrentPassword)}",
                        "Mevcut şifre yanlış.");
                }
                else
                {
                    ModelState.AddModelError($"{nameof(AdminManageAccountPageVm.ChangePasswordForm)}.{nameof(AdminChangePasswordVm.NewPassword)}",
                        msg);
                }
            }

            return await AccountIndexPasswordErrors(user, model);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["StatusOk"] = "Şifre güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    private void MoveTempDataStatusesToViewBag()
    {
        if (TempData.TryGetValue("StatusOk", out var ok))
        {
            ViewBag.StatusOk = ok;
        }

        if (TempData.TryGetValue("StatusInfo", out var info))
        {
            ViewBag.StatusInfo = info;
        }
    }

    private async Task<IActionResult> AccountIndexWithErrors(ApplicationUser dbUser,
        AdminChangeEmailVm attemptedEmailModel)
    {
        var refreshed = await _userManager.FindByIdAsync(dbUser.Id) ?? dbUser;

        MoveTempDataStatusesToViewBag();
        var page = new AdminManageAccountPageVm
        {
            CurrentEmail = refreshed.Email ?? string.Empty,
            ChangeEmailForm = attemptedEmailModel,
            ChangePasswordForm = new AdminChangePasswordVm(),
        };

        return View("Index", page);
    }

    private Task<IActionResult> AccountIndexPasswordErrors(ApplicationUser dbUser,
        AdminChangePasswordVm attemptedPasswordModel)
    {
        MoveTempDataStatusesToViewBag();
        var page = new AdminManageAccountPageVm
        {
            CurrentEmail = dbUser.Email ?? string.Empty,
            ChangeEmailForm = new AdminChangeEmailVm(),
            ChangePasswordForm = attemptedPasswordModel,
        };

        return Task.FromResult<IActionResult>(View("Index", page));
    }
}
