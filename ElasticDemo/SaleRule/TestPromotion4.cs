// 文件: PromotionEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.Arm;

namespace SaleRule4
{
    #region Models
    public record Product(string Sku, decimal Price, bool IsClearance = false, int? LimitPerCustomer = null, decimal? MemberPrice = null);

    public record CartItem(string Sku, int Quantity);

    public class SingleUnit
    {
        public string Sku { get; }
        public decimal Price { get; }
        public Product ProductRef { get; }
        public int Index { get; } // unique id in expanded list

        public SingleUnit(string sku, decimal price, Product productRef, int index)
        {
            Sku = sku;
            Price = price;
            ProductRef = productRef;
            Index = index;
        }
    }

    public record PromoApplicationResult(decimal TotalPrice, List<string> AppliedMessages);
    #endregion

    #region Promotion Abstractions
    public abstract class Promotion
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public int Priority { get; init; } = 0; // higher priority tried earlier (heuristic)

        // Given remaining units (not yet consumed), try to find all *possible* ways to apply this promotion;
        // return a list of tuples: (consumedUnitIndexes, priceToPayForThoseConsumedUnits, textualMessage)
        public abstract List<(List<int> consumedIndexes, decimal price, string message)> FindApplications(List<SingleUnit> availableUnits);
    }
    #endregion

    #region Concrete Promotions

    // Rule1: Bundle promotion (e.g., A + B for fixed bundle price)
    public class BundlePromotion : Promotion
    {
        public List<string> RequiredSkus { get; init; } = new();
        public decimal BundlePrice { get; init; }

        public override List<(List<int>, decimal, string)> FindApplications(List<SingleUnit> availableUnits)
        {
            // We need to find combinations that cover RequiredSkus (1 each)
            var result = new List<(List<int>, decimal, string)>();

            // For each required sku, collect available unit indexes
            var candidatesPerSku = RequiredSkus
                .Select(sku => availableUnits.Where(u => u.Sku == sku).ToList())
                .ToList();

            // If any required sku has no candidate, no application
            if (candidatesPerSku.Any(c => c.Count == 0)) return result;

            // Build Cartesian product (but RequiredSkus length typically small)
            IEnumerable<List<SingleUnit>> cartesian = new[] { new List<SingleUnit>() }.AsEnumerable();
            foreach (var list in candidatesPerSku)
            {
                cartesian = cartesian.SelectMany(
                    existing => list.Select
                    (
                        item => 
                        { 
                            var copy = new List<SingleUnit>(existing); 
                            copy.Add(item); return copy; 
                        })
                    );
            }

            foreach (var combo in cartesian)
            {
                // ensure distinct units (different indexes)
                if (combo.Select(u => u.Index).Distinct().Count() != combo.Count) continue;

                var consumed = combo.Select(u => u.Index).ToList();
                var message = $"Bundle '{Name}' applied on [{string.Join(",", combo.Select(u => u.Sku))}] -> pay {BundlePrice}";
                result.Add((consumed, BundlePrice, message));
            }

            return result;
        }
    }

    // Rule2: BOGO across a SKU set
    public class BogoPromotion : Promotion
    {
        public HashSet<string> EligibleSkus { get; init; } = new();
        // Note: BOGO pairs highest price with next highest free
        public override List<(List<int>, decimal, string)> FindApplications(List<SingleUnit> availableUnits)
        {
            var result = new List<(List<int>, decimal, string)>();

            var eligible = availableUnits.Where(u => EligibleSkus.Contains(u.Sku)).OrderByDescending(u => u.Price).ToList();
            if (eligible.Count < 2) return result;

            // We can apply on any pair among eligible units. To keep branching reasonable,
            // we propose to consider applying to the top two, but we should also consider pairs shifting.
            // For correctness, we'll generate pairings by picking any two different units.
            for (int i = 0; i < eligible.Count; i++)
            {
                for (int j = i + 1; j < eligible.Count; j++)
                {
                    var high = eligible[i];
                    var low = eligible[j];
                    // pay high.Price, low free
                    var consumed = new List<int> { high.Index, low.Index };
                    var price = high.Price;
                    var message = $"BOGO '{Name}' applied on [{high.Sku}({high.Price}), {low.Sku}({low.Price})] -> pay {price}";
                    result.Add((consumed, price, message));
                }
            }

            return result;
        }
    }

    // Rule3: Multi-item discount for same SKU (阶梯折扣)
    public class MultiItemDiscountPromotion : Promotion
    {
        public string Sku { get; init; }
        // discountRates: index1 -> rate for 1st (index 0 = first item rate?), but we'll apply based on ordinal
        // For simplicity specify as array: [1.0m, 0.85m, 0.7m, 0.7m]
        public decimal[] DiscountRates { get; init; } = new decimal[] { 1m, 0.85m, 0.7m };

        public override List<(List<int>, decimal, string)> FindApplications(List<SingleUnit> availableUnits)
        {
            var result = new List<(List<int>, decimal, string)>();
            var eligible = availableUnits.Where(u => u.Sku == Sku).OrderBy(u => u.Index).ToList();
            if (eligible.Count == 0) return result;

            // For multi-item discounts, an application could consume anywhere from 1 up to eligible.Count units.
            // We'll generate options for group sizes 1..eligible.Count (but usually limited size).
            for (int take = 1; take <= eligible.Count; take++)
            {
                var consumeUnits = eligible.Take(take).ToList();
                decimal total = 0m;
                for (int k = 0; k < take; k++)
                {
                    decimal rate = (k < DiscountRates.Length) ? DiscountRates[k] : DiscountRates.Last();
                    total += Math.Round(consumeUnits[k].Price * rate, 2);
                }
                var consumedIdx = consumeUnits.Select(u => u.Index).ToList();
                var msg = $"MultiDiscount '{Name}' on {Sku} x{take} -> pay {total}";
                result.Add((consumedIdx, total, msg));
            }

            return result;
        }
    }

    // Rule5: Threshold promotion (order-level). We treat this separately at the end as it applies to remaining subtotal.
    public class ThresholdPromotion : Promotion
    {
        public List<(decimal Threshold, decimal Discount)> Tiers { get; init; } = new();

        // threshold promos don't directly consume single units; instead they compute adjustment on subtotal.
        public override List<(List<int>, decimal, string)> FindApplications(List<SingleUnit> availableUnits)
        {
            // Not applicable in the per-unit consumption stage.
            return new List<(List<int>, decimal, string)>();
        }

        public (decimal discount, string message) ComputeDiscount(decimal subtotal)
        {
            // choose best discount (or stacking behavior if required; here choose max single-tier discount applicable)
            var applicable = Tiers.Where(t => subtotal >= t.Threshold).OrderByDescending(t => t.Discount).FirstOrDefault();
            if (applicable == default) return (0m, "No threshold applied");
            return (applicable.Discount, $"Threshold applied: -{applicable.Discount} for reach {applicable.Threshold}");
        }
    }

    // Rule6: Direct discount / single item special price
    public class DirectDiscountPromotion : Promotion
    {
        public string Sku { get; init; }
        public decimal? FixedPrice { get; init; } // if set, item price becomes this
        public decimal? DiscountRate { get; init; } // else apply percentage
        public int? LimitPerCustomer { get; init; } = null; // optional

        public override List<(List<int>, decimal, string)> FindApplications(List<SingleUnit> availableUnits)
        {
            var result = new List<(List<int>, decimal, string)>();
            var eligible = availableUnits.Where(u => u.Sku == Sku).OrderBy(u => u.Index).ToList();
            if (eligible.Count == 0) return result;

            int limit = LimitPerCustomer ?? eligible.Count;
            // Each application can take 1 unit (we'll create options to take 1..limit)
            for (int k = 1; k <= Math.Min(limit, eligible.Count); k++)
            {
                var units = eligible.Take(k).ToList();
                decimal total = 0;
                foreach (var u in units)
                {
                    total += FixedPrice ?? Math.Round(u.Price * (DiscountRate ?? 1m), 2);
                }
                result.Add((units.Select(u => u.Index).ToList(), total, $"DirectDiscount '{Name}' on {Sku} x{units.Count} -> pay {total}"));
            }

            return result;
        }
    }

    // Rule7: Clearance => acts like direct discount but forbids other promos (we'll mark product.IsClearance and engine will prevent combos).
    public class ClearancePromotion : Promotion
    {
        public string Sku { get; init; }
        public decimal ClearancePrice { get; init; }

        public override List<(List<int>, decimal, string)> FindApplications(List<SingleUnit> availableUnits)
        {
            var result = new List<(List<int>, decimal, string)>();
            var eligible = availableUnits.Where(u => u.Sku == Sku).OrderBy(u => u.Index).ToList();
            if (eligible.Count == 0) return result;
            // allow grouping 1..n units
            for (int k = 1; k <= eligible.Count; k++)
            {
                var units = eligible.Take(k).ToList();
                decimal total = units.Count * ClearancePrice;
                result.Add((units.Select(u => u.Index).ToList(), total, $"Clearance '{Name}' on {Sku} x{units.Count} -> pay {total}"));
            }
            return result;
        }
    }

    #endregion

    #region Engine
    public class PromotionEngine
    {
        private readonly List<Product> _products;
        private readonly List<Promotion> _promotions;
        private readonly ThresholdPromotion _thresholdPromo; // optional

        public PromotionEngine(List<Product> products, List<Promotion> promotions, ThresholdPromotion thresholdPromotion = null)
        {
            _products = products;
            _promotions = promotions.OrderByDescending(p => p.Priority).ToList(); // try high priority first
            _thresholdPromo = thresholdPromotion;
        }

        // Main entry: compute minimal price and return applied messages
        public PromoApplicationResult ComputeBestPrice(List<CartItem> cart, bool isMember = false)
        {
            // Expand items into single units for allocation
            var units = new List<SingleUnit>();
            int idx = 0;
            foreach (var ci in cart)
            {
                var prod = _products.FirstOrDefault(p => p.Sku == ci.Sku) ?? throw new Exception($"Unknown SKU {ci.Sku}");
                for (int q = 0; q < ci.Quantity; q++)
                {
                    decimal price = isMember && prod.MemberPrice.HasValue ? prod.MemberPrice.Value : prod.Price;
                    units.Add(new SingleUnit(prod.Sku, price, prod, idx++));
                }
            }

            // For items marked clearance, treat them as consumed by clearance promotion only (they can't join other promos)
            var clearanceSkus = _products.Where(p => p.IsClearance).Select(p => p.Sku).ToHashSet();

            // Pre-generate per-promo application options
            var promoOptionsPerPromo = _promotions.ToDictionary(
                p => p,
                p => (p.FindApplications(units) ?? new List<(List<int>, decimal, string)>())
            );

            // Backtracking search:
            decimal bestPrice = decimal.MaxValue;
            List<string> bestMessages = null;

            // Keep track of consumed unit indexes
            var consumed = new HashSet<int>();

            void Backtrack(int promoIndex, decimal accumulatedPrice, List<string> messages)
            {
                // Early prune
                if (accumulatedPrice >= bestPrice) return;

                // If all promos considered, compute remaining unconsumed unit price and then threshold
                if (promoIndex >= _promotions.Count)
                {
                    // Remaining units pay their base price, except clearance items which must use their clearance price:
                    decimal rem = 0;
                    foreach (var u in units)
                    {
                        if (consumed.Contains(u.Index)) continue;

                        if (clearanceSkus.Contains(u.Sku))
                        {
                            // find the clearance promotion price if any
                            var clearancePromo = _promotions.OfType<ClearancePromotion>().FirstOrDefault(cp => cp.Sku == u.Sku);
                            if (clearancePromo != null)
                            {
                                rem += clearancePromo.ClearancePrice;
                                continue;
                            }
                        }

                        // If there is any direct discount promotion for this SKU that wasn't applied earlier, we still should consider applying it here.
                        // For simplicity we charge base price (direct item discounts are represented among promotions; failing to apply them earlier means lost opportunity).
                        rem += u.Price;
                    }


                    return;
                }

                var promo = _promotions[promoIndex];
                var options = promoOptionsPerPromo[promo];

                // Option 1: skip this promo entirely
                Backtrack(promoIndex + 1, accumulatedPrice, messages);

                // Option 2: try each possible application of this promo (that doesn't consume already consumed units)
                foreach (var opt in options)
                {
                    if (opt.consumedIndexes.Any(ci => consumed.Contains(ci))) continue;
                    // If any consumed unit is clearance and this promo is not clearance, skip (clearance cannot combine)
                    if (opt.consumedIndexes.Any(ci => clearanceSkus.Contains(units.First(u => u.Index == ci).Sku))
                        && !(promo is ClearancePromotion))
                        continue;

                    // Apply
                    foreach (var ci in opt.consumedIndexes) consumed.Add(ci);
                    messages.Add(opt.message);

                    Backtrack(promoIndex + 1, accumulatedPrice + opt.price, messages);

                    // rollback
                    messages.RemoveAt(messages.Count - 1);
                    foreach (var ci in opt.consumedIndexes) consumed.Remove(ci);
                }
            }
            Backtrack(0, 0m, new List<string>());
            string thresholdMsg = "";
            if (_thresholdPromo != null)
            {
                var (discount, msg) = _thresholdPromo.ComputeDiscount(bestPrice);
                bestPrice -= discount;
                thresholdMsg = msg;
            }
            
            return new PromoApplicationResult(bestPrice, bestMessages ?? new List<string>());
        }


        public PromoApplicationResult ComputeBestPrice2(List<CartItem> cart)
        {
            
            return null;
        }
    }
    #endregion

    #region Sample usage (console style)
    public static class SampleRunner
    {
        public static void RunSample()
        {
            // Products
            var products = new List<Product>
            {
                new Product("A", 30m),
                new Product("B", 25m),
                new Product("C", 100m),
                new Product("D", 40m),
                new Product("E", 55m),
                new Product("F", 20m),
                // example clearance
                new Product("X", 50m, IsClearance: true)
            };

            // Promotions: implement rules 1,2,3,5,6,7
            var promos = new List<Promotion>();

            // Rule1: bundle A+B for 50 (example)
            promos.Add(new BundlePromotion { Id = "P-Bundle-AB", Name = "A+B Bundle", Priority = 100, RequiredSkus = new List<string> { "A", "B" }, BundlePrice = 50m });

            // Rule2: BOGO on A,E,F
            promos.Add(new BogoPromotion { Id = "P-BOGO-1", Name = "Buy1Get1 A/E/F", Priority = 90, EligibleSkus = new HashSet<string> { "A", "E", "F" } });

            // Rule3: multi-item discounts for A,B,E,F
            promos.Add(new MultiItemDiscountPromotion { Id = "P-Multi-A", Name = "A MultiDiscount", Priority = 80, Sku = "A", DiscountRates = new decimal[] { 1m, 0.9m, 0.8m, 0.8m } });
            promos.Add(new MultiItemDiscountPromotion { Id = "P-Multi-E", Name = "E MultiDiscount", Priority = 80, Sku = "E", DiscountRates = new decimal[] { 1m, 0.9m, 0.8m, 0.8m } });

            // Rule6: direct discounts (单品折扣)
            promos.Add(new DirectDiscountPromotion { Id = "P-Direct-C", Name = "C 9折", Priority = 70, Sku = "C", DiscountRate = 0.9m });

            // Rule7: clearance on X
            promos.Add(new ClearancePromotion { Id = "P-Clear-X", Name = "Clearance X", Priority = 200, Sku = "X", ClearancePrice = 20m });

            // Rule5: threshold full reduction
            var threshold = new ThresholdPromotion { Id = "T1", Name = "FullReduction", Priority = 1000, Tiers = new List<(decimal, decimal)> { (300m, 40m), (200m, 25m), (100m, 10m) } };

            var engine = new PromotionEngine(products, promos, threshold);

            var cart = new List<CartItem>
            {
                new CartItem("A", 2),
                new CartItem("B", 1),
                new CartItem("E", 1),
                new CartItem("C", 1),
                new CartItem("X", 1)
            };

            var result = engine.ComputeBestPrice(cart, isMember: false);
            Console.WriteLine($"Best price: {result.TotalPrice}");
            Console.WriteLine("Applied promos:");
            foreach (var m in result.AppliedMessages) Console.WriteLine(" - " + m);
        }
    }
    #endregion
}
