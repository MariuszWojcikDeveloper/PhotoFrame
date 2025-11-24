using Serilog;
using Serilog.Enrichers;
using System;
using System.IO;

namespace PhotoFrame;

public static class Logger
{
    private static ILogger _logger = CreateNullLogger();
    private static bool _isInitialized = false;
    
    private static ILogger CreateNullLogger()
    {
        return new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
    }
    
    public static void Initialize(bool enableLogging, string? logFilePath = null)
    {
        if (_isInitialized) return;
        
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .Enrich.WithThreadId();
            
        if (enableLogging)
        {
            var logPath = !string.IsNullOrWhiteSpace(logFilePath)
                ? Path.GetFullPath(logFilePath)
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PhotoFrame.log");

            // Configure Serilog to roll on file size limit and keep only one file
            loggerConfig.WriteTo.File(
                logPath,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [Thread {ThreadId}] {Message:lj}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Infinite,
                fileSizeLimitBytes: 10_485_760, // 10 MB limit
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 1
            );
        }
        
        _logger = loggerConfig.CreateLogger();
        _isInitialized = true;
        
        if (enableLogging)
        {
            _logger.Information("PhotoFrame Application Started - Logging Enabled");
        }
        else
        {
            _logger.Information("PhotoFrame Application Started - File Logging Disabled");
        }
    }
    
    public static void WriteLine(string message)
    {
        _logger.Information(message);
    }
    
    public static void WriteLine(string format, params object[] args)
    {
        _logger.Information(format, args);
    }
    
    public static void LogError(string message, Exception? exception = null)
    {
        _logger.Error(exception, message);
    }
    
    public static void LogWarning(string message)
    {
        _logger.Warning(message);
    }
    
    public static void LogDebug(string message)
    {
        _logger.Debug(message);
    }
    
    public static string GetLogFilePath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PhotoFrame.log");
    }
    
    public static bool IsInitialized => _isInitialized;
    
    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }
}