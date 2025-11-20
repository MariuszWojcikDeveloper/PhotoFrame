using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace PhotoFrame.Services;

public class ScreenMonitoringService : IScreenMonitoringService
{
    private readonly ITapoSmartPlugMonitor _tapoMonitor;
    private DispatcherTimer? _checkTimer;
    private bool _screenWasOff = false;

    public event EventHandler<bool>? ScreenStateChanged;

    public ScreenMonitoringService(ITapoSmartPlugMonitor tapoMonitor)
    {
        _tapoMonitor = tapoMonitor ?? throw new ArgumentNullException(nameof(tapoMonitor));
    }

    public void Initialize(double checkIntervalSeconds)
    {
        _checkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(checkIntervalSeconds)
        };
        _checkTimer.Tick += async (s, e) => await OnCheckTimerTickAsync();
        
        Logger.WriteLine("ScreenMonitoringService: Initialized");
    }

    public async Task<bool> CheckScreenStateAsync()
    {
        Logger.WriteLine("ScreenMonitoringService: Checking screen state...");
        var isScreenOn = await _tapoMonitor.IsScreenOnAsync();
        
        // Detect state changes
        if (!isScreenOn && !_screenWasOff)
        {
            // Screen just turned off
            _screenWasOff = true;
            Logger.WriteLine("ScreenMonitoringService: Screen turned OFF");
            ScreenStateChanged?.Invoke(this, false);
        }
        else if (isScreenOn && _screenWasOff)
        {
            // Screen just turned on
            _screenWasOff = false;
            Logger.WriteLine("ScreenMonitoringService: Screen turned ON");
            ScreenStateChanged?.Invoke(this, true);
        }
        
        return isScreenOn;
    }

    public void StartPeriodicCheck()
    {
        if (_checkTimer != null && !_checkTimer.IsEnabled)
        {
            _checkTimer.Start();
            Logger.WriteLine("ScreenMonitoringService: Started periodic checking");
        }
    }

    public void StopPeriodicCheck()
    {
        if (_checkTimer?.IsEnabled == true)
        {
            _checkTimer.Stop();
            Logger.WriteLine("ScreenMonitoringService: Stopped periodic checking");
        }
    }

    private async Task OnCheckTimerTickAsync()
    {
        // Timer only runs when screen is off, checking if it's back on
        var isScreenOn = await _tapoMonitor.IsScreenOnAsync();
        
        if (isScreenOn && _screenWasOff)
        {
            // Screen just turned back on
            _screenWasOff = false;
            StopPeriodicCheck();
            Logger.WriteLine("ScreenMonitoringService: Screen turned ON (detected by timer)");
            ScreenStateChanged?.Invoke(this, true);
        }
    }
}
