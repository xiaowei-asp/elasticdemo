using Outbox.Core;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Collections.Concurrent;
using System.Text;

public sealed class RabbitMqProducer : IMqProducer, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly SemaphoreSlim _confirmWindow =
    new(initialCount: 10000, maxCount: 10000);

    private long _localSeq = 0;

    private readonly ConcurrentDictionary<long, Guid> _pending = new();

    public RabbitMqProducer(IConnectionFactory factory)
    {
        // 7.2.x：必须 async，但构造函数不能 async
        _connection = factory
            .CreateConnectionAsync()
            .GetAwaiter()
            .GetResult();

        _channel = _connection
            .CreateChannelAsync(
                new CreateChannelOptions(true,true)
            )
            .GetAwaiter()
            .GetResult();
    }

    public async Task PublishAsync(
        string exchange,
        string routingKey,
        string body,
        IDictionary<string, object>? headers = null,
        CancellationToken ct = default)
    {

        await _confirmWindow.WaitAsync(ct);

        //var localSeq = NextLocalSeq();
        //_pending[localSeq] = msg.Id;


        var props = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            Headers = headers
        };

        var bodyBytes = Encoding.UTF8.GetBytes(body);

        // 7.x 批量确认的高性能写法
        var tasks = new List<Task>();

        try
        {
            // 因为开启了 publisherConfirmationTrackingEnabled = true
            // 这行代码会一直等待，直到服务器返回 Ack 才会继续执行
            await _channel.BasicPublishAsync(
                exchange: exchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: props,
                body: bodyBytes,
                cancellationToken: ct
            );

            // 执行到这里，说明消息已经被服务器确认了
            // 不需要再调用 WaitForConfirmsAsync
        }
        catch (PublishException ex)
        {
            // 如果服务器返回 Nack（拒绝），或者超时，会抛出此异常
            Console.WriteLine($"消息发送失败: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.CloseAsync();

        if (_connection is not null)
            await _connection.CloseAsync();
    }

    private long NextLocalSeq() =>
    Interlocked.Increment(ref _localSeq);
}
