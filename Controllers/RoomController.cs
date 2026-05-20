using IfsaKlasik.Web.Data;
using IfsaKlasik.Web.Models.Entities;
using IfsaKlasik.Web.Models.ViewModels;
using IfsaKlasik.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IfsaKlasik.Web.Controllers;

public sealed class RoomController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IRoomCookieService _cookies;

    public RoomController(ApplicationDbContext db, IRoomCookieService cookies)
    {
        _db = db;
        _cookies = cookies;
    }

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] string nickname, CancellationToken ct)
    {
        nickname = NormalizeNick(nickname);

        await using var trx = await _db.Database.BeginTransactionAsync(ct);

        var code = await CreateUniqueRoomCodeAsync(ct);

        var firstPkg = await _db.QuestionPackages.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync(ct);

        var room = new Room
        {
            Code = code,
            Phase = RoomPhase.Lobby,
            CreatedAtUtc = DateTime.UtcNow,
            SelectedPackageId = firstPkg?.Id,
            RoundTimerSeconds = 0,
            HostMemberId = null,
        };

        _db.Rooms.Add(room);
        await _db.SaveChangesAsync(ct);

        var host = new RoomMember
        {
            RoomId = room.Id,
            PublicId = Guid.NewGuid(),
            Nickname = nickname,
            IsHost = true,
            IsConnected = false,
        };

        _db.RoomMembers.Add(host);
        await _db.SaveChangesAsync(ct);

        room.HostMemberId = host.Id;
        await _db.SaveChangesAsync(ct);

        await trx.CommitAsync(ct);

        _cookies.SetParticipantCookie(Response, code, host.PublicId);
        return RedirectToAction(nameof(Lobby), new { code });
    }

    [HttpGet]
    public IActionResult Join(string? code) => View(new JoinVm { Code = code ?? string.Empty });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join([FromForm] JoinVm model, CancellationToken ct)
    {
        model.Code = RoomCodeNormalizer.Normalize(model.Code);
        if (!RoomCodeNormalizer.IsValidFormat(model.Code))
        {
            ModelState.AddModelError(nameof(model.Code), "Geçersiz oda kodu.");
            return View(model);
        }

        var exists = await _db.Rooms.AsNoTracking().AnyAsync(r => r.Code == model.Code, ct);
        if (!exists)
        {
            ModelState.AddModelError(nameof(model.Code), "Bu kodla oda bulunamadı.");
            return View(model);
        }

        var nick = NormalizeNick(model.Nickname);
        await using var trx = await _db.Database.BeginTransactionAsync(ct);

        var room = await _db.Rooms.FirstAsync(r => r.Code == model.Code, ct);

        var member = new RoomMember
        {
            RoomId = room.Id,
            PublicId = Guid.NewGuid(),
            Nickname = nick,
            IsHost = false,
            IsConnected = false,
        };
        _db.RoomMembers.Add(member);
        await _db.SaveChangesAsync(ct);

        await trx.CommitAsync(ct);

        _cookies.SetParticipantCookie(Response, model.Code, member.PublicId);

        return RedirectToAction(nameof(Lobby), new { code = model.Code });
    }

    [HttpGet]
    public async Task<IActionResult> Lobby(string code, CancellationToken ct)
    {
        code = RoomCodeNormalizer.Normalize(code ?? string.Empty);
        if (!RoomCodeNormalizer.IsValidFormat(code))
            return RedirectToAction(nameof(Join), new { code });

        var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Code == code, ct);
        if (room is null)
            return RedirectToAction(nameof(Join), new { code });

        var memberId = _cookies.TryGetParticipantPublicId(Request, code);
        if (!memberId.HasValue)
            return RedirectToAction(nameof(Join), new { code });

        var memberRow = await _db.RoomMembers.AsNoTracking()
            .Where(m => m.RoomId == room.Id && m.PublicId == memberId.Value)
            .Select(m => new { m.Id, m.Nickname })
            .FirstOrDefaultAsync(ct);
        if (memberRow is null)
            return RedirectToAction(nameof(Join), new { code });

        if (room.Phase != RoomPhase.Lobby)
            return RedirectToAction(nameof(Play), new { code });

        var pkgs = await _db.QuestionPackages.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new PackageVm(p.Id, p.Name))
            .ToListAsync(ct);

        var invite = BuildInviteUrl(code);
        var isHost = room.HostMemberId != null && room.HostMemberId == memberRow.Id;

        return View(new LobbyPageVm(code, memberId.Value, isHost,
            invite, pkgs,
            room.SelectedPackageId,
            memberRow.Nickname));
    }

    [HttpGet]
    public async Task<IActionResult> Play(string code, CancellationToken ct)
    {
        code = RoomCodeNormalizer.Normalize(code ?? string.Empty);
        if (!RoomCodeNormalizer.IsValidFormat(code))
            return RedirectToAction(nameof(Join), new { code });

        var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Code == code, ct);
        if (room is null)
            return RedirectToAction(nameof(Join), new { code });

        var memberGuid = _cookies.TryGetParticipantPublicId(Request, code);
        if (!memberGuid.HasValue)
            return RedirectToAction(nameof(Join), new { code });

        var memberExists = await _db.RoomMembers.AsNoTracking()
            .Where(m => m.RoomId == room.Id && m.PublicId == memberGuid.Value).Select(m => new { m.Id, m.Nickname })
            .FirstOrDefaultAsync(ct);
        if (memberExists is null)
            return RedirectToAction(nameof(Join), new { code });

        if (room.Phase == RoomPhase.Lobby)
            return RedirectToAction(nameof(Lobby), new { code });

        var isHost = room.HostMemberId == memberExists.Id;
        var invite = BuildInviteUrl(code);

        return View(new RoomPlayPageVm(code, memberGuid.Value, isHost, invite, memberExists.Nickname));
    }

    private async Task<string> CreateUniqueRoomCodeAsync(CancellationToken ct)
    {
        for (var i = 0; i < 50; i++)
        {
            var code = RoomCodeNormalizer.GenerateCode();
            var clash = await _db.Rooms.AsNoTracking().AnyAsync(r => r.Code == code, ct);
            if (!clash)
                return code;
        }

        throw new InvalidOperationException("Unique room code oluşturulamadı.");
    }

    private string BuildInviteUrl(string codeNormalized)
    {
        codeNormalized = RoomCodeNormalizer.Normalize(codeNormalized);
        return $"{Request.Scheme}://{Request.Host}{Url.Content($"~/Room/Join?code={Uri.EscapeDataString(codeNormalized)}")}";
    }

    private static string NormalizeNick(string nickname)
    {
        nickname = (nickname ?? "Misafir").Trim();
        if (nickname.Length < 2)
            nickname = "Misafir";
        if (nickname.Length > 32)
            nickname = nickname[..32];
        return nickname;
    }
}
