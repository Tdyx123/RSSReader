using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using RSSReader.Models;

namespace RSSReader.Services;

public class ConfigurationService
{
    private static ConfigurationService? _instance;
    public static ConfigurationService Instance => _instance ??= new ConfigurationService();

    private AppSettings _settings = new();
    public AppSettings Settings => _settings;

    private ConfigurationService()
    {
        LoadConfiguration();
    }

    private void LoadConfiguration()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "appsettings.json");
        
        if (!File.Exists(configPath))
        {
            configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        }

        if (File.Exists(configPath))
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(configPath) ?? AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            _settings = configuration.Get<AppSettings>() ?? new AppSettings();
        }
    }

    public void Reload()
    {
        LoadConfiguration();
    }

    public void SaveSettings()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "appsettings.json");
        
        if (!Directory.Exists(Path.GetDirectoryName(configPath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? string.Empty);
        }

        var json = System.Text.Json.JsonSerializer.Serialize(_settings, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        File.WriteAllText(configPath, json);
    }

    public string GetApiKey() => _settings.GlmApi.ApiKey;
    public string GetModel() => _settings.GlmApi.Model;
}
