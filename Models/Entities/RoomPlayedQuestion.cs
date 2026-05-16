namespace IfsaKlasik.Web.Models.Entities;

public class RoomPlayedQuestion
{
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
}
