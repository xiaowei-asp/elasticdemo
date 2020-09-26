using SaleRule.DTO;
using SaleRule.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaleRule
{
    public class PromotionEngine
    {
        private readonly List<IRule> _promotions;

        public PromotionEngine(IEnumerable<IRule> promotions)
        {
            _promotions = promotions.OrderBy(p => p.Priority).ToList();
        }

        public (Cart finalCart, List<PromotionResult> applied, decimal orderLevelDiscount) ApplyAll(Cart cart)
        {
            var results = new List<PromotionResult>();

            // 1) 初始化：每个 item 的 AdjustedAmount 清零（以便重算）
            foreach (var item in cart.Items)
            {
                item.AdjustedAmount = 0m;
                item.LockedQuantity = 0;
                item.AppliedPromotions.Clear();
            }

            // 2) 执行促销：按 Priority 顺序执行
            foreach (var promo in _promotions)
            {
                var r = promo.Apply(cart);
                results.Add(r);
            }

            // 3) 任何未被锁定或未计价的商品数量按原价计入（例如某些商品未参与任何促销）
            foreach (var item in cart.Items)
            {
                int remaining = item.Quantity - item.LockedQuantity;
                if (remaining > 0)
                {
                    item.AdjustedAmount += Math.Round(remaining * item.UnitPrice, 2);
                    // 标记为原价结算
                    item.AppliedPromotions.Add("NoPromotion_FullPrice");
                    item.LockedQuantity += remaining;
                }
            }

            // 4) 处理订单级满减（如果有）——注意：上面的满减实现返回折扣，但并没有把它应用到单品
            // 我们在 promotions 列表中已经包含了 FullReductionPromotion，并记录了其 DiscountAmount，但我们 need treat as order-level.
            decimal orderLevelDiscount = results.Where(r => r.PromotionId.StartsWith("FULLREDUCE")).Sum(r => r.DiscountAmount);

            return (cart, results, orderLevelDiscount);
        }
    }


    public static class Demo
    {
        public static void Main()
        {
            // 准备商品
            var A = new Product { Sku = "A", Name = "商品A", Price = 30m };
            var B = new Product { Sku = "B", Name = "商品B", Price = 25m };
            var C = new Product { Sku = "C", Name = "商品C", Price = 100m, IsClearance = false };
            var D = new Product { Sku = "D", Name = "商品D", Price = 40m };
            var milk = new Product { Sku = "M1", Name = "牛奶", Price = 10m };

            // 准备购物车（示例）
            var cart = new Cart
            {
                Items = new List<CartItem>
                {
                    new CartItem { Product = A, Quantity = 5 },
                    new CartItem { Product = B, Quantity = 4 },
                    new CartItem { Product = C, Quantity = 1 },
                    new CartItem { Product = milk, Quantity = 6 },
                }
            };

            // 准备促销规则（优先级 1 小先执行）
            var promotions = new List<IRule>
            {
                //// 组合：A+B 套餐价 = 40（原价 30+25=55, 节省15）
                new ComboRule("COMBO_A_B", priority: 1, skus: new[]{"A","B"}, comboPrice: 40m),

                //// BOGO：对 A/E/F 之类的商品启用（这里示范对 B 启用）
                //new BogoPromotion("BOGO_SPECIAL", priority: 2, predicate: p => p.Sku == "B"),

                //// 多件折扣对同一 SKU：第一件原价、第二件85折、第三件70折、第四件及以上70折（规则3）
                //new MultiQuantityPromotion("MULTI_SAMESKU", priority: 3, discountRates: new decimal[] {1.0m, 0.85m, 0.7m}),

                // 直接折扣：对 C 做 0.8 折（仅作示例）
                new DiscountRule("DIRECT_C_20P", priority: 4, predicate: p => new string[]{ "A"}.Contains(p.Sku) && !p.IsExplosiveDeal, discountRate: 0.8m),

                // 清仓（如果某个商品 IsClearance = true 会被匹配）
                //new DirectDiscountPromotion("CLEARANCE", priority: 4, predicate: p => p.IsClearance, discountRate: 0.5m),

                //// 满减：满100-10, 200-25, 300-40 (订单级) —— 放在最后优先级 10
                //new FullReductionPromotion("FULLREDUCE_1", priority: 10, tiers: new List<(decimal, decimal)> {
                //    (100m,10m),(200m,25m),(300m,40m)
                //})
            };

            var engine = new PromotionEngine(promotions);
            var (finalCart, appliedPromos, orderLevelDiscount) = engine.ApplyAll(cart);

            // 输出结果
            Console.WriteLine("原价合计: " + cart.GetOriginalTotal().ToString("C"));
            Console.WriteLine("商品明细：");
            foreach (var it in finalCart.Items)
            {
                Console.WriteLine($" - {it.Product.Sku} x{it.Quantity} | 单价 {it.UnitPrice:C} | 折后 {it.AdjustedAmount:C} | 促销: {string.Join(",", it.AppliedPromotions)}");
            }
            decimal itemsTotal = finalCart.GetAdjustedTotal();
            Console.WriteLine("商品折后小计: " + itemsTotal.ToString("C"));
            Console.WriteLine("订单级满减: -" + orderLevelDiscount.ToString("C"));
            Console.WriteLine("应付合计: " + (itemsTotal - orderLevelDiscount).ToString("C"));

            Console.WriteLine("\n应用的促销记录：");
            foreach (var r in appliedPromos)
            {
                Console.WriteLine($" - {r.PromotionId}: 折扣 {r.DiscountAmount:C} | 说明: {r.Note}");
            }
        }
    }
}
