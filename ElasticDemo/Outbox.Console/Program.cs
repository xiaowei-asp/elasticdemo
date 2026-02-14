using Confluent.Kafka;
using Outbox.Core;
using RabbitMQ.Client;

namespace Outbox
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //TestKafkaPublisher().GetAwaiter().GetResult();
            TestRabbitPublisher().GetAwaiter().GetResult();

            Console.WriteLine("Hello, World!");
        }


        private static async Task TestKafkaPublisher()
        {
            // 1. 配置
            var config = new ProducerConfig
            {
                BootstrapServers = "localhost:9092",
                ClientId = "MyServiceProducer",
                Acks = Acks.All, // 强一致性，对应 RabbitMQ 的 Persistent + Confirm
                MessageTimeoutMs = 5000 // 5秒超时
            };

            // 2. 注入
            IMqProducer producer = new KafkaProducer(config);

            // 3. 发送
            await producer.PublishAsync(
                exchange: "order-events",  // Topic
                routingKey: "order-10086", // Key (保证同一订单的消息在同一分区有序)
                body: "{ 'id': 10086, 'status': 'created' }"
            );
        }

        private static async Task TestRabbitPublisher()
        {
            // 1. 准备连接工厂
            var factory = new ConnectionFactory
            {
                HostName = "localhost", // RabbitMQ 服务器地址
                UserName = "guest",
                Password = "guest",
                Port = 5672,
                // 7.x 建议配置 DispatchConsumersAsync，虽然 Producer 用不到，但保持习惯
                //DispatchConsumersAsync = true
            };

            // 2. 实例化 Producer (注意：你的构造函数里建立了连接)
            // 使用 await using 确保结束时自动调用 DisposeAsync 关闭连接
            await using var producer = new RabbitMqProducer(factory);

            Console.WriteLine("开始发送消息...");

            // 3. 发送简单消息
            // 参数: Exchange名, RoutingKey, 消息体
            await producer.PublishAsync(
                exchange: "amq.direct", // 确保这个 Exchange 在 RabbitMQ 中存在！
                routingKey: "test-key",
                body: "Hello RabbitMQ 7.0!"
            );

            // 4. 发送带 Header 的消息
            var headers = new Dictionary<string, object>
            {
                { "message-id", Guid.NewGuid().ToString() },
                { "source", "console-app" }
            };
        }
    }
}
