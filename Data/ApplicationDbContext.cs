using IfsaKlasik.Web.Models;
using IfsaKlasik.Web.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IfsaKlasik.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<QuestionPackage> QuestionPackages => Set<QuestionPackage>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomMember> RoomMembers => Set<RoomMember>();
    public DbSet<RoomPlayedQuestion> RoomPlayedQuestions => Set<RoomPlayedQuestion>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<RoundAnswer> RoundAnswers => Set<RoundAnswer>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<QuestionPackage>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.Name);
        });

        builder.Entity<Question>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(2000).IsRequired();
            e.HasOne(x => x.Package)
                .WithMany(x => x.Questions)
                .HasForeignKey(x => x.PackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Room>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(16).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();

            e.HasOne(x => x.SelectedPackage).WithMany().HasForeignKey(x => x.SelectedPackageId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.HostMember).WithMany().HasForeignKey(x => x.HostMemberId)
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.CurrentRound).WithMany().HasForeignKey(x => x.CurrentRoundId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<RoomMember>(e =>
        {
            e.Property(x => x.Nickname).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.PublicId).IsUnique();

            e.HasOne(x => x.Room).WithMany(x => x.Members).HasForeignKey(x => x.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RoomPlayedQuestion>(e =>
        {
            e.HasKey(x => new { x.RoomId, x.QuestionId });

            e.HasOne(x => x.Room).WithMany(x => x.PlayedQuestions).HasForeignKey(x => x.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Round>(e =>
        {
            e.Property(x => x.ShuffledAnswersJson).HasMaxLength(8000);

            e.HasOne(x => x.Room).WithMany(x => x.Rounds).HasForeignKey(x => x.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RoundAnswer>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(512).IsRequired();

            e.HasOne(x => x.Round).WithMany(x => x.Answers).HasForeignKey(x => x.RoundId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.RoomMember).WithMany(x => x.Answers).HasForeignKey(x => x.RoomMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.RoundId, x.RoomMemberId }).IsUnique();
        });
    }
}
