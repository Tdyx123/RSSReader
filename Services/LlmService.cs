using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RSSReader.Models;

namespace RSSReader.Services;

public class LlmService
{
    private static LlmService? _instance;
    public static LlmService Instance => _instance ??= new LlmService();

    private readonly HttpClient _httpClient;
    private readonly string _apiUrl = "https://open.bigmodel.cn/api/paas/v4/chat/completions";

    private LlmService()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        
        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<string> GenerateSummaryAsync(string content, CancellationToken cancellationToken = default)
    {
        var prompt = "请用 2-3 句话概括以下文章的主要内容：\n\n" + content + "\n\n摘要：";

        return await SendMessageAsync(prompt, cancellationToken);
    }

    public async Task<List<EventLineItem>> GenerateEventLineAsync(List<Article> articles, CancellationToken cancellationToken = default)
    {
        var articlesText = new StringBuilder();
        foreach (var article in articles)
        {
            articlesText.AppendLine("- 标题: " + article.Title);
            articlesText.AppendLine("  日期: " + (article.PublishDate?.ToString("yyyy-MM-dd") ?? "未知"));
            var desc = article.Description ?? article.Content ?? "";
            articlesText.AppendLine("  内容: " + desc.Substring(0, Math.Min(500, desc.Length)) + "...");
            articlesText.AppendLine();
        }

        var prompt = @"请分析以下文章列表，提取关键事件并按时间顺序排列。每个事件需要包含：事件标题、事件描述、事件日期。

返回格式要求（JSON 数组）：
[
  {
    ""title"": ""事件标题"",
    ""description"": ""事件描述（50字以内）"",
    ""date"": ""YYYY-MM-DD""
  }}
]

文章列表：
" + articlesText + @"

请只返回 JSON 数组，不要其他内容。";

        var result = await SendMessageAsync(prompt, cancellationToken);

        try
        {
            var jsonStart = result.IndexOf('[');
            var jsonEnd = result.LastIndexOf(']');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = result.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var items = JsonSerializer.Deserialize<List<EventLineItemDto>>(json);
                
                if (items != null)
                {
                    var eventItems = new List<EventLineItem>();
                    foreach (var item in items)
                    {
                        eventItems.Add(new EventLineItem
                        {
                            Title = item.Title ?? "",
                            Description = item.Description ?? "",
                            EventDate = DateTime.TryParse(item.Date, out var date) ? date : DateTime.Now,
                            CreatedAt = DateTime.Now
                        });
                    }
                    return eventItems;
                }
            }
        }
        catch
        {
        }

        return new List<EventLineItem>();
    }

    public async Task<string> AnswerQuestionAsync(string question, List<Article> contextArticles, CancellationToken cancellationToken = default)
    {
        var contextText = new StringBuilder();
        foreach (var article in contextArticles.Take(10))
        {
            contextText.AppendLine("标题: " + article.Title);
            contextText.AppendLine("来源: " + (article.Feed?.Title ?? "未知"));
            contextText.AppendLine("日期: " + (article.PublishDate?.ToString("yyyy-MM-dd") ?? "未知"));
            var desc = article.Description ?? article.Content ?? "";
            contextText.AppendLine("内容: " + desc.Substring(0, Math.Min(300, desc.Length)) + "...");
            contextText.AppendLine();
        }

        var prompt = @"你是一个智能助手，请根据以下已收集的文章内容回答用户的问题。

用户问题：" + question + @"

参考文章：
" + contextText + @"

请基于以上文章内容回答问题。如果无法从文章中找到答案，请说明""根据已收集的文章，没有找到相关信息""。

回答：";

        return await SendMessageAsync(prompt, cancellationToken);
    }

    public async Task<string> SendMessageAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        var apiKey = ConfigurationService.Instance.GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            return "错误：请先在设置中配置 GLM API Key";
        }

        var request = new ChatRequest
        {
            Model = ConfigurationService.Instance.GetModel(),
            Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = userMessage }
            },
            MaxTokens = ConfigurationService.Instance.Settings.GlmApi.MaxTokens,
            Temperature = (float)ConfigurationService.Instance.Settings.GlmApi.Temperature
        };

        var json = JsonSerializer.Serialize(request);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, _apiUrl);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return "API 调用失败: " + response.StatusCode + " - " + errorContent;
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseContent);

        return chatResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? "没有收到有效响应";
    }

    public bool HasApiKey()
    {
        return !string.IsNullOrEmpty(ConfigurationService.Instance.GetApiKey());
    }

    private class EventLineItemDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Date { get; set; }
    }
}
