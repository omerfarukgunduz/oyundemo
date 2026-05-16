namespace IfsaKlasik.Web.Models.Entities;

public class QuestionPackage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
