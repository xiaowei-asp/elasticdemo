using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaleRule.Rules.RuleDto
{
    public class ComboRuleDto
    {
        public List<ComboItem> comboItems { get; set; } = new List<ComboItem>();

        public decimal ComboAmount { get; set; }
    }

    public class ComboItem
    {
        public string Name { get; set; }

        public string Sku { get; set; } 
    }
}
