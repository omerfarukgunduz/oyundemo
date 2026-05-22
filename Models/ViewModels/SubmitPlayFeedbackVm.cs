using System.ComponentModel.DataAnnotations;

namespace IfsaKlasik.Web.Models.ViewModels;

public sealed class SubmitPlayFeedbackVm
{
    [Required]
    public string RoomCode { get; set; } = string.Empty;

    [Required]
    public Guid MemberGuid { get; set; }

    [Required(ErrorMessage = "Lütfen kısa bir yanıt yazın.")]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;
}
