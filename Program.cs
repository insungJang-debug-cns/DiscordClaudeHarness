using System.Text.Json;
using Microsoft.Extensions.Configuration;
using DiscordClaudeHarness.Models;
using DiscordClaudeHarness.Services;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var reposJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "repos.json"));
var repoConfigRoot = JsonSerializer.Deserialize<RepoConfigRoot>(
    reposJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? new RepoConfigRoot();

var botService = new DiscordBotService(
    botToken: configuration["Discord:BotToken"]!,
    repos: repoConfigRoot.Repos,
    workspaceRoot: configuration["Workspace:RootPath"]!,
    defaultBranch: configuration["GitHub:DefaultBranch"] ?? "main",
    claudeExecutablePath: configuration["ClaudeCode:ExecutablePath"] ?? "claude",
    claudeTimeoutMinutes: int.Parse(configuration["ClaudeCode:TimeoutMinutes"] ?? "15"),
    githubToken: configuration["GitHub:PersonalAccessToken"]!
);

Console.WriteLine("Discord-Claude Harness 시작 중...");
await botService.StartAsync();
