using Confluent.Kafka;
using Outbox.Core;
using System.Collections.Concurrent;
using System.Text;

public sealed class KafkaProducer : IMqProducer, IAsyncDisposable
{
    // Confluent.Kafka 的 Producer 实例，Key 和 Value 这里假设都处理为 String
    // 实际生产中 Value 往往是 byte[] 或序列化后的 JSON
    private readonly IProducer<string, string> _producer;

    // 用于控制并发请求数的信号量，模仿你的 RabbitMQ 实现
    private readonly SemaphoreSlim _confirmWindow;

    public KafkaProducer(ProducerConfig config)
    {
        // 1. 初始化信号量
        _confirmWindow = new SemaphoreSlim(initialCount: 10000, maxCount: 10000);

        // 2. 配置 Producer
        // 关键配置说明：
        // Acks.All: 等同于 RabbitMQ 的 Publisher Confirms，确保所有 ISR 副本都写入成功才返回
        // EnableIdempotence: 开启幂等性（类似 RabbitMQ 的 Deduplication），防止重试导致重复
        if (config.Acks == null) 
            config.Acks = Acks.All;

        // 3. 创建 Producer 实例
        // Kafka 的 ProducerBuilder 是同步的，不需要像 RabbitMQ 那样写 .GetAwaiter().GetResult()
        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => Console.WriteLine($"Kafka Error: {e.Reason}"))
            .Build();
    }

    public async Task PublishAsync(
        string exchange,    // 在 Kafka 中，这里对应 Topic
        string routingKey,  // 在 Kafka 中，这里对应 Key (决定消息去哪个分区)
        string body,
        IDictionary<string, object>? headers = null,
        CancellationToken ct = default)
    {
        // 1. 获取信号量（背压控制）
        await _confirmWindow.WaitAsync(ct);

        try
        {
            // 2. 构建 Kafka 消息
            var kafkaMessage = new Message<string, string>
            {
                Key = routingKey,
                Value = body,
                Headers = ConvertHeaders(headers)
            };

            // 3. 异步发送并等待确认
            // ProduceAsync 会等待 Kafka Broker 的 Ack (基于配置的 Acks.All)
            // 如果发送失败，这里会直接抛出 ProduceException
            var deliveryResult = await _producer.ProduceAsync(exchange, kafkaMessage, ct);

            // 4. (可选) 处理确认后的逻辑
            // deliveryResult.Status 通常是 Persisted
            // deliveryResult.Offset 是消息的位移
            // Console.WriteLine($"消息已发送至分区 {deliveryResult.Partition}, Offset: {deliveryResult.Offset}");
        }
        catch (ProduceException<string, string> ex)
        {
            // 对应 RabbitMQ 的 PublishException
            Console.WriteLine($"Kafka 发送失败: ErrorCode={ex.Error.Code}, Reason={ex.Error.Reason}");
            throw; // 或者记录日志后吞掉
        }
        catch (OperationCanceledException)
        {
            // 处理超时或取消
            Console.WriteLine("发送操作被取消");
            throw;
        }
        finally
        {
            // 5. 释放信号量
            _confirmWindow.Release();
        }
    }

    // Kafka 的 Header 是 byte[]，需要转换一下
    private Headers ConvertHeaders(IDictionary<string, object>? source)
    {
        if (source == null) return null;

        var kafkaHeaders = new Headers();
        foreach (var kvp in source)
        {
            if (kvp.Value == null) continue;

            // 简单处理：将值转为 string 再转 byte[]
            // 实际场景可能需要根据 kvp.Value 的类型做更细致的序列化
            byte[] valBytes = kvp.Value is byte[] bytes
                ? bytes
                : Encoding.UTF8.GetBytes(kvp.Value.ToString() ?? "");

            kafkaHeaders.Add(kvp.Key, valBytes);
        }
        return kafkaHeaders;
    }

    public async ValueTask DisposeAsync()
    {
        if (_producer != null)
        {
            // Flush 确保本地队列中的消息都发送出去，类似 RabbitMQ Channel Close 前的等待
            // 这里给 10 秒超时
            _producer.Flush(TimeSpan.FromSeconds(10));

            _producer.Dispose();
        }

        _confirmWindow.Dispose();

        await Task.CompletedTask;
    }
}