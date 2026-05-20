using IfsaKlasik.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace IfsaKlasik.Web.Hubs;

public sealed record HubAck(bool Ok, string? Error);

public sealed class GameHub : Hub
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GameHub(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        using var scope = _scopeFactory.CreateScope();
        var game = scope.ServiceProvider.GetRequiredService<IGameRoomService>();
        await game.OnDisconnected(Context.ConnectionId, Context.ConnectionAborted);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task<HubAck> WithGame(Func<IGameRoomService, Task<Result>> func)
    {
        using var scope = _scopeFactory.CreateScope();
        var game = scope.ServiceProvider.GetRequiredService<IGameRoomService>();
        var r = await func(game);
        return new HubAck(r.Ok, r.Error);
    }

    public Task<HubAck> JoinRoom(string roomCode, Guid memberGuid) =>
        WithGame(g => g.JoinSignalRGroupAsync(roomCode, memberGuid, Context.ConnectionId, Context.ConnectionAborted));

    public Task<HubAck> SetNickname(string roomCode, Guid memberGuid, string nickname, Guid? targetMemberGuid) =>
        WithGame(g => g.SetNicknameAsync(roomCode, memberGuid, targetMemberGuid, nickname, Context.ConnectionAborted));

    public Task<HubAck> HostSelectPackage(string roomCode, Guid memberGuid, int packageId) =>
        WithGame(g => g.HostSetPackageAsync(roomCode, memberGuid, packageId, Context.ConnectionAborted));

    public Task<HubAck> HostKickMember(string roomCode, Guid memberGuid, Guid targetMemberGuid) =>
        WithGame(g => g.HostKickMemberAsync(roomCode, memberGuid, targetMemberGuid, Context.ConnectionAborted));

    public Task<HubAck> HostStart(string roomCode, Guid memberGuid) =>
        WithGame(g => g.HostStartGameAsync(roomCode, memberGuid, Context.ConnectionAborted));

    public Task<HubAck> SubmitAnswer(string roomCode, Guid memberGuid, string text) =>
        WithGame(g => g.SubmitAnswerAsync(roomCode, memberGuid, text, Context.ConnectionAborted));

    public Task<HubAck> HostRevealNow(string roomCode, Guid memberGuid) =>
        WithGame(g => g.HostRevealNowAsync(roomCode, memberGuid, Context.ConnectionAborted));

    public Task<HubAck> HostNextQuestion(string roomCode, Guid memberGuid) =>
        WithGame(g => g.HostNextQuestionAsync(roomCode, memberGuid, Context.ConnectionAborted));

    public Task<HubAck> SendChat(string roomCode, Guid memberGuid, string message) =>
        WithGame(g => g.SendRoomChatAsync(roomCode, memberGuid, message, Context.ConnectionAborted));
}
