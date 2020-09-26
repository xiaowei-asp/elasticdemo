using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.Inference;
using Elastic.Clients.Elasticsearch.Ingest;
using System.Diagnostics;
using System.Threading.Channels;

namespace Elastic9
{
    public class ProductService
    {
        private readonly ElasticsearchClient _client;
        private const string IndexName = "products";  // 索引名称

        public ProductService(ElasticsearchClient client)
        {
            _client = client;
        }

        // 创建索引并配置映射（在应用启动或首次使用时调用）
        public async Task CreateIndexWithMappingAsync()
        {
            var existsResponse = await _client.Indices.ExistsAsync(IndexName);
            if (existsResponse.Exists)
            {
                // 如果存在，可选择删除或更新映射
                // await _client.Indices.DeleteAsync(IndexName);  // 谨慎使用
                return;  // 或更新映射
            }

            // 创建索引请求
            var createResponse = await _client.Indices.CreateAsync<Product>(IndexName, c => c
                .Settings(s => s
                    .NumberOfShards(3)  // 分片数，根据数据规模调整
                    .NumberOfReplicas(1)  // 副本数
                    .Analysis(a => a  // 配置分析器
                        .Analyzers(an => an
                            .Custom("custom_analyzer", ca => ca
                                .Tokenizer("standard")
                                .Filter(new[] { "lowercase" })  // 示例：小写过滤
                            )
                        )
                        
                    )
                )
                .Mappings(m => m  // 定义字段映射
                    .Properties(p => p
                        .Keyword(k => k.Id)  // ID 作为 keyword（精确匹配）
                        .Text(t => t.Name,text => text.Analyzer("custom_analyzer"))  // 名称作为 text，支持全文搜索
                        .DoubleNumber(d => d.Price)  // 价格作为 double
                        .Keyword(k => k.Category)  // 类别精确匹配
                        .Date(d => d.CreatedAt)  // 日期类型
                        .Nested(o => o.Specs)  // 嵌套对象
                    )
                )
            );

            if (!createResponse.IsValidResponse)
            {
                throw new Exception($"Failed to create index: {createResponse.DebugInformation}");
            }
        }

        public async Task InitDataToElasticSearch()
        {
            var products = new List<Product>
            {
                new Product
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Apple iPhone 13",
                    Category = "Electronics",
                    Price = 999.99,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Specs = new Dictionary<string, string>
                    {
                        { "Color", "Black" },
                        { "Storage", "128GB" }
                    }
                },
                new Product
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Samsung Galaxy S21",
                    Category = "Electronics",
                    Price = 799.99,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Specs = new Dictionary<string, string>
                    {
                        { "Color", "White" },
                        { "Storage", "256GB" }
                    }
                },
                // 添加更多产品...
            };
            var process = Process.GetCurrentProcess();
            var processedCount = 0;
            var channelOptions = new BoundedChannelOptions(100) // 设置通道容量
            {
                FullMode = BoundedChannelFullMode.Wait // 当通道满时等待
            };
            var chanel = Channel.CreateBounded<List<Product>>(channelOptions);

            // 启动消费者任务
            var consumerTask = Task.Run(async () =>
            {
                await foreach (var batch in chanel.Reader.ReadAllAsync())
                {
                    processedCount += batch.Count;
                    var tcs = new TaskCompletionSource<bool>();
                    using var bulk = _client.BulkAll(batch, b =>
                        b.Index(IndexName)
                        .BackOffRetries(2)
                        .BackOffTime("30s")
                        .RefreshOnCompleted()
                        .MaxDegreeOfParallelism(5)
                        .Size(2000)
                    );

                    bulk.Subscribe(new BulkAllObserver(
                        onNext: (b) => { },
                        onError: (e) => tcs.TrySetException(e),
                        onCompleted: () => tcs.TrySetResult(true)
                    ));

                }
            });
            // 生产者任务
            var batchSize = 1000; // 每批次处理50条数据
            var total = products.Count;
            var pageCount = (int)Math.Ceiling(total * 1.0 / batchSize);
            var skip = 0;
            var take = batchSize;
            for (int i = 0; i < pageCount; i ++)
            {
                skip = i * batchSize;
                take = Math.Min(batchSize, total - skip);
                var batch = products.GetRange(skip, take).ToList();
                await chanel.Writer.WriteAsync(batch);
            }
            chanel.Writer.Complete(); // 完成写入
            await consumerTask; // 等待消费者完成

        }
    }
}