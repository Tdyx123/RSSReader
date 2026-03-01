using System;

namespace RSSReader.Models;

public class Feed
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FaviconUrl { get; set; }
    public string? Category { get; set; }
    public DateTime? LastUpdated { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsEnabled { get; set; } = true;
}
