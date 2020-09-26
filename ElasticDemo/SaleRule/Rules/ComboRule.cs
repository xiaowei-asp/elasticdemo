using SaleRule.DTO;

namespace SaleRule.Rules
{
    public class ComboRule : IRule
    {
        public string Id { get; set; }

        public int Priority { get; set; }

        private readonly List<string> _skus;

        private readonly decimal _comboPrice;

        public ComboRule(string id,int priority, IEnumerable<string> skus,decimal comboPrice) 
        {
            Id = id;
            Priority = priority;
            _skus = skus.ToList();
            _comboPrice = comboPrice;
        }

        public PromotionResult Apply(Cart cart)
        {
            var possibleTimes = int.MaxValue;
            if(cart == null || cart.Items.Count <= 0)
                throw new ArgumentException();

            var avaliableItems = cart.Items.Where(c => c.AvailableQuantity > 0).ToList();
            
            //判断能组成多少组合
            var avaliableItemDic = avaliableItems.ToDictionary(key=>key.Product.Sku, value => value);
            foreach (var sku in _skus)
            {
                if(!avaliableItemDic.ContainsKey(sku) || avaliableItemDic[sku].AvailableQuantity <= 0)
                {
                    possibleTimes = 0;
                    break;
                }

                possibleTimes = Math.Min(possibleTimes, avaliableItemDic[sku].AvailableQuantity);
            }

            if(possibleTimes <= 0)
            {
                return new PromotionResult() { PromotionId = Id, DiscountAmount = 0m, Note = "无可用商品，无法应用" };
            }

            //possibleTimes代表组合次数
            var discountTotal = 0m;
            for (int i = 0; i < possibleTimes; i++)
            {
                //原始的折扣价
                var origSum = 0m;
                foreach (var sku in _skus)
                {
                    var it = avaliableItemDic[sku];
                    origSum += it.UnitPrice;
                }
                var discount =  Math.Round(origSum - _comboPrice, 2);
                if(discount < 0)
                {
                    discount = 0m;
                }

                //按总数量分拆金额
                var sumUnit = _skus.Sum(c => avaliableItemDic[c].UnitPrice);

                foreach (var sku in _skus)
                {
                    var it = avaliableItemDic[sku];
                    it.LockedQuantity += 1;
                    var share = sumUnit > 0 ? (it.UnitPrice / sumUnit * _comboPrice): _comboPrice / _skus.Count;
                    it.AdjustedAmount += Math.Round(share, 2);
                    it.AppliedPromotions.Add($"{Id} (combo)");
                }

                discountTotal += discount;
            }

            return new PromotionResult() { PromotionId = Id, DiscountAmount = discountTotal, Note = $"应用组合{possibleTimes}次" };
        }
    }
}
