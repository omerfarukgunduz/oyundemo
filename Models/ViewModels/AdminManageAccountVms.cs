using System.ComponentModel.DataAnnotations;

namespace IfsaKlasik.Web.Models.ViewModels;

public sealed class AdminManageAccountPageVm
{
    public string CurrentEmail { get; set; } = string.Empty;

    public AdminChangeEmailVm ChangeEmailForm { get; set; } = new();

    public AdminChangePasswordVm ChangePasswordForm { get; set; } = new();
}

public sealed class AdminChangeEmailVm
{
    [Required(ErrorMessage = "Yeni e-posta gerekli.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [StringLength(256)]
    [Display(Name = "Yeni e-posta")]
    public string NewEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mevcut şifre gerekli.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut şifre")]
    public string CurrentPassword { get; set; } = string.Empty;
}

public sealed class AdminChangePasswordVm
{
    [Required(ErrorMessage = "Mevcut şifre gerekli.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut şifre")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni şifre gerekli.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalı.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni şifre")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni şifre tekrarı gerekli.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni şifre (tekrar)")]
    [Compare(nameof(NewPassword), ErrorMessage = "Yeni şifreler aynı değil.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
