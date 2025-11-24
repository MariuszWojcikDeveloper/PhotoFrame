using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace PhotoFrame.Services;

public class SlideshowCoordinator : ISlideshowCoordinator
{
    private readonly MediaFileManager _mediaFileManager;
    private readonly IScreenMonitoringService _screenMonitoringService;
    private DispatcherTimer? _slideshowTimer;
    private bool _isSlideshowEnabled = true;

    public event EventHandler<CurrentMedia>? MediaChanged;

    public SlideshowCoordinator(
        MediaFileManager mediaFileManager,
        IScreenMonitoringService screenMonitoringService)
    {
        _mediaFileManager = mediaFileManager ?? throw new ArgumentNullException(nameof(mediaFileManager));
        _screenMonitoringService = screenMonitoringService ?? throw new ArgumentNullException(nameof(screenMonitoringService));

        // Wire up screen state events
        _screenMonitoringService.ScreenStateChanged += (s, isScreenOn) =>
        {
            if (isScreenOn)
            {
                OnScreenTurnedOn();
            }
            else
            {
                OnScreenTurnedOff();
            }
        };
    }

    public async Task StartAsync()
    {
        _mediaFileManager.LoadPhotosFromConfig();
        _screenMonitoringService.Initialize(_mediaFileManager.SlideshowInterval);
        StartSlideshowTimer(_mediaFileManager.SlideshowInterval);
        
        Logger.WriteLine("SlideshowCoordinator: Starting slideshow");
        await ShowNextMediaAsync();
    }

    private void StartSlideshowTimer(double intervalSeconds)
    {
        if (_slideshowTimer == null)
        {
            _slideshowTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(intervalSeconds)
            };
            _slideshowTimer.Tick += async (s, e) =>
            {
                Logger.WriteLine("SlideshowCoordinator: Slideshow timer tick - advancing to next media");
                await ShowNextMediaAsync();
            };
        }
        else
        {
            _slideshowTimer.Interval = TimeSpan.FromSeconds(intervalSeconds);
        }

        if (!_slideshowTimer.IsEnabled)
        {
            _slideshowTimer.Start();
            Logger.WriteLine($"SlideshowCoordinator: Slideshow timer started (interval: {intervalSeconds}s)");
        }
    }

    private void StopSlideshowTimer()
    {
        if (_slideshowTimer?.IsEnabled == true)
        {
            _slideshowTimer.Stop();
            Logger.WriteLine("SlideshowCoordinator: Slideshow timer stopped");
        }
    }

    private bool IsSlideshowTimerRunning => _slideshowTimer?.IsEnabled == true;

    public async Task ShowNextMediaAsync()
    {
        Logger.WriteLine("SlideshowCoordinator: Getting next media");

        // Check if screen is on before proceeding
        var isScreenOn = await _screenMonitoringService.CheckScreenStateAsync();
        if (!isScreenOn)
        {
            Logger.WriteLine("SlideshowCoordinator: Screen is off, not advancing media");
            return; // ScreenMonitoringService handles the state change
        }

        var nextMedia = _mediaFileManager.GetNextMedia();
        if (nextMedia?.CurrentMedia == null)
        {
            Logger.WriteLine("SlideshowCoordinator: No media available");
            return;
        }

        // Stop timer if next media is a video (videos handle their own timing)
        if (nextMedia.CurrentMedia.IsVideo)
        {
            Logger.WriteLine("SlideshowCoordinator: Next media is video, stopping slideshow timer");
            StopSlideshowTimer();
        }
        else
        {
            // For images, ensure timer is running if slideshow is enabled
            if (_isSlideshowEnabled && !IsSlideshowTimerRunning)
            {
                Logger.WriteLine("SlideshowCoordinator: Next media is image, starting slideshow timer");
                StartSlideshowTimer(_mediaFileManager.SlideshowInterval);
            }
        }

        // Notify UI to display the media
        MediaChanged?.Invoke(this, nextMedia.CurrentMedia);
    }

    public void ToggleSlideshow()
    {
        _isSlideshowEnabled = !_isSlideshowEnabled;
        
        if (_isSlideshowEnabled)
        {
            StartSlideshowTimer(_mediaFileManager.SlideshowInterval);
        }
        else
        {
            StopSlideshowTimer();
        }
        
        Logger.WriteLine($"SlideshowCoordinator: Slideshow toggled - IsEnabled: {_isSlideshowEnabled}");
    }

    public void OnVideoCompleted()
    {
        if (_isSlideshowEnabled)
        {
            Logger.WriteLine("SlideshowCoordinator: Video completed, scheduling next media");
            _ = ShowNextMediaAsync(); // Fire and forget
        }
    }

    public void OnVideoCancelled()
    {
        if (_isSlideshowEnabled && !IsSlideshowTimerRunning)
        {
            StartSlideshowTimer(_mediaFileManager.SlideshowInterval);
            Logger.WriteLine("SlideshowCoordinator: Video cancelled, slideshow timer restarted");
        }
    }

    private void OnScreenTurnedOn()
    {
        Logger.WriteLine("SlideshowCoordinator: Screen turned on, resuming slideshow");
        if (_isSlideshowEnabled && !IsSlideshowTimerRunning)
        {
            StartSlideshowTimer(_mediaFileManager.SlideshowInterval);
            _ = ShowNextMediaAsync(); // Show media immediately when screen turns on
        }
    }

    private void OnScreenTurnedOff()
    {
        Logger.WriteLine("SlideshowCoordinator: Screen turned off, pausing slideshow");
        StopSlideshowTimer();
        _screenMonitoringService.StartPeriodicCheck();
    }

    public SlideshowContext? GetCurrentMediaInfo()
    {
        return _mediaFileManager.GetCurrentMediaInfo();
    }

    public bool IsSlideshowEnabled => _isSlideshowEnabled;
    public double SlideshowInterval => _mediaFileManager.SlideshowInterval;
}
