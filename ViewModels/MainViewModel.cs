using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSSReader.Models;
using RSSReader.Services;

namespace RSSReader.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly RssParserService _rssParser = new();
    private CancellationTokenSource? _refreshCts;

    [ObservableProperty]
    private ObservableCollection<Feed> _feeds = new();

    [ObservableProperty]
    private ObservableCollection<Article> _articles = new();

    [ObservableProperty]
    private ObservableCollection<EventLineItem> _eventLineItems = new();

    [ObservableProperty]
    private ObservableCollection<ChatMessage> _chatMessages = new();

    [ObservableProperty]
    private Feed? _selectedFeed;

    [ObservableProperty]
    private Article? _selectedArticle;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private string _questionText = string.Empty;

    [ObservableProperty]
    private bool _hasApiKey;

    [ObservableProperty]
    private string _newFeedUrl = string.Empty;

    [ObservableProperty]
    private string _newFeedCategory = string.Empty;

    [ObservableProperty]
    private bool _isWebViewVisible;

    public MainViewModel()
    {
        LoadFeeds();
        HasApiKey = LlmService.Instance.HasApiKey();
        
        ChatMessages.Add(new ChatMessage 
        { 
            Role = "assistant", 
            Content = "你好！可以问我关于已收集文章的任何问题。" 
        });
    }

    private void LoadFeeds()
    {
        var feeds = DatabaseService.Instance.GetAllFeeds();
        Feeds.Clear();
        foreach (var feed in feeds)
        {
            Feeds.Add(feed);
        }

        if (Feeds.Any())
        {
            SelectedFeed = Feeds.First();
        }
        else
        {
            LoadAllArticles();
        }
    }

    private void LoadAllArticles()
    {
        var articles = DatabaseService.Instance.GetAllArticles();
        Articles.Clear();
        foreach (var article in articles)
        {
            Articles.Add(article);
        }
    }

    partial void OnSelectedFeedChanged(Feed? value)
    {
        if (value == null)
        {
            LoadAllArticles();
            return;
        }

        var articles = DatabaseService.Instance.GetArticlesByFeedId(value.Id);
        Articles.Clear();
        foreach (var article in articles)
        {
            Articles.Add(article);
        }
    }

    partial void OnSelectedArticleChanged(Article? value)
    {
        if (value != null)
        {
            IsWebViewVisible = true;
        }
        else
        {
            IsWebViewVisible = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAllAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        StatusMessage = "正在刷新所有订阅...";
        _refreshCts = new CancellationTokenSource();

        try
        {
            var feeds = DatabaseService.Instance.GetAllFeeds();
            var totalAdded = 0;

            foreach (var feed in feeds)
            {
                if (!feed.IsEnabled) continue;

                try
                {
                    var (parsedFeed, articles) = await _rssParser.ParseFeedAsync(feed.Url);
                    
                    feed.Title = parsedFeed.Title;
                    feed.Description ??= parsedFeed.Description;
                    feed.FaviconUrl ??= await _rssParser.GetFaviconAsync(feed.Url);
                    feed.LastUpdated = DateTime.Now;
                    
                    DatabaseService.Instance.UpdateFeed(feed);

                    var maxArticles = ConfigurationService.Instance.Settings.Rss.MaxArticlesPerFeed;
                    foreach (var article in articles.Take(maxArticles))
                    {
                        var existingArticle = DatabaseService.Instance.GetArticleByGuid(article.Guid ?? "", feed.Id);
                        if (existingArticle == null)
                        {
                            article.FeedId = feed.Id;
                            DatabaseService.Instance.AddArticle(article);
                            totalAdded++;
                        }
                    }

                    StatusMessage = $"已刷新: {feed.Title}, 新增: {totalAdded} 篇";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"刷新失败: {feed.Title} - {ex.Message}";
                }
            }

            StatusMessage = $"刷新完成，新增 {totalAdded} 篇文章";
            LoadFeeds();
        }
        finally
        {
            IsLoading = false;
            _refreshCts?.Dispose();
            _refreshCts = null;
        }
    }

    [RelayCommand]
    private async Task AddFeedAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFeedUrl))
        {
            MessageBox.Show("请输入订阅源 URL", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        StatusMessage = "正在添加订阅源...";

        try
        {
            var (feed, articles) = await _rssParser.ParseFeedAsync(NewFeedUrl);
            
            feed.Category = string.IsNullOrWhiteSpace(NewFeedCategory) ? "默认" : NewFeedCategory;
            feed.FaviconUrl ??= await _rssParser.GetFaviconAsync(NewFeedUrl);

            var addedFeed = DatabaseService.Instance.AddFeed(feed);

            var maxArticles = ConfigurationService.Instance.Settings.Rss.MaxArticlesPerFeed;
            foreach (var article in articles.Take(maxArticles))
            {
                article.FeedId = addedFeed.Id;
                DatabaseService.Instance.AddArticle(article);
            }

            StatusMessage = $"添加成功: {feed.Title}";
            NewFeedUrl = string.Empty;
            NewFeedCategory = string.Empty;
            
            LoadFeeds();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"添加订阅源失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "添加失败";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void DeleteFeed()
    {
        if (SelectedFeed == null) return;

        var result = MessageBox.Show($"确定要删除订阅源 \"{SelectedFeed.Title}\" 吗？", 
            "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            DatabaseService.Instance.DeleteFeed(SelectedFeed.Id);
            LoadFeeds();
            StatusMessage = "订阅源已删除";
        }
    }

    [RelayCommand]
    private async Task GenerateSummaryAsync()
    {
        if (SelectedArticle == null || HasApiKey == false) return;

        if (string.IsNullOrEmpty(SelectedArticle.Content) && string.IsNullOrEmpty(SelectedArticle.Description))
        {
            MessageBox.Show("文章内容为空，无法生成摘要", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        StatusMessage = "正在生成 AI 摘要...";

        try
        {
            var content = SelectedArticle.Content ?? SelectedArticle.Description ?? "";
            var summary = await LlmService.Instance.GenerateSummaryAsync(content);
            
            SelectedArticle.AiSummary = summary;
            DatabaseService.Instance.UpdateArticle(SelectedArticle);
            
            StatusMessage = "摘要生成完成";
            OnPropertyChanged(nameof(SelectedArticle));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"生成摘要失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void MarkAsRead()
    {
        if (SelectedArticle == null) return;

        SelectedArticle.IsRead = true;
        DatabaseService.Instance.UpdateArticle(SelectedArticle);
        OnPropertyChanged(nameof(SelectedArticle));
    }

    [RelayCommand]
    private void ToggleStar()
    {
        if (SelectedArticle == null) return;

        SelectedArticle.IsStarred = !SelectedArticle.IsStarred;
        DatabaseService.Instance.UpdateArticle(SelectedArticle);
        OnPropertyChanged(nameof(SelectedArticle));
    }

    [RelayCommand]
    private async Task GenerateEventLineAsync()
    {
        if (HasApiKey == false) return;

        IsLoading = true;
        StatusMessage = "正在生成事件线...";

        try
        {
            var articles = DatabaseService.Instance.GetAllArticles(100);
            
            DatabaseService.Instance.ClearEventLineItems();
            var items = await LlmService.Instance.GenerateEventLineAsync(articles);

            foreach (var item in items)
            {
                DatabaseService.Instance.AddEventLineItem(item);
            }

            var savedItems = DatabaseService.Instance.GetEventLineItems();
            EventLineItems.Clear();
            foreach (var item in savedItems)
            {
                EventLineItems.Add(item);
            }

            StatusMessage = $"事件线生成完成，共 {items.Count} 个事件";
            SelectedTabIndex = 1;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"生成事件线失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SendQuestionAsync()
    {
        if (string.IsNullOrWhiteSpace(QuestionText) || HasApiKey == false) return;

        var question = QuestionText;
        QuestionText = string.Empty;

        ChatMessages.Add(new ChatMessage { Role = "user", Content = question });

        IsLoading = true;
        StatusMessage = "AI 正在思考...";

        try
        {
            var articles = DatabaseService.Instance.GetAllArticles(100);
            var answer = await LlmService.Instance.AnswerQuestionAsync(question, articles);

            ChatMessages.Add(new ChatMessage { Role = "assistant", Content = answer });
            StatusMessage = "回答完成";
        }
        catch (Exception ex)
        {
            ChatMessages.Add(new ChatMessage { Role = "assistant", Content = $"回答失败: {ex.Message}" });
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearChat()
    {
        ChatMessages.Clear();
        ChatMessages.Add(new ChatMessage 
        { 
            Role = "assistant", 
            Content = "你好！可以问我关于已收集文章的任何问题。" 
        });
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsWindow = new Views.SettingsWindow();
        settingsWindow.ShowDialog();
        
        HasApiKey = LlmService.Instance.HasApiKey();
    }

    [RelayCommand]
    private void OpenArticleLink()
    {
        if (SelectedArticle?.Link == null) return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = SelectedArticle.Link,
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show("无法打开链接", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
