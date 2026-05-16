namespace IfsaKlasik.Web.Models.Entities;

public class Round
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    /// <summary>JSON array of answer texts shuffled order (anonymous payloads only).</summary>
    public string? ShuffledAnswersJson { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }

    public ICollection<RoundAnswer> Answers { get; set; } = new List<RoundAnswer>();
}
