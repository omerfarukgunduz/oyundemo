using IfsaKlasik.Web.Data;
using IfsaKlasik.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IfsaKlasik.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = IfsaRoles.Admin)]
public sealed class FeedbackController : Controller
{
    private readonly ApplicationDbContext _db;

    public FeedbackController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var rows = await _db.PlayFeedbacks.AsNoTracking()
            .OrderByDescending(f => f.SubmittedAtUtc)
            .Take(500)
            .ToListAsync(ct);

        return View(rows);
    }
}
