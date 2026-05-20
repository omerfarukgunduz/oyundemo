using System.Text.Json;
using IfsaKlasik.Web.Data;
using IfsaKlasik.Web.Hubs;
using IfsaKlasik.Web.Models.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IfsaKlasik.Web.Services;

public interface IGameRoomService
{
    Task OnDisconnected(string connectionId, CancellationToken ct = default);

    Task<GameHubState?> GetStateForParticipantAsync(string roomCodeNormalized, Guid memberPublicId,
        CancellationToken ct = default);

    Task<Result> JoinSignalRGroupAsync(string roomCodeNormalized, Guid memberPublicId, string connectionId,
        CancellationToken ct = default);

    Task<Result> SetNicknameAsync(string roomCodeNormalized, Guid memberPublicId, Guid? actingAsHostPublicId,
        string nickname, CancellationToken ct = default);

    Task<Result> HostKickMemberAsync(string roomCodeNormalized, Guid hostPublicId, Guid targetPublicId,
        CancellationToken ct = default);

    Task<Result> HostSetPackageAsync(string roomCodeNormalized, Guid memberPublicId, int packageId,
        CancellationToken ct = default);

    Task<Result> HostStartGameAsync(string roomCodeNormalized, Guid memberPublicId, CancellationToken ct = default);

    Task<Result> SubmitAnswerAsync(string roomCodeNormalized, Guid memberPublicId, string text,
        CancellationToken ct = default);

    Task<Result> HostRevealNowAsync(string roomCodeNormalized, Guid memberPublicId, CancellationToken ct = default);

    Task<Result> HostNextQuestionAsync(string roomCodeNormalized, Guid memberPublicId, CancellationToken ct = default);

    Task<Result> SendRoomChatAsync(string roomCodeNormalized, Guid memberPublicId, string message,
        CancellationToken ct = default);

    Task ProcessExpiredRoundsAsync(CancellationToken ct = default);
}

public sealed record Result(bool Ok, string? Error);

public sealed record GameHubState(
    string RoomCode,
    string Phase,
    int? SelectedPackageId,
    int TimerSecondsConfigured,
    string? QuestionText,
    DateTime? RoundEndsUtc,
    int? CurrentRoundId,
    IList<string>? ShuffledCards,
    IReadOnlyList<LobbyPersonDto> People,
    bool YouAreHost,
    string YourNickname,
    bool AlreadySubmittedAnswerThisRound,
    int AnswersSubmittedCount,
    int AnswersRoomMemberTotal);

public sealed record LobbyPersonDto(Guid PublicId, string Nickname, bool IsHost, bool IsConnected);

public sealed class GameRoomService : IGameRoomService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<GameHub> _hub;

    public GameRoomService(ApplicationDbContext db, IHubContext<GameHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task OnDisconnected(string connectionId, CancellationToken ct = default)
    {
        var member = await _db.RoomMembers
            .Include(m => m.Room)
            .FirstOrDefaultAsync(m => m.SignalRConnectionId == connectionId, ct);
        if (member is null)
            return;

        member.SignalRConnectionId = null;
        member.IsConnected = false;
        await _db.SaveChangesAsync(ct);
        await PublishLobby(member.Room.Code, ct);
        await PublishState(member.Room.Code, ct);
    }

    public async Task<GameHubState?> GetStateForParticipantAsync(string roomCodeNormalized, Guid memberPublicId,
        CancellationToken ct = default)
    {
        var roomCode = RoomCodeNormalizer.Normalize(roomCodeNormalized);
        var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Code == roomCode, ct);
        if (room is null)
            return null;

        var viewer = await _db.RoomMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.RoomId == room.Id && m.PublicId == memberPublicId, ct);
        if (viewer is null)
            return null;

        return await BuildStateDbAsync(room.Id, viewer, ct);
    }

    public async Task<Result> JoinSignalRGroupAsync(string roomCodeNormalized, Guid memberPublicId,
        string connectionId, CancellationToken ct = default)
    {
        var code = RoomCodeNormalizer.Normalize(roomCodeNormalized);
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (room is null)
            return Fail("Oda bulunamadı.");

        var member = await _db.RoomMembers.FirstOrDefaultAsync(m => m.RoomId == room.Id && m.PublicId == memberPublicId, ct);
        if (member is null)
            return Fail("Oyuncu bulunamadı.");

        member.SignalRConnectionId = connectionId;
        member.IsConnected = true;
        await _db.SaveChangesAsync(ct);

        await _hub.Groups.AddToGroupAsync(connectionId, GroupName(code), ct);
        await PublishLobby(code, ct);
        await PublishState(code, ct);
        return Ok();
    }

    public async Task<Result> SetNicknameAsync(string roomCodeNormalized, Guid memberPublicId, Guid? actingAsHostPublicId,
        string nickname, CancellationToken ct = default)
    {
        var code = RoomCodeNormalizer.Normalize(roomCodeNormalized);
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (room is null)
            return Fail("Oda bulunamadı.");

        nickname = nickname.Trim();
        if (nickname.Length < 2 || nickname.Length > 32)
            return Fail("İsim 2–32 karakter olmalı.");

        var actor = await _db.RoomMembers.FirstOrDefaultAsync(m => m.RoomId == room.Id && m.PublicId == memberPublicId, ct);
        if (actor is null)
            return Fail("Oyuncu bulunamadı.");

        RoomMember target;
        if (actingAsHostPublicId is { } hid && hid != memberPublicId)
        {
            if (room.HostMemberId != actor.Id)
                return Fail("Sadece oda kurucusu başkasının adını güncelleyebilir.");

            target = await _db.RoomMembers.FirstOrDefaultAsync(m => m.RoomId == room.Id && m.PublicId == hid, ct) ?? actor;
        }
        else
        {
            target = actor;
        }

        target.Nickname = nickname;
        await _db.SaveChangesAsync(ct);
        await PublishLobby(code, ct);
        return Ok();
    }

    public async Task<Result> HostKickMemberAsync(string roomCodeNormalized, Guid hostPublicId, Guid targetPublicId,
        CancellationToken ct = default)
    {
        var code = RoomCodeNormalizer.Normalize(roomCodeNormalized);
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (room is null)
            return Fail("Oda bulunamadı.");

        var hostActor = await _db.RoomMembers.FirstOrDefaultAsync(
            m => m.RoomId == room.Id && m.PublicId == hostPublicId, ct);
        if (hostActor is null || room.HostMemberId != hostActor.Id)
            return Fail("Sadece oda kurucusu oyuncu atabilir.");

        if (hostPublicId == targetPublicId)
            return Fail("Kendini atamazsin.");

        var target = await _db.RoomMembers.FirstOrDefaultAsync(m => m.RoomId == room.Id && m.PublicId == targetPublicId, ct);
        if (target is null)
            return Fail("Bu oyuncu masada bulunamadi.");

        if (target.Id == room.HostMemberId)
            return Fail("Kurucu atilamaz.");

        var kickedConn = target.SignalRConnectionId;

        var answers = await _db.RoundAnswers.Where(a => a.RoomMemberId == target.Id).ToListAsync(ct);
        _db.RoundAnswers.RemoveRange(answers);
        _db.RoomMembers.Remove(target);
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(kickedConn))
        {
            try
            {
                await _hub.Clients.Client(kickedConn).SendAsync(
                    "kickedFromRoom",
                    new { roomCode = room.Code, message = "Oda kurucusu seni odadan çıkardı." },
                    ct);
                await _hub.Groups.RemoveFromGroupAsync(kickedConn, GroupName(code), ct);
            }
            catch
            {
                // bağlantı kapanmış olabilir
            }
        }

        room = await _db.Rooms.FirstAsync(r => r.Id == room.Id, ct);
        var revealed = await TryCompletingRoundAfterAllAnswersAsync(room, ct);
        await PublishLobby(code, ct);
        if (!revealed)
            await PublishState(code, ct);

        return Ok();
    }

    public async Task<Result> HostSetPackageAsync(string roomCodeNormalized, Guid memberPublicId, int packageId,
        CancellationToken ct = default)
    {
        var code = RoomCodeNormalizer.Normalize(roomCodeNormalized);
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (room is null)
            return Fail("Oda bulunamadı.");

        var member = await _db.RoomMembers.FirstOrDefaultAsync(m => m.RoomId == room.Id && m.PublicId == memberPublicId, ct);
        if (member is null || room.HostMemberId != member.Id)
            return Fail("Sadece kurucu paket seçebilir.");

        if (room.Phase != RoomPhase.Lobby)
            return Fail("Oyundayken paket değiştirilemez.");

        var pkgExists = await _db.QuestionPackages.AnyAsync(p => p.Id == packageId && p.IsActive, ct);
        if (!pkgExists)
            return Fail("Paket geçersiz.");

        room.SelectedPackageId = packageId;
        await _db.SaveChangesAsync(ct);

        await PublishLobby(code, ct);
        await PublishState(code, ct);
        return Ok();
    }

    public async Task<Result> HostStartGameAsync(string roomCodeNormalized, Guid memberPublicId,
        CancellationToken ct = default)
    {
        var code = RoomCodeNormalizer.Normalize(roomCodeNormalized);
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (room is null)
            return Fail("Oda bulunamadı.");

        var member = await _db.RoomMembers.FirstOrDefaultAsync(m => m.RoomId == room.Id && m.PublicId == memberPublicId, ct);
        if (member is null || room.HostMemberId != member.Id)
            return Fail("Sadece kurucu oyunu başlatabilir.");

        if (!room.SelectedPackageId.HasValue)
            return Fail("Önce bir soru paketi seç.");

        await StartNewRoundAsync(room.Id, ct);
        return Ok();
    }

    public async Task<Result> SubmitAnswerAsync(string roomCodeNormalized, Guid memberPublicId, string text,
        CancellationToken ct = default)
    {
        text = text.Trim();
        if (text.Length is < 1 or > 500)
            return Fail("Cevap 1–500 karakter arasında olmalı.");

        var code = RoomCodeNormalizer.Normalize(roomCodeNormalized);
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (room is null)
            return Fail("Oda bulunamadı.");

        if (room.Phase != RoomPhase.CollectingAnswers || room.CurrentRoundId is null)
            return Fail("Şu anda cevap gönderilmiyor.");

        var round = await _db.Rounds
            .FirstOrDefaultAsync(r => r.Id == room.CurrentRoundId, ct);

        if (round is null)
            return Fail("Tur bulunamadı.");

        var dbMember = await _db.RoomMembers.FirstOrDefaultAsync(m => m.RoomId == room.Id && m.PublicId == memberPublicId, ct);
        if (dbMember is null)
            return Fail("Oyuncu bulunamadı.");

        var existing = await _db.RoundAnswers.FirstOrDefaultAsync(a => a.RoundId == round.Id && a.RoomMemberId == dbMember.Id, ct);
        if (existing is not null)
            return Fail("Bu soruya zaten cevap gönderdin.");

        _db.RoundAnswers.Add(new RoundAnswer { RoundId = round.Id, RoomMemberId = dbMember.Id, Text = text });

        await _db.SaveChangesAsync(ct);

        var revealed = await TryCompletingRoundAfterAllAnswersAsync(room, ct);
        if (!revealed)
            await PublishState(code, ct);

        return Ok();
    }

    public async Task<Result> HostRevealNowAsync(string roomCodeNormalized, Guid memberPublicId,
        CancellationToken ct = default)
    {
        var code = RoomCodeNormalizer.Normalize(roomCodeNormalized);
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (room is null)
            return Fail("Oda bulunamadı.");

        var member = await _db.RoomMembers.FirstOrDefaultAsync(m => m.RoomId == room.Id && m.PublicId == memberPublicId, ct);
        if (member is null || room.HostMemberId != member.Id)
            return Fail("Sadece kurucu kartları açabilir.");

        await RevealCurrentRoundAsync(room, ct);
        return Ok();
    }

    public async Task<Result> HostNextQuestionAsync(string roomCodeNormalized, Guid memberPublicId,
        CancellationToken ct = default)
    {
        var code = RoomCodeNormalizer.Normalize(roomCodeNormalized);
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (room is null)
            return Fail("Oda bulunamadı.");

        var member = await _db.RoomMembers.FirstOrDefaultAsync(m => m.RoomId == room.Id && m.PublicId == memberPublicId, ct);
        if (member is null || room.HostMemberId != member.Id)
            return Fail("Sadece kurucu sıradaki soruya geçebilir.");

        if (room.Phase != RoomPhase.Revealed)
            return Fail("Önce kartların açılmış olması gerek.");

        await StartNewRoundAsync(room.Id, ct);
        return Ok();
    }

    public async Task<Result> SendRoomChatAsync(string roomCodeNormalized, Guid memberPublicId, string message,
        CancellationToken ct = default)
    {
        message = message.Trim();
        if (message.Length is < 1 or > 400)
            return Fail("Mesaj 1-400 karakter olmalı.");

        var code = RoomCodeNormalizer.Normalize(roomCodeNormalized);
        var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Code == code, ct);
        if (room is null)
            return Fail("Oda bulunamadı.");

        var nickname = await _db.RoomMembers.AsNoTracking()
            .Where(m => m.RoomId == room.Id && m.PublicId == memberPublicId)
            .Select(m => m.Nickname)
            .FirstOrDefaultAsync(ct);
        if (nickname is null)
            return Fail("Oyuncu bulunamadı.");

        var payload = new { nickname, text = message, sentAtUtc = DateTime.UtcNow };
        await _hub.Clients.Group(GroupName(code)).SendAsync("chatMessage", payload, ct);
        return Ok();
    }

    public async Task ProcessExpiredRoundsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var dueRoundIds = await _db.Rounds
            .Where(rr => rr.EndsAtUtc != null && rr.EndsAtUtc <= now)
            .Select(rr => rr.Id)
            .ToListAsync(ct);

        foreach (var roundId in dueRoundIds)
        {
            var room = await _db.Rooms.FirstOrDefaultAsync(
                r => r.Phase == RoomPhase.CollectingAnswers && r.CurrentRoundId == roundId,
                ct);
            if (room is null)
                continue;

            await RevealCurrentRoundAsync(room, ct);
        }
    }

    private async Task StartNewRoundAsync(int roomId, CancellationToken ct)
    {
        var room = await _db.Rooms.FirstAsync(r => r.Id == roomId, ct);

        if (!room.SelectedPackageId.HasValue)
            throw new InvalidOperationException("Paket seçilmemiş.");

        room.Phase = RoomPhase.CollectingAnswers;

        var playedIds = await _db.RoomPlayedQuestions.Where(p => p.RoomId == room.Id).Select(p => p.QuestionId)
            .ToListAsync(ct);

        var candidates = await _db.Questions
            .Where(q => q.PackageId == room.SelectedPackageId && !playedIds.Contains(q.Id))
            .Select(q => q.Id)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            var oldPlayed = await _db.RoomPlayedQuestions.Where(p => p.RoomId == room.Id).ToListAsync(ct);
            _db.RoomPlayedQuestions.RemoveRange(oldPlayed);
            await _db.SaveChangesAsync(ct);
            candidates = await _db.Questions.Where(q => q.PackageId == room.SelectedPackageId).Select(q => q.Id)
                .ToListAsync(ct);
        }

        var qid = candidates[Random.Shared.Next(candidates.Count)];
        _db.RoomPlayedQuestions.Add(new RoomPlayedQuestion { RoomId = room.Id, QuestionId = qid });

        var round = new Round
        {
            RoomId = room.Id,
            QuestionId = qid,
            StartedAtUtc = DateTime.UtcNow,
            EndsAtUtc = null,
        };
        _db.Rounds.Add(round);
        await _db.SaveChangesAsync(ct);

        room.CurrentRoundId = round.Id;
        await _db.SaveChangesAsync(ct);

        await PublishState(room.Code, ct);
        await PublishLobby(room.Code, ct);
    }

    private async Task<bool> TryCompletingRoundAfterAllAnswersAsync(Room room, CancellationToken ct)
    {
        if (room.Phase != RoomPhase.CollectingAnswers || room.CurrentRoundId is null)
            return false;

        var roundId = room.CurrentRoundId.Value;
        var answerCount = await _db.RoundAnswers.CountAsync(a => a.RoundId == roundId, ct);
        var memberCount = await _db.RoomMembers.CountAsync(m => m.RoomId == room.Id, ct);
        if (memberCount == 0 || answerCount < memberCount)
            return false;

        await RevealCurrentRoundAsync(room, ct);
        return true;
    }

    private async Task RevealCurrentRoundAsync(Room room, CancellationToken ct)
    {
        if (room.Phase != RoomPhase.CollectingAnswers || room.CurrentRoundId is null)
            return;

        var roundId = room.CurrentRoundId.Value;
        var listMem =
            await _db.RoundAnswers.Where(a => a.RoundId == roundId).Select(a => a.Text).ToListAsync(ct);
        var texts = Shuffle(listMem);

        var round = await _db.Rounds.FirstAsync(r => r.Id == roundId, ct);
        round.ShuffledAnswersJson = JsonSerializer.Serialize(texts);
        room.Phase = RoomPhase.Revealed;

        await _db.SaveChangesAsync(ct);
        await _hub.Clients.Group(GroupName(room.Code)).SendAsync("cardsRevealed", texts, ct);
        await PublishState(room.Code, ct);
    }

    private static List<string> Shuffle(IReadOnlyList<string> items)
    {
        var arr = items.ToArray();
        for (var i = arr.Length - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }

        return arr.ToList();
    }

    private async Task<GameHubState> BuildStateDbAsync(int roomId, RoomMember viewer, CancellationToken ct)
    {
        var room = await _db.Rooms.AsNoTracking()
            .FirstAsync(r => r.Id == roomId, ct);

        var peopleQuery = await _db.RoomMembers.AsNoTracking()
            .Where(m => m.RoomId == roomId)
            .OrderByDescending(m => m.IsHost).ThenBy(m => m.Id)
            .Select(m => new LobbyPersonDto(m.PublicId, m.Nickname, m.IsHost, m.IsConnected))
            .ToListAsync(ct);

        IList<string>? cards = null;
        string? question = null;
        DateTime? ends = null;
        var alreadySubmittedThisRound = false;
        var answersSubmittedCount = 0;
        var answersRoomMemberTotal = 0;

        if (room.CurrentRoundId.HasValue)
        {
            var rr = await _db.Rounds.AsNoTracking().FirstAsync(r => r.Id == room.CurrentRoundId!.Value, ct);
            var qtxt = await _db.Questions.AsNoTracking().Where(q => q.Id == rr.QuestionId).Select(q => q.Text)
                .FirstAsync(ct);
            question = qtxt;
            ends = rr.EndsAtUtc;

            if (room.Phase == RoomPhase.CollectingAnswers)
            {
                alreadySubmittedThisRound = await _db.RoundAnswers.AsNoTracking()
                    .AnyAsync(a => a.RoundId == rr.Id && a.RoomMemberId == viewer.Id, ct);
                answersSubmittedCount = await _db.RoundAnswers.AsNoTracking()
                    .CountAsync(a => a.RoundId == rr.Id, ct);
                answersRoomMemberTotal =
                    await _db.RoomMembers.AsNoTracking().CountAsync(m => m.RoomId == room.Id, ct);
            }

            if (room.Phase == RoomPhase.Revealed && rr.ShuffledAnswersJson is { } raw)
            {
                cards = JsonSerializer.Deserialize<List<string>>(raw) ?? [];
            }
        }

        var hostId = room.HostMemberId ?? 0;
        var viewerRow = await _db.RoomMembers.AsNoTracking().FirstAsync(v => v.Id == viewer.Id, ct);
        var isHost = hostId != 0 && viewerRow.Id == hostId;

        return new GameHubState(
            room.Code,
            room.Phase.ToString(),
            room.SelectedPackageId,
            room.RoundTimerSeconds,
            question,
            ends,
            room.CurrentRoundId,
            cards,
            peopleQuery,
            isHost,
            viewerRow.Nickname,
            alreadySubmittedThisRound,
            answersSubmittedCount,
            answersRoomMemberTotal);
    }

    private async Task PublishLobby(string normalizedCode, CancellationToken ct)
    {
        normalizedCode = RoomCodeNormalizer.Normalize(normalizedCode);
        var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Code == normalizedCode, ct);
        if (room is null)
            return;

        var people = await _db.RoomMembers.AsNoTracking()
            .Where(m => m.RoomId == room.Id)
            .OrderByDescending(m => m.IsHost).ThenBy(m => m.Id)
            .Select(m => new LobbyPersonDto(m.PublicId, m.Nickname, m.IsHost, m.IsConnected))
            .ToListAsync(ct);

        await _hub.Clients.Group(GroupName(normalizedCode)).SendAsync("lobbyUpdated", people, ct);
    }

    private async Task PublishState(string normalizedCode, CancellationToken ct)
    {
        normalizedCode = RoomCodeNormalizer.Normalize(normalizedCode);

        var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Code == normalizedCode, ct);
        if (room is null)
            return;

        var members = await _db.RoomMembers
            .Where(m => m.RoomId == room.Id && m.SignalRConnectionId != null)
            .ToListAsync(ct);

        foreach (var m in members)
        {
            if (string.IsNullOrEmpty(m.SignalRConnectionId))
                continue;

            var state = await BuildStateDbAsync(room.Id, m, ct);
            await _hub.Clients.Client(m.SignalRConnectionId!).SendAsync("stateFull", state, ct);
        }
    }

    private static string GroupName(string code) => "room-" + RoomCodeNormalizer.Normalize(code);

    private static Result Ok() => new(true, null);

    private static Result Fail(string msg) => new(false, msg);
}
