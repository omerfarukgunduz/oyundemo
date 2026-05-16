using IfsaKlasik.Web.Data;
using IfsaKlasik.Web.Models;
using IfsaKlasik.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IfsaKlasik.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = IfsaRoles.Admin)]
public sealed class QuestionsController : Controller
{
    private readonly ApplicationDbContext _db;

    public QuestionsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int packageId, CancellationToken ct)
    {
        var pkg = await _db.QuestionPackages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == packageId, ct);
        if (pkg is null)
            return NotFound();

        var rows = await _db.Questions.AsNoTracking()
            .Where(q => q.PackageId == packageId)
            .OrderBy(q => q.Id)
            .Select(q => new QuestionListItemVm(q.Id, q.Text))
            .ToListAsync(ct);

        return View(new QuestionsIndexVm(pkg.Name, packageId, rows));
    }

    [HttpGet]
    public async Task<IActionResult> Create(int packageId, CancellationToken ct)
    {
        if (!await _db.QuestionPackages.AnyAsync(p => p.Id == packageId, ct))
            return NotFound();

        return View(new QuestionVm { PackageId = packageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] QuestionVm vm, CancellationToken ct)
    {
        vm.Text = vm.Text.Trim();

        if (string.IsNullOrWhiteSpace(vm.Text) || vm.Text.Length > 2000)
            ModelState.AddModelError(nameof(vm.Text), "Soru metni zorunludur ve 2000 karakteri geçemez.");

        if (!await _db.QuestionPackages.AnyAsync(p => p.Id == vm.PackageId, ct))
            ModelState.AddModelError(string.Empty, "Paket geçersiz.");

        if (!ModelState.IsValid)
            return View(vm);

        _db.Questions.Add(new Question { PackageId = vm.PackageId, Text = vm.Text });
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index), new { packageId = vm.PackageId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var entity = await _db.Questions.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id, ct);
        if (entity is null)
            return NotFound();

        return View(new QuestionVm { Id = entity.Id, PackageId = entity.PackageId, Text = entity.Text });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromForm] QuestionVm vm, CancellationToken ct)
    {
        vm.Text = vm.Text.Trim();

        var entity = await _db.Questions.FirstOrDefaultAsync(q => q.Id == vm.Id, ct);
        if (entity is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(vm.Text) || vm.Text.Length > 2000)
        {
            ModelState.AddModelError(nameof(vm.Text), "Soru metni zorunludur ve 2000 karakteri geçemez.");
            return View(vm);
        }

        entity.Text = vm.Text;
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index), new { packageId = entity.PackageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.Questions.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (entity is null)
            return NotFound();

        var pkg = entity.PackageId;
        _db.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index), new { packageId = pkg });
    }
}

public sealed record QuestionListItemVm(int Id, string Text);

public sealed class QuestionsIndexVm
{
    public QuestionsIndexVm(string packageName, int packageId, IReadOnlyList<QuestionListItemVm> questions)
    {
        PackageName = packageName;
        PackageId = packageId;
        Questions = questions;
    }

    public string PackageName { get; }
    public int PackageId { get; }
    public IReadOnlyList<QuestionListItemVm> Questions { get; }
}

public sealed class QuestionVm
{
    public int Id { get; set; }

    public int PackageId { get; set; }

    public string Text { get; set; } = string.Empty;
}
