using System;

namespace RSSReader.Models;

public class Article
{
    public int Id { get; set; }
    public int FeedId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }
    public string? Link { get; set; }
    public string? Author { get; set; }
    public DateTime? PublishDate { get; set; }
    public string? Guid { get; set; }
    public bool IsRead { get; set; }
    public bool IsStarred { get; set; }
    public string? AiSummary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Feed? Feed { get; set; }
}
