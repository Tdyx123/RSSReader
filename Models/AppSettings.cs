namespace RSSReader.Models;

public class AppSettings
{
    public GlmApiSettings GlmApi { get; set; } = new();
    public RssSettings Rss { get; set; } = new();
}

public class GlmApiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "glm-4.7-flash";
    public int MaxTokens { get; set; } = 2048;
    public double Temperature { get; set; } = 0.7;
}

public class RssSettings
{
    public int RefreshIntervalMinutes { get; set; } = 30;
    public int MaxArticlesPerFeed { get; set; } = 100;
}
