using System;
using System.Collections.Generic;
using System.Text;

namespace AI.Copilot.Core
{
    public class CopilotOptions
    {
        // OpenAI 或 Azure OpenAI 配置
        public string ModelId { get; set; } = "gpt-4";
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }

        // 系统提示词 (System Prompt) - 这是组件的灵魂
        // 业务方可以在这里填："你是一个HR助手..." 或 "你是一个IT运维..."
        public string SystemPrompt { get; set; } = "You are a helpful AI assistant.";

        // 向量检索的阈值
        public float RelevanceThreshold { get; set; } = 0.7f;
    }
}
