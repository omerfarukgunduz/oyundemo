namespace IfsaKlasik.Web.Models.Entities;

public class RoomMember
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public string Nickname { get; set; } = string.Empty;
    public bool IsHost { get; set; }
    public string? SignalRConnectionId { get; set; }
    /// <summary>Presence flag updated on hub connect/disconnect.</summary>
    public bool IsConnected { get; set; }

    public ICollection<RoundAnswer> Answers { get; set; } = new List<RoundAnswer>();
}
