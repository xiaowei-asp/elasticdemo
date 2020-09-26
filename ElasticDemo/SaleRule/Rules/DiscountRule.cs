using SaleRule.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaleRule.Rules
{
    public class DiscountRule : IRule
    {
        public string Id { get; }
        public int Priority { get; }
        private readonly Func<Product, bool> _predicate; // 哪些商品被折扣
        private readonly decimal _discountRate; // 例如 0.8 表示 8 折
        //private readonly bool _isExplosive; // 若为爆款则不参与其他活动（由调用方按优先级控制）

        public DiscountRule(string id, int priority, Func<Product, bool> predicate, decimal discountRate)
        {
            Id = id; Priority = priority;
            _predicate = predicate;
            _discountRate = discountRate;
            //_isExplosive = isExplosive;
        }

        public PromotionResult Apply(Cart cart)
        {
            decimal discountSum = 0m;

            if (cart == null || cart.Items.Count <= 0)
                throw new ArgumentException();

            var avaliableItems = cart.Items.Where(c => _predicate(c.Product) && c.AvailableQuantity > 0).ToList();
            if(avaliableItems.Count > 0)
            {
                foreach (var item in avaliableItems)
                {
                    var availableQuantity = item.AvailableQuantity;
                    for (int i = 0; i < availableQuantity; i++)
                    {
                        var needPay = Math.Round(item.UnitPrice * _discountRate, 2);
                        item.AdjustedAmount += needPay;
                        item.LockedQuantity += 1;
                        item.AppliedPromotions.Add($"{Id}");
                        discountSum += Math.Round(item.UnitPrice - needPay, 2);
                    }
                    //if (_isExplosive)
                    //{
                    //    item.Product.IsExplosiveDeal = true;
                    //}
                }
            }
            
            return new PromotionResult { PromotionId = Id, DiscountAmount = Math.Round(discountSum, 2), Note = "直接折扣应用" };
        }
    }
}
