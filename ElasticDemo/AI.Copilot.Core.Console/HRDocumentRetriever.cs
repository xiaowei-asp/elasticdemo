using System;
using System.Collections.Generic;
using System.Text;

namespace AI.Copilot.Core
{
    public class HRDocumentRetriever : IKnowledgeRetriever
    {
        // 构造函数可以注入其他服务，例如一个模拟的 HR 数据库客户端
        public HRDocumentRetriever(/* IHRDatabaseClient hrClient */)
        {
            // 在这里实现根据 query 查找 HR 文档的逻辑
            // 例如：去公司内部的 SharePoint 文档库、Wiki 或数据库中搜索
        }

        public Task<IEnumerable<string>> SearchAsync(string query)
        {
            //Console.WriteLine($"HRDocumentRetriever: Searching for '{query}' in HR knowledge base.");
            // 模拟从 HR 知识库中检索
            var results = new List<string>();
            if (query.Contains("请假", StringComparison.OrdinalIgnoreCase))
            {
                results.Add("请假政策：员工每年有10天年假，3天病假，请提前3天申请。");
            }
            else if (query.Contains("工资", StringComparison.OrdinalIgnoreCase))
            {
                results.Add("工资发放日期为每月15号，通过银行转账。");
            }
            return Task.FromResult<IEnumerable<string>>(results);
        }
    }
}
