using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using PhotoFrame.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PhotoFrame;

public partial class MainWindow : Window
{
    private readonly ISlideshowCoordinator _mediaCoordinator;
    private VideoPlayer? _videoPlayer;
    private int _mediaDisplayedCount = 0;
    private DispatcherTimer? _cursorHideTimer;
    private DispatcherTimer? _infoPanelHideTimer;

    public MainWindow(ISlideshowCoordinator mediaCoordinator)
    {
        try
        {
            Logger.WriteLine("MainWindow: Starting initialization...");
            InitializeComponent();
            Logger.WriteLine("MainWindow: InitializeComponent completed");

            // Initialize dependencies via dependency injection
            _mediaCoordinator = mediaCoordinator ?? throw new ArgumentNullException(nameof(mediaCoordinator));
            // Initialize video player
            Logger.WriteLine("MainWindow: Initializing video player...");
            _videoPlayer = new VideoPlayer();
            Logger.WriteLine("MainWindow: Video player initialized successfully");

            // Setup cursor auto-hide
            SetupCursorAutoHide();
            
            // Subscribe to media changes
            _mediaCoordinator.MediaChanged += async (s, media) => await ShowMediaAsync(media);

            Closing += (sender, e) =>
            {
                Logger.WriteLine("MainWindow: Window closing, stopping services...");
                _videoPlayer?.Stop();
                _videoPlayer = null;
                _cursorHideTimer?.Stop();
            };

            Logger.WriteLine("MainWindow: Constructor completed successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError($"MainWindow: Constructor failed: {ex.Message}", ex);
            throw;
        }
    }

    private void SetupCursorAutoHide()
    {
        // Hide cursor initially
        Cursor = new Cursor(StandardCursorType.None);

        // Setup timer to hide cursor after 3 seconds of inactivity
        _cursorHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _cursorHideTimer.Tick += (s, e) =>
        {
            Cursor = new Cursor(StandardCursorType.None);
            _cursorHideTimer.Stop();
        };

        // Show cursor on mouse move
        PointerMoved += (s, e) =>
        {
            Cursor = new Cursor(StandardCursorType.Arrow);
            _cursorHideTimer.Stop();
            _cursorHideTimer.Start();
        };
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Logger.WriteLine("MainWindow: Window opened, starting slideshow...");
        await _mediaCoordinator.StartAsync();
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.Right:
                await _mediaCoordinator.ShowNextMediaAsync();
                break;
            case Key.Space:
                _mediaCoordinator.ToggleSlideshow();
                UpdateMediaInfo();
                break;
            case Key.F11:
                WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
                break;
        }
    }

    private async Task ShowMediaAsync(CurrentMedia media)
    {
        UpdateMediaInfo();

        Logger.WriteLine($"ShowMediaAsync: Displaying {(media.IsVideo ? "video" : "image")}: {media.FileName}");
        
        if (media.IsVideo)
        {
            await ShowVideoAsync(media);            
            return;
        }
        else
        {
            ShowImage(media.CacheFilePath);
        }
        
        // Increment displayed photos count
        _mediaDisplayedCount++;
        Logger.WriteLine($"ShowMediaAsync: Media displayed count: {_mediaDisplayedCount}");
        
        // Hide instructions only after 2nd media is displayed
        if (_mediaDisplayedCount >= 2)
        {
            LoadingText.IsVisible = false;
            Logger.WriteLine("ShowMediaAsync: Instructions hidden");
        }
    }

    private void ShowImage(string imagePath)
    {        
        using var stream = File.OpenRead(imagePath);
        var bitmap = new Bitmap(stream);
        PhotoImage.Source = bitmap;
        
        // Read EXIF orientation and apply rotation
        var rotation = GetExifRotation(imagePath);
        if (rotation != 0)
        {
            PhotoImage.RenderTransform = new RotateTransform(rotation);
            Logger.WriteLine($"ShowImage: Applied {rotation}° rotation based on EXIF orientation");
        }
        else
        {
            PhotoImage.RenderTransform = null;
        }
        
        PhotoImage.IsVisible = true;
        VideoContainer.IsVisible = false;
        
        Logger.WriteLine($"ShowImage: Displaying image: {Path.GetFileName(imagePath)}");
    }

    private double GetExifRotation(string imagePath)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(imagePath);
            var exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            
            if (exifIfd0Directory == null)
                return 0;
            
            if (exifIfd0Directory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientation))
            {
                return orientation switch
                {
                    1 => 0,    // Normal
                    3 => 180,  // Rotate 180
                    6 => 90,   // Rotate 90 CW
                    8 => 270,  // Rotate 270 CW (or 90 CCW)
                    _ => 0
                };
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"GetExifRotation: Could not read EXIF data: {ex.Message}");
        }
        
        return 0;
    }

    private async Task ShowVideoAsync(CurrentMedia media)
    {
        // Hide image and show video placeholder
        PhotoImage.IsVisible = false;
        VideoContainer.IsVisible = true;
        VideoStatusText.Text = $"Playing: {media.NetworkPath}";
        VideoStatusText.IsVisible = true;
        
        // Wait 5 seconds before starting video
        await Task.Delay(5000);
        
        Logger.WriteLine($"ShowVideoAsync: Starting video playback: {media.NetworkPath}");
        
        // Play video via VLC process and wait for completion
        bool completedNormally = await _videoPlayer!.PlayVideoAsync(media.CacheFilePath);
        Logger.WriteLine($"ShowVideoAsync: Video playback ended (completed normally: {completedNormally})");
        
        // Hide video container after playback
        VideoContainer.IsVisible = false;
        VideoStatusText.IsVisible = false;
        
        // Notify coordinator
        if (completedNormally)
        {
            _mediaCoordinator.OnVideoCompleted();
        }
        else
        {
            _mediaCoordinator.OnVideoCancelled();
        }
    }

    private void UpdateMediaInfo()
    {
        var mediaInfo = _mediaCoordinator.GetCurrentMediaInfo();
        if (mediaInfo == null)
            return;
                    
        InfoPanel.IsVisible = true;
        var mediaType = mediaInfo.CurrentMedia!.IsVideo ? "Video" : "Photo";
        
        var slideshowStatus = _mediaCoordinator.IsSlideshowEnabled ? "Slideshow" : "Manual";
        
        var baseInfo = $"{mediaType}: {mediaInfo.CurrentMedia.NetworkPath} | {mediaInfo.CurrentMedia.FileSizeKB:N0} KB | Shown: {mediaInfo.CurrentMedia.TimesShown}x | {slideshowStatus}\nCache size: {mediaInfo.CacheFileCount} files, {mediaInfo.CacheSizeGB:F2} GB";
        
        // Add network status if unavailable
        if (mediaInfo.IsNetworkUnavailable && mediaInfo.NetworkUnavailableUntil.HasValue)
        {
            var timeRemaining = mediaInfo.NetworkUnavailableUntil.Value - DateTime.Now;
            var minutesRemaining = (int)timeRemaining.TotalMinutes;
            baseInfo += $"\n⚠️ NETWORK UNAVAILABLE - Cache-only mode for {minutesRemaining} more minutes";
        }
        
        PhotoInfoText.Text = baseInfo;
        
        // Hide info panel after 10% of slideshow interval, but at least 5 seconds
        _infoPanelHideTimer?.Stop();
        var hideDelay = Math.Max(5, _mediaCoordinator.SlideshowInterval * 0.1);
        _infoPanelHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(hideDelay)
        };
        _infoPanelHideTimer.Tick += (s, e) =>
        {
            InfoPanel.IsVisible = false;
            _infoPanelHideTimer.Stop();
        };
        _infoPanelHideTimer.Start();
    }
}
