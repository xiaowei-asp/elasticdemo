// See https://aka.ms/new-console-template for more information
using AI.Copilot.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((hostContext, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        config.AddEnvironmentVariables();
        // 添加 SecretManager 用于开发环境的敏感信息
        if (hostContext.HostingEnvironment.IsDevelopment())
        {
            config.AddUserSecrets<Program>();
        }
    })
    .ConfigureServices((hostContext, services) =>
    {
        // =======================================================
        // 这里就是你要添加的代码！
        // =======================================================
        // 1. 注册 HR 部门特有的 IKnowledgeRetriever 实现
        services.AddScoped<IKnowledgeRetriever, HRDocumentRetriever>();

        // 2. 注册你的 Copilot 核心组件
        services.AddCompanyCopilot(options =>
        {
            // 从配置中读取 OpenAI 相关设置
            options.ModelId = hostContext.Configuration["Copilot:ModelId"] ?? "gpt-4-turbo";
            options.Endpoint = hostContext.Configuration["Copilot:Endpoint"]!;
            options.ApiKey = hostContext.Configuration["Copilot:ApiKey"]!;
            options.SystemPrompt = "你是一个专业友善的 HR 助手，请根据公司的 HR 政策回答问题。";
        });

        // 3. 注册你的 Console 应用程序的运行器，它会用到 ICopilotService
        services.AddSingleton<ConsoleAppRunner>();
    }).Build();

// 启动 Host 并运行你的应用程序逻辑
var runner = host.Services.GetRequiredService<ConsoleAppRunner>();
await runner.RunAsync();

//await host.RunAsync(); // 保持主机运行，但对于这种简单的命令行应用，可以不需要
Console.WriteLine("Hello, World!");