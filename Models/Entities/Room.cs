namespace IfsaKlasik.Web.Models.Entities;

public class Room
{
    public int Id { get; set; }
    /// <summary>Uppercase alphanumeric room code shown to players.</summary>
    public string Code { get; set; } = string.Empty;
    public int? SelectedPackageId { get; set; }
    public QuestionPackage? SelectedPackage { get; set; }
    public int? HostMemberId { get; set; }
    public RoomMember? HostMember { get; set; }
    public RoomPhase Phase { get; set; }
    public int? CurrentRoundId { get; set; }
    public Round? CurrentRound { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Utc when first play round began (tur süresi / özet süresi için).</summary>
    public DateTime? GameStartedAtUtc { get; set; }
    /// <summary>Round answer timer seconds. 0 = disabled.</summary>
    public int RoundTimerSeconds { get; set; }

    public ICollection<RoomMember> Members { get; set; } = new List<RoomMember>();
    public ICollection<Round> Rounds { get; set; } = new List<Round>();
    public ICollection<RoomPlayedQuestion> PlayedQuestions { get; set; } = new List<RoomPlayedQuestion>();
}
