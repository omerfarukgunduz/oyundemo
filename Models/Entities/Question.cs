namespace IfsaKlasik.Web.Models.Entities;

public class Question
{
    public int Id { get; set; }
    public int PackageId { get; set; }
    public string Text { get; set; } = string.Empty;

    public QuestionPackage Package { get; set; } = null!;
}
