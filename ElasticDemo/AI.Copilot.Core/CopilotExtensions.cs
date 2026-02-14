using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Text;

namespace AI.Copilot.Core
{
    public static class CopilotExtensions
    {
        public static IServiceCollection AddCompanyCopilot(
            this IServiceCollection services,
            Action<CopilotOptions> configureOptions)
        {
            // 1. 绑定配置
            services.Configure(configureOptions);
            var options = new CopilotOptions();
            configureOptions(options);

            // 2. 注册 Semantic Kernel
            var builder = Kernel.CreateBuilder();

            // 根据配置决定连接 OpenAI 还是 Azure OpenAI
            builder.AddAzureOpenAIChatCompletion(
                options.ModelId,
                options.Endpoint,
                options.ApiKey);

            // 3. 自动发现并注册宿主程序里的所有 Plugin
            // 这一步很关键：它允许业务方写自己的 Plugin，组件会自动加载
            builder.Services.AddLogging(); // SK 需要 Logger

            services.AddTransient<Kernel>(sp => {
                // 这里可以加入逻辑，从 DI 容器中寻找带有 [KernelFunction] 的类并导入
                var kernel = builder.Build();
                // 示例：手动导入某些全局插件
                return kernel;
            });

            // 4. 注册核心服务
            services.AddScoped<ICopilotService, SemanticKernelCopilotService>();

            return services;
        }
    }
}
