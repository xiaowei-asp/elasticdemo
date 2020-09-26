using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaleRule.DTO
{
    public class Product
    {
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public bool IsClearance { get; set; } = false; // 规则7
        public bool IsExplosiveDeal { get; set; } = false; // 规则6 的爆款标记（不参与其他）
    }
}
