using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using RSSReader.Services;

namespace RSSReader;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        InitializeServices();
    }

    private void InitializeServices()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "appsettings.json");
        
        if (!Directory.Exists(Path.GetDirectoryName(configPath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? string.Empty);
        }

        if (!File.Exists(configPath))
        {
            var defaultConfig = @"{
  ""GlmApi"": {
    ""ApiKey"": """",
    ""Model"": ""glm-4-flash"",
    ""MaxTokens"": 2048,
    ""Temperature"": 0.7
  },
  ""Rss"": {
    ""RefreshIntervalMinutes"": 30,
    ""MaxArticlesPerFeed"": 100
  }
}";
            File.WriteAllText(configPath, defaultConfig);
        }

        var _ = ConfigurationService.Instance;
        var _2 = DatabaseService.Instance;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"发生错误: {e.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show($"发生严重错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
