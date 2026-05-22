namespace IfsaKlasik.Web.Models.Entities;

/// <summary>Masada oyun bitiminde oyuncudan alınan anonim geliştirici geri bildirimi.</summary>
public class PlayFeedback
{
    public int Id { get; set; }

    /// <summary>Normalize oda kodu (büyük harf).</summary>
    public string RoomCode { get; set; } = string.Empty;

    public Guid MemberPublicId { get; set; }

    public string Nickname { get; set; } = string.Empty;

    /// <summary>Tek kutuda oyuncunun serbest geri bildirim metni (keyif / geliştiriciye ileti).</summary>
    public string DeveloperMessage { get; set; } = string.Empty;

    public DateTime SubmittedAtUtc { get; set; }
}
