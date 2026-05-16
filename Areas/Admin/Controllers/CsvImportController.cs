using IfsaKlasik.Web.Data;
using IfsaKlasik.Web.Models;
using IfsaKlasik.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IfsaKlasik.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = IfsaRoles.Admin)]
public sealed class CsvImportController : Controller
{
    private readonly ApplicationDbContext _db;

    public CsvImportController(ApplicationDbContext db)
    {
        _db = db;
    }

    public IActionResult Index()
    {
        return View(new CsvImportVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Bir CSV dosyası seçin.");
            return View(new CsvImportVm());
        }

        var imported = 0;
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);

        await using var trx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var isFirstNonEmptyLine = true;
            while (!ct.IsCancellationRequested)
            {
                var raw = await reader.ReadLineAsync(ct);
                if (raw is null)
                    break;

                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                if (isFirstNonEmptyLine &&
                    line.Equals("package_name,question_text", StringComparison.OrdinalIgnoreCase))
                {
                    isFirstNonEmptyLine = false;
                    continue;
                }

                isFirstNonEmptyLine = false;

                var splitIdx = line.IndexOf(',', StringComparison.Ordinal);
                if (splitIdx <= 0 || splitIdx == line.Length - 1)
                    continue;

                var pkgRaw = line[..splitIdx].Trim();
                var qRaw = line[(splitIdx + 1)..].Trim();

                if (pkgRaw.Equals("package_name", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(pkgRaw) || string.IsNullOrWhiteSpace(qRaw))
                    continue;

                if (pkgRaw.Length > 128 || qRaw.Length > 2000)
                    continue;

                var pkg = await _db.QuestionPackages.FirstOrDefaultAsync(p => p.Name == pkgRaw, ct);
                if (pkg is null)
                {
                    pkg = new QuestionPackage { Name = pkgRaw, IsActive = true };
                    _db.QuestionPackages.Add(pkg);
                    await _db.SaveChangesAsync(ct);
                }

                _db.Questions.Add(new Question { PackageId = pkg.Id, Text = qRaw });
                imported++;
            }

            await _db.SaveChangesAsync(ct);
            await trx.CommitAsync(ct);
        }
        catch
        {
            await trx.RollbackAsync(ct);
            throw;
        }

        return View(new CsvImportVm
        {
            Success = $"İçe aktarılan satır: {imported}",
        });
    }
}

public sealed class CsvImportVm
{
    public string? Success { get; init; }
}
