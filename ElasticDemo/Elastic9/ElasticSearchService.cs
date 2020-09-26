using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Transport;
using Elastic9;

public class ElasticSearchService
{
    private readonly ElasticsearchClient _client;
    private readonly string _indexName = "products";

    public ElasticSearchService(string uri, string username = "elastic", string password = "changeme")
    {
        var settings = new ElasticsearchClientSettings(new Uri(uri))
            .Authentication(new BasicAuthentication(username, password))
            .DefaultIndex(_indexName);

        _client = new ElasticsearchClient(settings);
    }

    /// <summary>
    /// 创建索引并配置映射
    /// </summary>
    public async Task CreateIndexAsync()
    {
        var exists = await _client.Indices.ExistsAsync(_indexName);
        if (exists.Exists) return;

        var response = await _client.Indices.CreateAsync(_indexName, c => c
            .Settings(s => s
                .NumberOfShards(3)
                .NumberOfReplicas(1)
                .RefreshInterval("30s")
            )
            .Mappings(m => m
                .Properties<Product>(ps => ps
                    .Keyword(k => k.Id)
                    .Text(t => t.Name, t => t.Analyzer("standard"))
                    .Keyword(k => k.Category)
                    .FloatNumber(n => n.Price)
                    .Date(d => d.CreatedAt, f => f.Format("yyyy-MM-dd HH:mm:ss||epoch_millis"))
                    .Boolean(b => b.IsActive)
                )
            )
        );

        if (!response.IsValidResponse)
        {
            throw new Exception($"创建索引失败: {response.DebugInformation}");
        }
    }

    /// <summary>
    /// 单条插入
    /// </summary>
    public async Task InsertAsync(Product product)
    {
        var response = await _client.IndexAsync(product, _indexName);
        if (!response.IsValidResponse)
        {
            throw new Exception($"写入失败: {response.DebugInformation}");
        }
    }

    /// <summary>
    /// 批量写入
    /// </summary>
    public async Task BulkInsertAsync(IEnumerable<Product> products)
    {
        var response = await _client.BulkAsync(b => b
            .Index(_indexName)
            .CreateMany(products)
        );

        if (response.Errors)
        {
            var errors = string.Join("\n", response.ItemsWithErrors.Select(e => e.Error.Reason));
            throw new Exception($"批量写入失败: {errors}");
        }
    }

    /// <summary>
    /// 更新文档
    /// </summary>
    public async Task UpdateAsync(Product product)
    {
        var response = await _client.UpdateAsync<Product, Product>(_indexName,product.Id ,u => u
            .Index(_indexName)
            .Doc(product)
        );

        if (!response.IsValidResponse)
        {
            throw new Exception($"更新失败: {response.DebugInformation}");
        }
    }

    /// <summary>
    /// 删除文档
    /// </summary>
    public async Task DeleteAsync(string id)
    {
        var response = await _client.DeleteAsync<Product>(id, d => d.Index(_indexName));
        if (!response.IsValidResponse)
        {
            throw new Exception($"删除失败: {response.DebugInformation}");
        }
    }

    /// <summary>
    /// 按名称搜索
    /// </summary>
    public async Task<IEnumerable<Product>> SearchByNameAsync(string keyword)
    {
        var response = await _client.SearchAsync<Product>(s => s
            .Indices(_indexName)
            .Query(q => q.Match(m => m.Field(f => f.Name).Query(keyword)))
        );

        return response.Documents;
    }
}
