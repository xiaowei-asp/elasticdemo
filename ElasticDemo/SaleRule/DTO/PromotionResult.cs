using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaleRule.DTO
{
    public class PromotionResult
    {
        public string PromotionId { get; set; } = "";
        public decimal DiscountAmount { get; set; } = 0m; // 减少的金额
        public string Note { get; set; } = "";
    }
}
