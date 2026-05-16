using IfsaKlasik.Web.Data;
using IfsaKlasik.Web.Models.Entities;
using IfsaKlasik.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IfsaKlasik.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = IfsaRoles.Admin)]
public sealed class PackagesController : Controller
{
    private readonly ApplicationDbContext _db;

    public PackagesController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var rows = await _db.QuestionPackages.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new AdminPackageRow(p.Id, p.Name, p.IsActive, p.Questions.Count))
            .ToListAsync(ct);

        return View(rows);
    }

    [HttpGet]
    public IActionResult Create() => View(new QuestionPackageVm { Name = string.Empty });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] QuestionPackageVm vm, CancellationToken ct)
    {
        vm.Name = vm.Name.Trim();
        if (string.IsNullOrWhiteSpace(vm.Name) || vm.Name.Length > 128)
        {
            ModelState.AddModelError(nameof(vm.Name), "Paket adı 1–128 karakter olmalıdır.");
            return View(vm);
        }

        var exists = await _db.QuestionPackages.AnyAsync(p => p.Name == vm.Name, ct);
        if (exists)
        {
            ModelState.AddModelError(nameof(vm.Name), "Bu isimde paket zaten var.");
            return View(vm);
        }

        _db.QuestionPackages.Add(new QuestionPackage { Name = vm.Name, IsActive = vm.IsActive });
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var row = await _db.QuestionPackages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (row is null)
            return NotFound();

        var vm = new QuestionPackageVm { Id = row.Id, Name = row.Name, IsActive = row.IsActive };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromForm] QuestionPackageVm vm, CancellationToken ct)
    {
        vm.Name = vm.Name.Trim();
        if (vm.Id <= 0)
            return BadRequest();

        var entity = await _db.QuestionPackages.FirstOrDefaultAsync(p => p.Id == vm.Id, ct);
        if (entity is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(vm.Name) || vm.Name.Length > 128)
        {
            ModelState.AddModelError(nameof(vm.Name), "Paket adı 1–128 karakter olmalıdır.");
            return View(vm);
        }

        entity.Name = vm.Name;
        entity.IsActive = vm.IsActive;
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.QuestionPackages.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null)
            return NotFound();

        _db.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Index));
    }

}

public sealed record AdminPackageRow(int Id, string Name, bool IsActive, int QuestionCount);

public sealed class QuestionPackageVm
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
