using SaleRule.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaleRule.Rules
{
    /// <summary>
    /// 买一送一规则
    /// </summary>
    public class BogoRule : IRule
    {
        public string Id { get; set; }

        public int Priority { get; set; }

        private readonly Func<Product, bool> _predicate; // 哪些商品参与BOGO

        public BogoRule(string id, int priority, Func<Product, bool> predicate)
        {
            Id = id; Priority = priority;
            _predicate = predicate;
        }

        public PromotionResult Apply(Cart cart)
        {
            // 收集所有参与BOGO的 CartItem 的逐件价格（展开成单件列表）
            var pieceList = new List<(CartItem item, decimal unitPrice)>();
            foreach (var item in cart.Items.Where(i => _predicate(i.Product) && i.AvailableQuantity > 0))
            {
                for (int q = 0; q < item.AvailableQuantity; q++)
                    pieceList.Add((item, item.UnitPrice));
            }

            if (!pieceList.Any())
                return new PromotionResult { PromotionId = Id, DiscountAmount = 0m, Note = "无参活动商品" };

            // 按单价降序排序然后两两配对
            pieceList = pieceList.OrderByDescending(x => x.unitPrice).ToList();
            decimal discount = 0m;
            int idx = 0;
            while (idx + 1 < pieceList.Count)
            {
                var high = pieceList[idx];
                var low = pieceList[idx + 1];

                // 计价：高价计费，低价为 0 => 折扣等于低.unitPrice
                low.item.LockedQuantity += 1;
                high.item.LockedQuantity += 1;

                // high pays full price (add to adjusted), low pays 0
                high.item.AdjustedAmount += high.unitPrice;
                high.item.AppliedPromotions.Add($"{Id}");
                low.item.AdjustedAmount += 0m;
                low.item.AppliedPromotions.Add($"{Id} (free)");

                discount += low.unitPrice;
                idx += 2;
            }

            // 若有剩余单件（奇数）——按原价结算
            if (idx < pieceList.Count)
            {
                var remain = pieceList[idx];
                remain.item.LockedQuantity += 1;
                remain.item.AdjustedAmount += remain.unitPrice;
                remain.item.AppliedPromotions.Add($"{Id} (odd-item full price)");
            }

            return new PromotionResult { PromotionId = Id, DiscountAmount = Math.Round(discount, 2), Note = $"BOGO 共优惠 {discount:C}" };
        }
    }
}
