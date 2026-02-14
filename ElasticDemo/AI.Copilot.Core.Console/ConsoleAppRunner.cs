using System;
using System.Collections.Generic;
using System.Text;

namespace AI.Copilot.Core
{
    public class ConsoleAppRunner
    {
        private readonly ICopilotService _copilotService;

        public ConsoleAppRunner(ICopilotService copilotService)
        {
            _copilotService = copilotService;
        }

        public async Task RunAsync()
        {
            Console.WriteLine("HR Copilot Console App Ready. Type your question (or 'exit' to quit):");
            while (true)
            {
                Console.Write("You: ");
                var input = Console.ReadLine();
                if (input?.ToLower() == "exit")
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(input)) continue;

                Console.Write("HR Copilot: ");
                var sessionId = "my-hr-session-1"; // 模拟一个会话ID
                await foreach (var chunk in _copilotService.ChatAsync(input!, sessionId))
                {
                    Console.Write(chunk);
                }
                Console.WriteLine(); // 换行
            }
        }
    }
}
