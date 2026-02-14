using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.Collections.Generic;
using System.Text;

namespace AI.Copilot.Core
{
    public class SemanticKernelCopilotService : ICopilotService
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chat;
        private readonly CopilotOptions _options;
        // 注入我们定义的通用检索接口
        private readonly IKnowledgeRetriever _retriever;

        public SemanticKernelCopilotService(
            Kernel kernel,
            IOptions<CopilotOptions> options,
            IKnowledgeRetriever retriever)
        {
            _kernel = kernel;
            _options = options.Value;
            _retriever = retriever;
            _chat = kernel.GetRequiredService<IChatCompletionService>();
        }


        public async IAsyncEnumerable<string> ChatAsync(string userMessage, string sessionId, Dictionary<string, object>? contextData = null)
        {
            // 1. RAG 检索：调用接口获取知识（不再直接查 DB）
            var relatedDocs = await _retriever.SearchAsync(userMessage);
            string contextString = string.Join("\n", relatedDocs);

            // 2. 构建 Prompt
            // 这里使用了 Semantic Kernel 的 ChatHistory
            var history = new ChatHistory(_options.SystemPrompt);

            // 把检索到的知识塞进 System Prompt 或者 User Message 中
            if (!string.IsNullOrEmpty(contextString))
            {
                history.AddSystemMessage($"参考以下知识库回答问题：\n{contextString}");
            }

            // 添加用户消息
            history.AddUserMessage(userMessage);

            // 3. 执行 AI 调用 (开启自动函数调用)
            var executionSettings = new OpenAIPromptExecutionSettings()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions // 关键：允许 AI 调用插件
            };

            // 4. 流式返回
            await foreach (var content in _chat.GetStreamingChatMessageContentsAsync(
                               history,
                               executionSettings,
                               _kernel))
            {
                if (content.Content != null)
                    yield return content.Content;
            }
        }

        public Task ClearHistoryAsync(string sessionId)
        {
            throw new NotImplementedException();
        }
    }
}

