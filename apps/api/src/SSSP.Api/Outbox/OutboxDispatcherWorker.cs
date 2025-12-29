using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SSSP.BL.Outbox;

namespace SSSP.Api.Realtime.Outbox;

public sealed class OutboxDispatcherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcherWorker> _logger;

    public OutboxDispatcherWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxDispatcherWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reader = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
                var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

                var batch = await reader.DequeueBatchAsync(batchSize: 50, stoppingToken);

                if (batch.Count == 0)
                {
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                foreach (var msg in batch)
                {
                    try
                    {
                        // PayloadJson should be a JSON object
                        using var doc = JsonDocument.Parse(msg.PayloadJson);
                        var payload = doc.RootElement.Clone();

                        await PublishAsync(
                            publisher,
                            msg.Scope,
                            msg.ScopeKey,
                            msg.Topic,
                            msg.Event,
                            payload,
                            stoppingToken);

                        _logger.LogInformation("OUTBOX => Scope={Scope}, ScopeKey={ScopeKey}, Topic={Topic}, Event={Event}",
                                msg.Scope, msg.ScopeKey, msg.Topic, msg.Event);


                        await reader.MarkProcessedAsync(msg.Id, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Outbox message failed. Id={Id}, Topic={Topic}, Event={Event}", msg.Id, msg.Topic, msg.Event);
                        await reader.MarkFailedAsync(msg.Id, ex.Message, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxDispatcherWorker loop failed.");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private static Task PublishAsync(
        INotificationPublisher publisher,
        string scope,
        string? scopeKey,
        string topic,
        string @event,
        JsonElement payload,
        CancellationToken ct)
    {
        return scope switch
        {
            "global" => publisher.ToGlobalAsync(topic, @event, payload, ct),
            "operator" => publisher.ToOperatorAsync(int.Parse(scopeKey!), topic, @event, payload, ct),
            "incident" => publisher.ToIncidentAsync(int.Parse(scopeKey!), topic, @event, payload, ct),
            "user" => publisher.ToUserAsync(Guid.Parse(scopeKey!), topic, @event, payload, ct),
            "role" => publisher.ToRoleAsync(scopeKey!, topic, @event, payload, ct),
            "camera" => publisher.ToCameraAsync(scopeKey!, topic, @event, payload, ct),
            _ => publisher.ToGlobalAsync(topic, @event, payload, ct),
        };
    }
}
