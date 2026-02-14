using System;
using System.Collections.Generic;
using System.Text;

namespace AI.Copilot.Core
{
    public interface IKnowledgeRetriever
    {
        // 根据用户的问题，返回相关的文本片段
        Task<IEnumerable<string>> SearchAsync(string query);
    }
}
