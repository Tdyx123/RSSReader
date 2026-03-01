using System;
using System.Windows;
using System.Windows.Controls;
using RSSReader.Models;

namespace RSSReader.Views;

public partial class ArticleWebViewPanel : UserControl
{
    public static readonly DependencyProperty ArticleProperty =
        DependencyProperty.Register(nameof(Article), typeof(Article), typeof(ArticleWebViewPanel),
            new PropertyMetadata(null, OnArticleChanged));

    public Article? Article
    {
        get => (Article?)GetValue(ArticleProperty);
        set => SetValue(ArticleProperty, value);
    }

    public event EventHandler? BackRequested;

    public ArticleWebViewPanel()
    {
        InitializeComponent();
    }

    private static void OnArticleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArticleWebViewPanel panel && e.NewValue is Article article)
        {
            panel.DisplayArticle(article);
        }
    }

    private void DisplayArticle(Article article)
    {
        TitleText.Text = article.Title;
        ArticleTitle.Text = article.Title;

        if (article.PublishDate.HasValue)
        {
            PublishDateText.Text = article.PublishDate.Value.ToString("yyyy-MM-dd HH:mm");
        }
        else
        {
            PublishDateText.Text = "";
        }

        FeedTitleText.Text = article.Feed?.Title ?? "";

        var content = article.Content ?? article.Description ?? "<p>暂无内容</p>";
        
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            font-size: 14px;
            line-height: 1.6;
            color: #374151;
            padding: 16px;
            margin: 0;
            background: #ffffff;
        }}
        img {{
            max-width: 100%;
            height: auto;
        }}
        a {{
            color: #2563EB;
            text-decoration: none;
        }}
        pre, code {{
            background: #F3F4F6;
            padding: 2px 6px;
            border-radius: 4px;
            font-family: Consolas, monospace;
        }}
        pre {{
            padding: 12px;
            overflow-x: auto;
        }}
        blockquote {{
            border-left: 4px solid #E5E7EB;
            margin-left: 0;
            padding-left: 16px;
            color: #6B7280;
        }}
    </style>
</head>
<body>
    {content}
</body>
</html>";

        try
        {
            HtmlContent.NavigateToString(html);
        }
        catch { }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenLinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (Article?.Link != null)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Article.Link,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
