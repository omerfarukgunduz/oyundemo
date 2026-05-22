using IfsaKlasik.Web.Data;
using IfsaKlasik.Web.Models.Entities;
using IfsaKlasik.Web.Models.ViewModels;
using IfsaKlasik.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IfsaKlasik.Web.Controllers;

/// <summary>Anonim masada oyun sonu geri bildirimi — Identity gerektirmez.</summary>
public sealed class FeedbackController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IRoomCookieService _cookies;

    public FeedbackController(ApplicationDbContext db, IRoomCookieService cookies)
    {
        _db = db;
        _cookies = cookies;
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitPlayFeedback([FromForm] SubmitPlayFeedbackVm model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var first = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
            return BadRequest(new { ok = false, error = first ?? "Form doğrulanamadı." });
        }

        var code = RoomCodeNormalizer.Normalize(model.RoomCode);
        if (!RoomCodeNormalizer.IsValidFormat(code))
        {
            return BadRequest(new { ok = false, error = "Geçersiz oda kodu." });
        }

        var cookieGuid = _cookies.TryGetParticipantPublicId(Request, code);
        if (cookieGuid is null || cookieGuid.Value != model.MemberGuid)
        {
            return BadRequest(new { ok = false, error = "Masada oturumun doğrulanamadı; sayfayı yenileyip tekrar deneyebilirsiniz." });
        }

        var roomExists = await _db.Rooms.AsNoTracking().AnyAsync(r => r.Code == code, ct);
        if (!roomExists)
        {
            return BadRequest(new { ok = false, error = "Oda bulunamadı." });
        }

        var nick = await _db.RoomMembers.AsNoTracking()
            .Where(m => m.PublicId == model.MemberGuid)
            .Select(m => m.Nickname)
            .FirstOrDefaultAsync(ct);

        if (nick is null)
        {
            return BadRequest(new { ok = false, error = "Oyuncu kaydı bulunamadı." });
        }

        var message = model.Message.Trim();
        if (message.Length < 2)
        {
            return BadRequest(new { ok = false, error = "Yanıt biraz daha uzun olsun (en az 2 karakter)." });
        }

        if (message.Length > 2000)
        {
            message = message[..2000];
        }

        var row = new PlayFeedback
        {
            RoomCode = code,
            MemberPublicId = model.MemberGuid,
            Nickname = nick,
            DeveloperMessage = message,
            SubmittedAtUtc = DateTime.UtcNow,
        };

        _db.PlayFeedbacks.Add(row);
        await _db.SaveChangesAsync(ct);

        return Json(new { ok = true });
    }
}
