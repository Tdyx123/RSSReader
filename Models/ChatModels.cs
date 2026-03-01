namespace RSSReader.Models;

public class ChatMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public class ChatRequest
{
    public string Model { get; set; } = "glm-4-flash";
    public List<ChatMessage> Messages { get; set; } = new();
    public int MaxTokens { get; set; } = 2048;
    public double Temperature { get; set; } = 0.7;
    public bool Stream { get; set; } = false;
}

public class ChatResponse
{
    public string Id { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public List<Choice> Choices { get; set; } = new();
    public Usage? Usage { get; set; }
}

public class Choice
{
    public int Index { get; set; }
    public Message Message { get; set; } = new();
    public string? FinishReason { get; set; }
}

public class Message
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class Usage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
