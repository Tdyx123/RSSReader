using System;
using System.Collections.Generic;

namespace RSSReader.Models;

public class EventLineItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string? Category { get; set; }
    public List<int> RelatedArticleIds { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
