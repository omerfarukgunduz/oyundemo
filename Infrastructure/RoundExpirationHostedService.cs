using IfsaKlasik.Web.Services;

namespace IfsaKlasik.Web.Infrastructure;

public sealed class RoundExpirationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RoundExpirationHostedService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                using var scope = _scopeFactory.CreateScope();
                var game = scope.ServiceProvider.GetRequiredService<IGameRoomService>();
                await game.ProcessExpiredRoundsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shut down
        }
    }
}
