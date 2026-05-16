using IfsaKlasik.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace IfsaKlasik.Web.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        if (await db.QuestionPackages.AnyAsync())
            return;

        await AddPackageAsync(db, "Klasik",
            "Grupta en çok geç kalan kim?",
            "En dramatik kişi kim?",
            "Grupta en komik kişi kim?",
            "Kim ilk mesajı hep geç yazıyor?",
            "En çok spoiler veren kim?");

        await AddPackageAsync(db, "İlişki",
            "İlk evlenecek kişi kim?",
            "Kim gizlice romantizm sever?",
            "En çok kıskanç kim?",
            "En iyi özür dileyen kim?",
            "En çok 'ben de' diyen kim?");

        await AddPackageAsync(db, "Üniversite",
            "En sık vize haftasında panik yaşayan kim?",
            "Kopya ile kurtulup sonra anlatan kim?",
            "En çok sınavdan sonra kahve için aceleci kim?",
            "En fazla uyuyan kim?",
            "Ödevleri son dakikaya bırakan kim?");

        await AddPackageAsync(db, "İş Hayatı",
            "En çok süreden yakınan kim?",
            "Kim toplantılarda kahve için yaşıyor?",
            "En çok e-postayı geç atan kim?",
            "Slack'i en fazla titreten kim?",
            "İlk çıkış yapan kim?");

        await AddPackageAsync(db, "Cesur Sorular",
            "Grup fotoğrafında yüz yapmayan kim?",
            "Kim gizlice influencer olmak ister?",
            "En çok filtreden geçiren kim?",
            "Grubun sırrını en fazla sızdıran kim?",
            "En tehlikeli fikirlere hep 'ben varım' diyen kim?");
    }

    private static async Task AddPackageAsync(ApplicationDbContext db, string name, params string[] questions)
    {
        var pkg = new QuestionPackage { Name = name, IsActive = true };
        foreach (var q in questions)
        {
            pkg.Questions.Add(new Question { Text = q });
        }

        db.QuestionPackages.Add(pkg);
        await db.SaveChangesAsync();
    }
}
