namespace IfsaKlasik.Web.Models.Entities;

public class RoundAnswer
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public Round Round { get; set; } = null!;
    public int RoomMemberId { get; set; }
    public RoomMember RoomMember { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
}
