using FriendMap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FriendMap.Api.Services;

public class NotificationDispatchService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ApnsClient _apnsClient;
    private readonly NotificationDispatchOptions _options;
    private readonly ILogger<NotificationDispatchService> _logger;

    public NotificationDispatchService(
        IServiceScopeFactory scopeFactory,
        ApnsClient apnsClient,
        IOptions<NotificationDispatchOptions> options,
        ILogger<NotificationDispatchService> logger)
    {
        _scopeFactory = scopeFactory;
        _apnsClient = apnsClient;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.DispatchEnabled)
        {
            _logger.LogInformation("Notification dispatch is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await DispatchBatchAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.DispatchIntervalSeconds)), stoppingToken);
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        var items = await db.NotificationOutboxItems
            .Where(x => x.Status == "pending" && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now))
            .OrderBy(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);

        foreach (var item in items)
        {
            var tokens = await db.NotificationDeviceTokens
                .Where(x => x.UserId == item.UserId && x.Platform == "ios" && x.IsActive)
                .ToListAsync(ct);

            if (tokens.Count == 0)
            {
                item.Status = "skipped";
                item.LastError = "No active iOS device tokens.";
                item.UpdatedAtUtc = now;
                continue;
            }

            try
            {
                foreach (var token in tokens)
                {
                    await _apnsClient.SendAsync(token.DeviceToken, item.Title, item.Body, item.PayloadJson, ct);
                }

                item.Status = "sent";
                item.SentAtUtc = DateTimeOffset.UtcNow;
                item.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                item.Attempts++;
                item.LastError = ex.Message;
                item.UpdatedAtUtc = DateTimeOffset.UtcNow;
                item.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, item.Attempts)));

                if (item.Attempts >= _options.MaxAttempts)
                {
                    item.Status = "failed";
                }

                _logger.LogWarning(ex, "Notification outbox item {ItemId} failed.", item.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
