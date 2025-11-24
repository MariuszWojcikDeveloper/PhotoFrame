using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PhotoFrame.DataAccess;
using PhotoFrame.Services;
using System;
using System.IO;

namespace PhotoFrame;

public partial class App : Application
{
    public ServiceProvider? ServiceProvider { get; private set; }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ConfigureServices();
    }
    
    private void ConfigureServices()
    {
        var services = new ServiceCollection();
        
        // Build configuration - environment variables override JSON config
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("photoframe-config.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "PHOTOFRAME_")  // Environment variables with PHOTOFRAME_ prefix
            .Build();
            
        services.AddSingleton<IConfiguration>(configuration);
        
        // Configure PhotoFrameConfig with validation
        services.Configure<PhotoFrameConfig>(configuration.GetSection(PhotoFrameConfig.SectionName));
        services.AddSingleton<IValidateOptions<PhotoFrameConfig>, PhotoFrameConfigValidation>();
        
        // Initialize logging based on configuration
        var configSection = configuration.GetSection(PhotoFrameConfig.SectionName);
        var enableLogging = configSection.GetValue<bool>("EnableLogging", true);
        var logFilePath = configSection.GetValue<string>("LogFilePath", "photoframe.log");
        Logger.Initialize(enableLogging, logFilePath);
        
        // Register Tapo Smart Plug monitoring
        // Credentials can be set via environment variables:
        // Windows PowerShell: $env:PHOTOFRAME_PhotoFrame__TapoEmail = "your@email.com"
        // Linux/macOS: export PHOTOFRAME_PhotoFrame__TapoEmail="your@email.com"
        var tapoEmail = configSection.GetValue<string>("TapoEmail");
        var tapoPassword = configSection.GetValue<string>("TapoPassword");
        var tapoDeviceIp = configSection.GetValue<string>("TapoDeviceIp");
        services.AddSingleton<ITapoSmartPlugMonitor>(sp => new TapoSmartPlugMonitor(tapoEmail, tapoPassword, tapoDeviceIp));
        
        // Register business logic services
        services.AddSingleton<IScreenMonitoringService, ScreenMonitoringService>();
        services.AddSingleton<ISlideshowCoordinator, SlideshowCoordinator>();
        
        // Configure SQLite database
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "media.db");
        services.AddDbContext<MediaDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));
        
        // Register application services
        services.AddSingleton<DatabaseMediaRepository>();
        services.AddSingleton<CacheManager>(sp => 
        {
            var config = sp.GetRequiredService<IOptions<PhotoFrameConfig>>();
            var repository = sp.GetRequiredService<DatabaseMediaRepository>();
            return new CacheManager(config, repository, MediaExtensions.AllExtensions);
        });
        services.AddSingleton<MediaFileManager>();
        services.AddTransient<MainWindow>();
        
        ServiceProvider = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Get MainWindow from DI
            desktop.MainWindow = ServiceProvider!.GetRequiredService<MainWindow>();
            
            // Handle application shutdown
            desktop.Exit += (sender, e) => ServiceProvider?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}