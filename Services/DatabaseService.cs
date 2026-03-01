using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using RSSReader.Models;

namespace RSSReader.Services;

public class DatabaseService
{
    private static DatabaseService? _instance;
    public static DatabaseService Instance => _instance ??= new DatabaseService();

    private readonly string _connectionString;

    private DatabaseService()
    {
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "rssreader.db");
        
        if (!Directory.Exists(Path.GetDirectoryName(dbPath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? string.Empty);
        }

        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Feeds (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Url TEXT NOT NULL UNIQUE,
                Description TEXT,
                FaviconUrl TEXT,
                Category TEXT,
                LastUpdated TEXT,
                CreatedAt TEXT NOT NULL,
                IsEnabled INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS Articles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FeedId INTEGER NOT NULL,
                Title TEXT NOT NULL,
                Description TEXT,
                Content TEXT,
                Link TEXT,
                Author TEXT,
                PublishDate TEXT,
                Guid TEXT,
                IsRead INTEGER NOT NULL DEFAULT 0,
                IsStarred INTEGER NOT NULL DEFAULT 0,
                AiSummary TEXT,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (FeedId) REFERENCES Feeds(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS EventLineItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Description TEXT NOT NULL,
                EventDate TEXT NOT NULL,
                Category TEXT,
                RelatedArticleIds TEXT,
                CreatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_articles_feedid ON Articles(FeedId);
            CREATE INDEX IF NOT EXISTS idx_articles_publishdate ON Articles(PublishDate);
            CREATE INDEX IF NOT EXISTS idx_eventline_eventdate ON EventLineItems(EventDate);
        ";
        command.ExecuteNonQuery();
    }

    public List<Feed> GetAllFeeds()
    {
        var feeds = new List<Feed>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Feeds ORDER BY Category, Title";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            feeds.Add(new Feed
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Url = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                FaviconUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
                Category = reader.IsDBNull(5) ? null : reader.GetString(5),
                LastUpdated = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                IsEnabled = reader.GetInt32(8) == 1
            });
        }

        return feeds;
    }

    public Feed AddFeed(Feed feed)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Feeds (Title, Url, Description, FaviconUrl, Category, LastUpdated, CreatedAt, IsEnabled)
            VALUES (@Title, @Url, @Description, @FaviconUrl, @Category, @LastUpdated, @CreatedAt, @IsEnabled);
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("@Title", feed.Title);
        command.Parameters.AddWithValue("@Url", feed.Url);
        command.Parameters.AddWithValue("@Description", feed.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@FaviconUrl", feed.FaviconUrl ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Category", feed.Category ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@LastUpdated", feed.LastUpdated?.ToString("o") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@CreatedAt", feed.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("@IsEnabled", feed.IsEnabled ? 1 : 0);

        feed.Id = Convert.ToInt32(command.ExecuteScalar());
        return feed;
    }

    public void UpdateFeed(Feed feed)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Feeds SET 
                Title = @Title, 
                Url = @Url, 
                Description = @Description, 
                FaviconUrl = @FaviconUrl, 
                Category = @Category, 
                LastUpdated = @LastUpdated,
                IsEnabled = @IsEnabled
            WHERE Id = @Id
        ";

        command.Parameters.AddWithValue("@Id", feed.Id);
        command.Parameters.AddWithValue("@Title", feed.Title);
        command.Parameters.AddWithValue("@Url", feed.Url);
        command.Parameters.AddWithValue("@Description", feed.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@FaviconUrl", feed.FaviconUrl ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Category", feed.Category ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@LastUpdated", feed.LastUpdated?.ToString("o") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsEnabled", feed.IsEnabled ? 1 : 0);

        command.ExecuteNonQuery();
    }

    public void DeleteFeed(int feedId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Feeds WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", feedId);
        command.ExecuteNonQuery();
    }

    public List<Article> GetArticlesByFeedId(int feedId, int limit = 100)
    {
        var articles = new List<Article>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT a.*, f.Title as FeedTitle, f.FaviconUrl 
            FROM Articles a 
            LEFT JOIN Feeds f ON a.FeedId = f.Id 
            WHERE a.FeedId = @FeedId 
            ORDER BY a.PublishDate DESC 
            LIMIT @Limit
        ";
        command.Parameters.AddWithValue("@FeedId", feedId);
        command.Parameters.AddWithValue("@Limit", limit);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            articles.Add(ReadArticle(reader));
        }

        return articles;
    }

    public List<Article> GetAllArticles(int limit = 500)
    {
        var articles = new List<Article>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT a.*, f.Title as FeedTitle, f.FaviconUrl 
            FROM Articles a 
            LEFT JOIN Feeds f ON a.FeedId = f.Id 
            ORDER BY a.PublishDate DESC 
            LIMIT @Limit
        ";
        command.Parameters.AddWithValue("@Limit", limit);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            articles.Add(ReadArticle(reader));
        }

        return articles;
    }

    public Article? GetArticleByGuid(string guid, int feedId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Articles WHERE Guid = @Guid AND FeedId = @FeedId";
        command.Parameters.AddWithValue("@Guid", guid);
        command.Parameters.AddWithValue("@FeedId", feedId);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new Article
            {
                Id = reader.GetInt32(0),
                FeedId = reader.GetInt32(1),
                Title = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                Content = reader.IsDBNull(4) ? null : reader.GetString(4),
                Link = reader.IsDBNull(5) ? null : reader.GetString(5),
                Author = reader.IsDBNull(6) ? null : reader.GetString(6),
                PublishDate = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
                Guid = reader.IsDBNull(8) ? null : reader.GetString(8),
                IsRead = reader.GetInt32(9) == 1,
                IsStarred = reader.GetInt32(10) == 1,
                AiSummary = reader.IsDBNull(11) ? null : reader.GetString(11),
                CreatedAt = DateTime.Parse(reader.GetString(12))
            };
        }

        return null;
    }

    public Article AddArticle(Article article)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Articles (FeedId, Title, Description, Content, Link, Author, PublishDate, Guid, IsRead, IsStarred, AiSummary, CreatedAt)
            VALUES (@FeedId, @Title, @Description, @Content, @Link, @Author, @PublishDate, @Guid, @IsRead, @IsStarred, @AiSummary, @CreatedAt);
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("@FeedId", article.FeedId);
        command.Parameters.AddWithValue("@Title", article.Title);
        command.Parameters.AddWithValue("@Description", article.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Content", article.Content ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Link", article.Link ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Author", article.Author ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@PublishDate", article.PublishDate?.ToString("o") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Guid", article.Guid ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsRead", article.IsRead ? 1 : 0);
        command.Parameters.AddWithValue("@IsStarred", article.IsStarred ? 1 : 0);
        command.Parameters.AddWithValue("@AiSummary", article.AiSummary ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@CreatedAt", article.CreatedAt.ToString("o"));

        article.Id = Convert.ToInt32(command.ExecuteScalar());
        return article;
    }

    public void UpdateArticle(Article article)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Articles SET 
                IsRead = @IsRead, 
                IsStarred = @IsStarred, 
                AiSummary = @AiSummary
            WHERE Id = @Id
        ";

        command.Parameters.AddWithValue("@Id", article.Id);
        command.Parameters.AddWithValue("@IsRead", article.IsRead ? 1 : 0);
        command.Parameters.AddWithValue("@IsStarred", article.IsStarred ? 1 : 0);
        command.Parameters.AddWithValue("@AiSummary", article.AiSummary ?? (object)DBNull.Value);

        command.ExecuteNonQuery();
    }

    public void DeleteArticle(int articleId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Articles WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", articleId);
        command.ExecuteNonQuery();
    }

    public List<EventLineItem> GetEventLineItems(int limit = 50)
    {
        var items = new List<EventLineItem>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT * FROM EventLineItems 
            ORDER BY EventDate DESC 
            LIMIT @Limit
        ";
        command.Parameters.AddWithValue("@Limit", limit);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var item = new EventLineItem
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Description = reader.GetString(2),
                EventDate = DateTime.Parse(reader.GetString(3)),
                Category = reader.IsDBNull(4) ? null : reader.GetString(4),
                RelatedArticleIds = reader.IsDBNull(5) 
                    ? new List<int>() 
                    : System.Text.Json.JsonSerializer.Deserialize<List<int>>(reader.GetString(5)) ?? new List<int>(),
                CreatedAt = DateTime.Parse(reader.GetString(6))
            };
            items.Add(item);
        }

        return items;
    }

    public EventLineItem AddEventLineItem(EventLineItem item)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO EventLineItems (Title, Description, EventDate, Category, RelatedArticleIds, CreatedAt)
            VALUES (@Title, @Description, @EventDate, @Category, @RelatedArticleIds, @CreatedAt);
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("@Title", item.Title);
        command.Parameters.AddWithValue("@Description", item.Description);
        command.Parameters.AddWithValue("@EventDate", item.EventDate.ToString("o"));
        command.Parameters.AddWithValue("@Category", item.Category ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RelatedArticleIds", System.Text.Json.JsonSerializer.Serialize(item.RelatedArticleIds));
        command.Parameters.AddWithValue("@CreatedAt", item.CreatedAt.ToString("o"));

        item.Id = Convert.ToInt32(command.ExecuteScalar());
        return item;
    }

    public void ClearEventLineItems()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM EventLineItems";
        command.ExecuteNonQuery();
    }

    private Article ReadArticle(SqliteDataReader reader)
    {
        var article = new Article
        {
            Id = reader.GetInt32(0),
            FeedId = reader.GetInt32(1),
            Title = reader.GetString(2),
            Description = reader.IsDBNull(3) ? null : reader.GetString(3),
            Content = reader.IsDBNull(4) ? null : reader.GetString(4),
            Link = reader.IsDBNull(5) ? null : reader.GetString(5),
            Author = reader.IsDBNull(6) ? null : reader.GetString(6),
            PublishDate = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
            Guid = reader.IsDBNull(8) ? null : reader.GetString(8),
            IsRead = reader.GetInt32(9) == 1,
            IsStarred = reader.GetInt32(10) == 1,
            AiSummary = reader.IsDBNull(11) ? null : reader.GetString(11),
            CreatedAt = DateTime.Parse(reader.GetString(12))
        };

        if (!reader.IsDBNull(13))
        {
            article.Feed = new Feed
            {
                Title = reader.GetString(13),
                FaviconUrl = reader.IsDBNull(14) ? null : reader.GetString(14)
            };
        }

        return article;
    }
}
