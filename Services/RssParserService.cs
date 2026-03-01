using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Xml.Linq;
using RSSReader.Models;

namespace RSSReader.Services;

public class RssParserService
{
    private readonly HttpClient _httpClient;
    private static readonly XNamespace Content = "http://purl.org/rss/1.0/modules/content/";
    private static readonly XNamespace DC = "http://purl.org/dc/elements/1.1/";

    public RssParserService()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        
        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "RSSReader/1.0");
    }

    public async Task<(Feed Feed, List<Article> Articles)> ParseFeedAsync(string url)
    {
        var response = await _httpClient.GetStringAsync(url);
        
        XDocument doc;
        try
        {
            doc = XDocument.Parse(response);
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to parse RSS feed: " + ex.Message);
        }
        
        var root = doc.Root;
        if (root == null) throw new Exception("Invalid RSS feed");

        Feed feed;
        List<Article> articles;

        try
        {
            if (root.Name.LocalName == "rss")
            {
                (feed, articles) = ParseRss20(root, url);
            }
            else if (root.Name.LocalName == "feed")
            {
                (feed, articles) = ParseAtom(root, url);
            }
            else if (root.Name.LocalName == "RDF")
            {
                (feed, articles) = ParseRss10(root, url);
            }
            else
            {
                throw new Exception("Unknown feed format: " + root.Name.LocalName);
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to parse feed: " + ex.Message);
        }

        feed.Url = url;
        return (feed, articles);
    }

    private (Feed Feed, List<Article> Articles) ParseRss20(XElement root, string url)
    {
        var channel = root.Element("channel");
        if (channel == null) throw new Exception("Invalid RSS 2.0 feed");

        var feed = new Feed
        {
            Title = channel.Element("title")?.Value ?? "Unknown",
            Description = channel.Element("description")?.Value,
            FaviconUrl = channel.Element("image")?.Element("url")?.Value
        };

        var articles = new List<Article>();
        foreach (var item in channel.Elements("item"))
        {
            var title = item.Element("title")?.Value ?? "No Title";
            var description = item.Element("description")?.Value;
            var content = GetElementValue(item, Content + "encoded") ?? description;
            var author = item.Element("author")?.Value ?? GetElementValue(item, DC + "creator");
            var guid = item.Element("guid")?.Value ?? item.Element("link")?.Value;
            var pubDate = item.Element("pubDate")?.Value;

            var article = new Article
            {
                Title = title,
                Description = description,
                Content = content,
                Link = item.Element("link")?.Value,
                Author = author,
                Guid = guid,
                PublishDate = ParseDate(pubDate)
            };
            articles.Add(article);
        }

        return (feed, articles);
    }

    private (Feed Feed, List<Article> Articles) ParseAtom(XElement root, string url)
    {
        var feed = new Feed
        {
            Title = root.Element("title")?.Value ?? "Unknown",
            Description = root.Element("subtitle")?.Value,
            FaviconUrl = root.Element("icon")?.Value
        };

        var articles = new List<Article>();
        foreach (var entry in root.Elements("entry"))
        {
            var link = entry.Element("link")?.Attribute("href")?.Value;
            var article = new Article
            {
                Title = entry.Element("title")?.Value ?? "No Title",
                Description = entry.Element("summary")?.Value ?? entry.Element("content")?.Value,
                Content = entry.Element("content")?.Value,
                Link = link,
                Author = entry.Element("author")?.Element("name")?.Value,
                Guid = entry.Element("id")?.Value ?? link,
                PublishDate = ParseDate(entry.Element("published")?.Value ?? entry.Element("updated")?.Value)
            };
            articles.Add(article);
        }

        return (feed, articles);
    }

    private (Feed Feed, List<Article> Articles) ParseRss10(XElement root, string url)
    {
        var channel = root.Element("channel");
        if (channel == null) throw new Exception("Invalid RSS 1.0 feed");

        var feed = new Feed
        {
            Title = channel.Element("title")?.Value ?? "Unknown",
            Description = channel.Element("description")?.Value
        };

        var articles = new List<Article>();
        foreach (var item in root.Elements("item"))
        {
            var title = item.Element("title")?.Value ?? "No Title";
            var description = item.Element("description")?.Value;
            var content = GetElementValue(item, Content + "encoded") ?? description;
            var author = GetElementValue(item, DC + "creator");
            var pubDate = GetElementValue(item, DC + "date");

            var article = new Article
            {
                Title = title,
                Description = description,
                Content = content,
                Link = item.Element("link")?.Value,
                Author = author,
                Guid = item.Element("link")?.Value,
                PublishDate = ParseDate(pubDate)
            };
            articles.Add(article);
        }

        return (feed, articles);
    }

    private string? GetElementValue(XElement parent, XName name)
    {
        var element = parent.Element(name);
        return element?.Value;
    }

    private DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;

        string[] formats = {
            "r",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:sszzz",
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd"
        };

        if (DateTime.TryParseExact(dateStr.Trim(), formats, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal, out var date))
        {
            return date;
        }

        if (DateTime.TryParse(dateStr, out date))
        {
            return date;
        }

        return null;
    }

    public async Task<string?> GetFaviconAsync(string url)
    {
        try
        {
            var uri = new Uri(url);
            var faviconUrl = $"{uri.Scheme}://{uri.Host}/favicon.ico";
            
            var response = await _httpClient.GetAsync(faviconUrl);
            if (response.IsSuccessStatusCode)
            {
                return faviconUrl;
            }
            
            return $"{uri.Scheme}://{uri.Host}/favicon.ico";
        }
        catch
        {
            return null;
        }
    }
}
