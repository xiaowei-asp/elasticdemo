using System.Text.Json.Serialization;

namespace Elastic9
{
    public class Product
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("price")]
        public double Price { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }

        // 如果有嵌套对象，例如规格
        [JsonPropertyName("specs")]
        public Dictionary<string, string> Specs { get; set; }
    }
}
