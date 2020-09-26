using System;
using System.Collections.Generic;
using System.Linq;

class Promotion
{
    public Dictionary<string, int> NeedItems { get; set; } = new Dictionary<string, int>();
    public int Price { get; set; }
}

class PromotionSolver
{
    private Dictionary<string, int> prices;
    private List<Promotion> promotions;
    private Dictionary<string, int> memo = new(); // 记忆化缓存

    public PromotionSolver(Dictionary<string, int> prices, List<Promotion> promotions)
    {
        this.prices = prices;
        this.promotions = promotions;
    }

    // 主入口：计算最优价格
    public int GetMinCost(Dictionary<string, int> items)
    {
        return Dfs(items);
    }

    private int Dfs(Dictionary<string, int> items)
    {
        // 所有商品买完了
        if (items.Values.All(v => v == 0))
            return 0;

        // 状态 key：用剩余商品拼接成字符串
        string stateKey = string.Join(",", items.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value}"));
        if (memo.ContainsKey(stateKey))
            return memo[stateKey];

        int minCost = int.MaxValue;

        // 1. 尝试用促销
        foreach (var promo in promotions)
        {
            if (CanApply(items, promo))
            {
                var nextItems = ApplyPromotion(items, promo);
                int cost = promo.Price + Dfs(nextItems);
                minCost = Math.Min(minCost, cost);
            }
        }

        // 2. 尝试单买一个商品
        foreach (var kv in items)
        {
            if (kv.Value > 0)
            {
                var nextItems = new Dictionary<string, int>(items);
                nextItems[kv.Key]--;
                int cost = prices[kv.Key] + Dfs(nextItems);
                minCost = Math.Min(minCost, cost);
            }
        }

        memo[stateKey] = minCost;
        return minCost;
    }

    private bool CanApply(Dictionary<string, int> items, Promotion promo)
    {
        foreach (var need in promo.NeedItems)
        {
            if (!items.ContainsKey(need.Key) || items[need.Key] < need.Value)
                return false;
        }
        return true;
    }

    private Dictionary<string, int> ApplyPromotion(Dictionary<string, int> items, Promotion promo)
    {
        var result = new Dictionary<string, int>(items);
        foreach (var need in promo.NeedItems)
        {
            result[need.Key] -= need.Value;
        }
        return result;
    }
}

class Program1
{
    static void Main()
    {
        // 商品价格
        var prices = new Dictionary<string, int>
        {
            { "A", 30 },
            { "B", 25 },
            { "C", 100 }
        };

        // 促销活动
        var promotions = new List<Promotion>
        {
            new Promotion { NeedItems = new Dictionary<string, int>{{"A",1},{"B",1}}, Price = 50 },   // 套餐 A+B=50
            new Promotion { NeedItems = new Dictionary<string, int>{{"B",1},{"C",1}}, Price = 100 }, // 套餐 B+C=100
            new Promotion { NeedItems = new Dictionary<string, int>{{"A",2}}, Price = 30 }           // 买一送一 (2个A=30)
        };

        // 用户购买清单
        var items = new Dictionary<string, int>
        {
            { "A", 1 },
            { "B", 1 },
            { "C", 1 }
        };

        var solver = new PromotionSolver(prices, promotions);
        int minCost = solver.GetMinCost(items);
        Console.WriteLine($"最优价格 = {minCost}");
    }
}
