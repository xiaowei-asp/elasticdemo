// 使用 .NET 7+ 语法
using System;
using System.Collections.Generic;
using System.Linq;

namespace SaleRule2
{
    #region Models
    public class Product
    {
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public bool IsClearance { get; set; } = false; // 规则7
        public bool IsExplosiveDeal { get; set; } = false; // 规则6 的爆款标记（不参与其他）
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

    public class Cart
    {
        public List<CartItem> Items { get; set; } = new();
        public decimal GetOriginalTotal() => Items.Sum(i => i.UnitPrice * i.Quantity);
        public decimal GetAdjustedTotal() => Items.Sum(i => i.AdjustedAmount);
    }

    public class PromotionResult
    {
        public string PromotionId { get; set; } = "";
        public decimal DiscountAmount { get; set; } = 0m; // 减少的金额
        public string Note { get; set; } = "";
    }
    #endregion

    #region Promotion Abstraction
    public interface IPromotion
    {
        string Id { get; }
        int Priority { get; } // 小的先执行
        PromotionResult Apply(Cart cart);
    }
    #endregion

    #region Promotion Implementations

    // 规则1：组合套餐（优先）
    public class ComboPromotion : IPromotion
    {
        public string Id { get; }
        public int Priority { get; }
        // 定义组合：sku 列表 -> 套餐总价
        private readonly List<string> _skus;
        private readonly decimal _comboPrice;

        public ComboPromotion(string id, int priority, IEnumerable<string> skus, decimal comboPrice)
        {
            Id = id; Priority = priority;
            _skus = skus.ToList();
            _comboPrice = comboPrice;
        }

        public PromotionResult Apply(Cart cart)
        {
            // 计算每个 sku 的可用数量（未被锁定）
            var itemsMap = cart.Items.ToDictionary(i => i.Product.Sku, i => i);

            // 找到该组合中每个 sku 的可用数量
            int possibleTimes = int.MaxValue;
            foreach (var sku in _skus)
            {
                if (!itemsMap.ContainsKey(sku) || itemsMap[sku].AvailableQuantity <= 0)
                {
                    possibleTimes = 0; break;
                }
                possibleTimes = Math.Min(possibleTimes, itemsMap[sku].AvailableQuantity);
            }

            if (possibleTimes <= 0)
                return new PromotionResult { PromotionId = Id, DiscountAmount = 0m, Note = "不满足组合条件" };

            // 若能够应用多次（例如买多个组合），按 possibleTimes 次数处理
            decimal discountTotal = 0m;
            for (int t = 0; t < possibleTimes; t++)
            {
                // 计算组合中原价总和
                decimal sumOrig = 0m;
                foreach (var sku in _skus)
                {
                    var it = itemsMap[sku];
                    sumOrig += it.UnitPrice;
                }
                decimal discount = Math.Round(sumOrig - _comboPrice, 2);
                if (discount < 0) discount = 0m;

                // 分摊套餐价到每个商品（按单价占比）
                decimal sumUnit = _skus.Sum(s => itemsMap[s].UnitPrice);
                foreach (var sku in _skus)
                {
                    var it = itemsMap[sku];
                    // 标记锁定一件
                    it.LockedQuantity += 1;

                    // 分摊逻辑（简单按占比）
                    decimal share = sumUnit > 0 ? (it.UnitPrice / sumUnit) * _comboPrice : _comboPrice / _skus.Count;
                    it.AdjustedAmount += Math.Round(share, 2);
                    it.AppliedPromotions.Add($"{Id} (combo)");
                }
                discountTotal += discount;
            }

            return new PromotionResult { PromotionId = Id, DiscountAmount = Math.Round(discountTotal, 2), Note = $"应用组合{possibleTimes}次" };
        }
    }

    // 规则2：买一送一（BOGO）
    public class BogoPromotion : IPromotion
    {
        public string Id { get; }
        public int Priority { get; }
        private readonly Func<Product, bool> _predicate; // 哪些商品参与BOGO

        public BogoPromotion(string id, int priority, Func<Product, bool> predicate)
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

    // 规则3：多件折扣（同一 SKU）
    public class MultiQuantityPromotion : IPromotion
    {
        public string Id { get; }
        public int Priority { get; }
        // 支持两种模式：按序折扣（第N件不同折扣） 或 买N送1（N->free)
        private readonly bool _isGiveOneForBuyN;
        private readonly int _buyN;
        private readonly decimal[] _discountRates; // 举例: [1.0m,0.85m,0.7m,0.7m...]

        public MultiQuantityPromotion(string id, int priority, decimal[] discountRates)
        {
            Id = id; Priority = priority;
            _discountRates = discountRates;
            _isGiveOneForBuyN = false;
        }

        public MultiQuantityPromotion(string id, int priority, int buyNForOneFree)
        {
            Id = id; Priority = priority;
            _buyN = buyNForOneFree;
            _isGiveOneForBuyN = true;
        }

        public PromotionResult Apply(Cart cart)
        {
            decimal discountSum = 0m;
            foreach (var item in cart.Items.Where(i => i.AvailableQuantity > 0 && !i.Product.IsClearance && !i.Product.IsExplosiveDeal))
            {
                if (_isGiveOneForBuyN)
                {
                    int groups = item.AvailableQuantity / _buyN;
                    if (groups <= 0) continue;
                    // 每组赠送 1 件 -> 折扣为最便宜的那件价格（同 SKU 则为 unitPrice）
                    int freeCount = groups;
                    item.LockedQuantity += freeCount; // 这 free 数量视为占用
                    // 计：已付数量 = AvailableQuantity (不改变付款件数，但我们将免费的单独计为 0)
                    // 更好的方法是：对每个被赠送的位置把 AdjustedAmount 加 0，并把被赠送的按 unitPrice 折算为 discount
                    item.AdjustedAmount += item.UnitPrice * (item.AvailableQuantity - freeCount);
                    item.AppliedPromotions.Add($"{Id} buy{_buyN}get1");
                    discountSum += item.UnitPrice * freeCount;
                }
                else
                {
                    // 按序折扣
                    int avail = item.AvailableQuantity;
                    for (int i = 0; i < avail; i++)
                    {
                        decimal rate = i < _discountRates.Length ? _discountRates[i] : _discountRates.Last();
                        decimal mustPay = Math.Round(item.UnitPrice * rate, 2);
                        item.AdjustedAmount += mustPay;
                        item.LockedQuantity += 1;
                        item.AppliedPromotions.Add($"{Id} x{i + 1}");
                        discountSum += Math.Round(item.UnitPrice - mustPay, 2);
                    }
                }
            }

            return new PromotionResult { PromotionId = Id, DiscountAmount = Math.Round(discountSum, 2), Note = "多件优惠应用完毕" };
        }
    }

    // 规则5：满减（订单级）
    public class FullReductionPromotion : IPromotion
    {
        public string Id { get; }
        public int Priority { get; }
        // 支持分档，例如 [(100,10),(200,25),(300,40)]
        private readonly List<(decimal threshold, decimal minus)> _tiers;

        public FullReductionPromotion(string id, int priority, List<(decimal threshold, decimal minus)> tiers)
        {
            Id = id; Priority = priority;
            _tiers = tiers.OrderBy(t => t.threshold).ToList();
        }

        public PromotionResult Apply(Cart cart)
        {
            // 计算当前已调整的商品总价（已考虑前面商品级折扣）
            decimal currentTotal = cart.GetAdjustedTotal();
            // 找出最高满足级别
            var applicable = _tiers.Where(t => currentTotal >= t.threshold).OrderByDescending(t => t.threshold).FirstOrDefault();
            if (applicable == default)
                return new PromotionResult { PromotionId = Id, DiscountAmount = 0m, Note = "未触达满减门槛" };

            decimal minus = applicable.minus;
            // 将减免按商品占比分摊到各商品 adjustedAmount（或直接在订单层减）
            // 这里简单做为订单层减免（不变更单品 adjusted），并返回折扣信息。
            return new PromotionResult { PromotionId = Id, DiscountAmount = minus, Note = $"订单满{applicable.threshold}减{minus}" };
        }
    }

    // 规则6/7：直接降价 / 清仓（优先于订单级）
    public class DirectDiscountPromotion : IPromotion
    {
        public string Id { get; }
        public int Priority { get; }
        private readonly Func<Product, bool> _predicate; // 哪些商品被折扣
        private readonly decimal _discountRate; // 例如 0.8 表示 8 折
        private readonly bool _isExplosive; // 若为爆款则不参与其他活动（由调用方按优先级控制）

        public DirectDiscountPromotion(string id, int priority, Func<Product, bool> predicate, decimal discountRate, bool isExplosive = false)
        {
            Id = id; Priority = priority;
            _predicate = predicate;
            _discountRate = discountRate;
            _isExplosive = isExplosive;
        }

        public PromotionResult Apply(Cart cart)
        {
            decimal discountSum = 0m;
            foreach (var item in cart.Items.Where(i => _predicate(i.Product) && i.AvailableQuantity > 0))
            {
                // 如果商品标记为清仓，则覆盖（由 predicate 控制）
                for (int q = 0; q < item.AvailableQuantity; q++)
                {
                    decimal pay = Math.Round(item.UnitPrice * _discountRate, 2);
                    item.AdjustedAmount += pay;
                    item.LockedQuantity += 1;
                    item.AppliedPromotions.Add($"{Id}");
                    discountSum += Math.Round(item.UnitPrice - pay, 2);
                }
                if (_isExplosive)
                {
                    item.Product.IsExplosiveDeal = true;
                }
            }

            return new PromotionResult { PromotionId = Id, DiscountAmount = Math.Round(discountSum, 2), Note = "直接折扣应用" };
        }
    }

    #endregion

    #region Engine
    public class PromotionEngine
    {
        private readonly List<IPromotion> _promotions;

        public PromotionEngine(IEnumerable<IPromotion> promotions)
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
    #endregion

    #region Demo Run
    public static class Demo2
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
            var promotions = new List<IPromotion>
            {
                //// 组合：A+B 套餐价 = 40（原价 30+25=55, 节省15）
                new ComboPromotion("COMBO_A_B", priority: 1, skus: new[]{"A","B"}, comboPrice: 40m),

                //// BOGO：对 A/E/F 之类的商品启用（这里示范对 B 启用）
                //new BogoPromotion("BOGO_SPECIAL", priority: 2, predicate: p => p.Sku == "B"),

                //// 多件折扣对同一 SKU：第一件原价、第二件85折、第三件70折、第四件及以上70折（规则3）
                //new MultiQuantityPromotion("MULTI_SAMESKU", priority: 3, discountRates: new decimal[] {1.0m, 0.85m, 0.7m}),

                // 直接折扣：对 C 做 0.8 折（仅作示例）
                new DirectDiscountPromotion("DIRECT_C_20P", priority: 4, predicate: p => p.Sku=="A", discountRate: 0.8m),

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
    #endregion
}
