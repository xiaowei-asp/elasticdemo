using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaleRule.DTO
{
    public class Cart
    {
        public List<CartItem> Items { get; set; } = [];
        public decimal GetOriginalTotal() => Items.Sum(i => i.UnitPrice * i.Quantity);
        public decimal GetAdjustedTotal() => Items.Sum(i => i.AdjustedAmount);
        
    }

    public class CartItem
    {
        public Product Product { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal UnitPrice => Product.Price;
        // 锁定数量：多少数量已被促销占用（不再参与其他促销）
        public int LockedQuantity { get; set; } = 0;

        // 记录最终应付金额（促销会把折后金额累加到 AdjustedAmount）
        public decimal AdjustedAmount { get; set; } = 0m;

        // 可用于显示促销标签
        public List<string> AppliedPromotions { get; set; } = new();

        public int FreeQuantity => Math.Max(0, Quantity - (Quantity - LockedQuantity)); // 不常用
        public int AvailableQuantity => Quantity - LockedQuantity;
    }
}
