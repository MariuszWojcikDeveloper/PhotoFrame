using Avalonia;
using System;

namespace PhotoFrame;

class Program
{
    public static string[]? CommandLineArgs { get; private set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            Logger.WriteLine("Program: Starting PhotoFrame application...");            
            
            var app = BuildAvaloniaApp();
            Logger.WriteLine("Program: Starting with classic desktop lifetime...");
            
            app.StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Program: Fatal error: {ex.Message}", ex);
            Environment.Exit(1);
        }
        finally
        {
            Logger.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
