using System.Windows;
using System.Windows.Controls;
using RSSReader.Services;

namespace RSSReader.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = ConfigurationService.Instance.Settings;

        ApiKeyBox.Password = settings.GlmApi.ApiKey;
        
        TemperatureSlider.Value = settings.GlmApi.Temperature;
        MaxTokensBox.Text = settings.GlmApi.MaxTokens.ToString();
        RefreshIntervalBox.Text = settings.Rss.RefreshIntervalMinutes.ToString();
        MaxArticlesBox.Text = settings.Rss.MaxArticlesPerFeed.ToString();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = ConfigurationService.Instance.Settings;

        settings.GlmApi.ApiKey = ApiKeyBox.Password;
        settings.GlmApi.Model = "glm-4.7-flash";
        settings.GlmApi.Temperature = TemperatureSlider.Value;
        
        if (int.TryParse(MaxTokensBox.Text, out int maxTokens))
        {
            settings.GlmApi.MaxTokens = maxTokens;
        }

        if (int.TryParse(RefreshIntervalBox.Text, out int refreshInterval))
        {
            settings.Rss.RefreshIntervalMinutes = refreshInterval;
        }

        if (int.TryParse(MaxArticlesBox.Text, out int maxArticles))
        {
            settings.Rss.MaxArticlesPerFeed = maxArticles;
        }

        ConfigurationService.Instance.SaveSettings();
        
        MessageBox.Show("设置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
