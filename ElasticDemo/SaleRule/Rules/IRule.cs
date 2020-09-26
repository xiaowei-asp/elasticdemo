using SaleRule.DTO;

namespace SaleRule.Rules
{
    public interface IRule
    {
        string Id { get; }
        int Priority { get; } // 小的先执行
        PromotionResult Apply(Cart cart);
    }
}
