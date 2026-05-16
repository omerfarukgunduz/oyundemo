using System.ComponentModel.DataAnnotations;

namespace IfsaKlasik.Web.Models.ViewModels;

public sealed class JoinVm
{
    [Required]
    [StringLength(16, MinimumLength = 4)]
    [Display(Name = "Oda kodu")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(32, MinimumLength = 2)]
    [Display(Name = "Takma ad")]
    public string Nickname { get; set; } = "Misafir";
}

public sealed record PackageVm(int Id, string Name);

public sealed record LobbyPageVm(
    string RoomCode,
    Guid MemberGuid,
    bool IsHost,
    string InviteUrl,
    IReadOnlyList<PackageVm> Packages,
    int TimerSecondsConfigured,
    int? SelectedPackageId,
    string Nickname);

public sealed record RoomPlayPageVm(string RoomCode, Guid MemberGuid, bool IsHost, string InviteUrl, string Nickname);
