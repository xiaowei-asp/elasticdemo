using System;
using System.Collections.Generic;
using System.Text;

namespace AI.Copilot.Core
{
    // Company.AI.Copilot.Core/Interfaces/ICopilotService.cs
    public interface ICopilotService
    {
        /// <summary>
        /// 发送消息并获取流式回复
        /// </summary>
        /// <param name="userMessage">用户输入</param>
        /// <param name="sessionId">会话ID（用于隔离不同用户的记忆）</param>
        /// <param name="contextData">可选的上下文数据（如用户ID、当前页面URL）</param>
        IAsyncEnumerable<string> ChatAsync(string userMessage, string sessionId, Dictionary<string, object>? contextData = null);

        /// <summary>
        /// 清除对话记忆
        /// </summary>
        Task ClearHistoryAsync(string sessionId);
    }
}
