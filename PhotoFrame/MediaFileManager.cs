    using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Options;
using PhotoFrame.DataAccess;

namespace PhotoFrame;

public class MediaFileManager
{
    private MediaFile? _currentMedia;
    private readonly PhotoFrameConfig _config;
    private readonly DatabaseMediaRepository _repository;
    private readonly CacheManager _cacheManager;
    private readonly Random _random = new Random();
    private DateTime? _networkUnavailableUntil = null;
    
    public int SlideshowInterval => _config.SlideshowInterval;    
    public string? CurrentFile => _currentMedia != null ? _cacheManager.GetLocalPath(_currentMedia) : null;
    public bool IsNetworkUnavailable => _networkUnavailableUntil.HasValue && DateTime.Now < _networkUnavailableUntil.Value;
    public DateTime? NetworkUnavailableUntil => _networkUnavailableUntil;

    public MediaFileManager(IOptions<PhotoFrameConfig> config, DatabaseMediaRepository repository, CacheManager cacheManager)
    {
        _config = config.Value;
        _repository = repository;
        _cacheManager = cacheManager;
        
        // Validate configuration
        var validator = new PhotoFrameConfigValidation();
        var validationResult = validator.Validate(null, _config);
        
        if (validationResult.Failed)
        {
            var errors = string.Join(", ", validationResult.Failures);
            throw new InvalidOperationException($"Configuration validation failed: {errors}");
        }
    }
    
    public void LoadPhotosFromConfig()
    {        
        Logger.WriteLine("MediaFileManager: LoadPhotosFromConfig starting...");
        
        try
        {
            Logger.WriteLine($"MediaFileManager: Using folder: {_config.FolderPath}");
            Logger.WriteLine($"MediaFileManager: Slideshow interval: {_config.SlideshowInterval}s");
            
            // Initialize database
            _repository.EnsureDatabaseCreated();
            
            // Ensure local storage folder exists
            _cacheManager.EnsureCacheFolderExists();
            
            // Sync local storage with database
            _cacheManager.CleanupLocalStorage();
            
            // Scan and dump photo list from NetworkLocationPhoto if configured
            if (!string.IsNullOrWhiteSpace(_config.NetworkLocationPhoto))
            {
                //ScanAndDumpPhotoList(_config.NetworkLocationPhoto);
            }
            
            // Scan and dump video list from NetworkLocationVideos if configured
            if (!string.IsNullOrWhiteSpace(_config.NetworkLocationVideos))
            {
                //ScanAndDumpVideoList(_config.NetworkLocationVideos);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"MediaFileManager: Error loading from config: {ex.Message}", ex);
            throw;
        }
    }

    private void ScanAndDumpPhotoList(string folderPath)
    {
        try
        {
            Logger.WriteLine($"MediaFileManager: Scanning photos from {folderPath}");
            
            if (!Directory.Exists(folderPath))
            {
                Logger.WriteLine($"MediaFileManager: NetworkLocationPhoto directory does not exist: {folderPath}");
                return;
            }

            var photoFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(file => MediaExtensions.PhotoExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .OrderBy(file => file)
                .ToList();

            Logger.WriteLine($"MediaFileManager: Found {photoFiles.Count} photo files");

            if (photoFiles.Count == 0)
            {
                Logger.WriteLine("MediaFileManager: No photo files found to dump");
                return;
            }
            
            // Save to database
            _repository.SaveFiles(photoFiles);
        }
        catch (Exception ex)
        {
            Logger.LogError($"MediaFileManager: Error scanning and dumping photo list: {ex.Message}", ex);
            // Don't throw - this is not critical to the application
        }
    }

    private void ScanAndDumpVideoList(string folderPath)
    {
        try
        {
            Logger.WriteLine($"MediaFileManager: Scanning videos from {folderPath}");
            
            if (!Directory.Exists(folderPath))
            {
                Logger.WriteLine($"MediaFileManager: NetworkLocationVideos directory does not exist: {folderPath}");
                return;
            }

            var videoFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(file => MediaExtensions.VideoExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .OrderBy(file => file)
                .ToList();

            Logger.WriteLine($"MediaFileManager: Found {videoFiles.Count} video files");

            if (videoFiles.Count == 0)
            {
                Logger.WriteLine("MediaFileManager: No video files found to dump");
                return;
            }
            
            // Save to database
            _repository.SaveFiles(videoFiles);
        }
        catch (Exception ex)
        {
            Logger.LogError($"MediaFileManager: Error scanning and dumping video list: {ex.Message}", ex);
            // Don't throw - this is not critical to the application
        }
    }

    public SlideshowContext? GetNextMedia()
    {
        // Check if network unavailability period has expired
        if (_networkUnavailableUntil.HasValue && DateTime.Now >= _networkUnavailableUntil.Value)
        {
            Logger.WriteLine("MediaFileManager: Network unavailability period expired, resuming normal mode");
            _networkUnavailableUntil = null;
        }

        // If network is unavailable, force cache-only mode
        // If nothing is cached yet, force network load regardless of percentage
        var hasCachedMedia = _repository.GetAllCachedMedia().Any();
        var randomNumber = _random.Next(0, 100);
        Logger.WriteLine($"MediaFileManager: Random number for network load decision: {randomNumber}");
        bool loadFromNetwork = !IsNetworkUnavailable && (!hasCachedMedia || randomNumber <= _config.NetworkRefreshPercentage);

        if (loadFromNetwork)
        {
            // Get a random media file from database that hasn't been cached yet (TimesShown = 0)
            var uncachedRandomMedia = _repository.GetRandomUncachedMedia();

            if (uncachedRandomMedia == null)
            {
                // No uncached media available, fall back to cached media
                Logger.WriteLine("MediaFileManager: No uncached media available, falling back to cached media");
            }
            else
            {
                Logger.WriteLine("MediaFileManager: Loading new media from network");

                // Try to copy the file to cache
                bool isFileInCache = _cacheManager.CopyFileToCache(
                    uncachedRandomMedia,
                    CheckNetworkDriveAvailability,
                    () => _networkUnavailableUntil = DateTime.Now.AddHours(1)
                );

                if (!isFileInCache)
                {
                    // Failed to copy from network, try to get cached media instead
                    Logger.WriteLine("MediaFileManager: Failed to load from network, falling back to cached media");
                } else {
                    _repository.IncrementTimesShown(uncachedRandomMedia.Id);
                    _currentMedia = uncachedRandomMedia;

                    return GetCurrentMediaInfo();
                }
            }
        }
                
        // Get a random media file from database that is already cached (TimesShown > 0)
        var cachedRandomMedia = _repository.GetRandomCachedMedia();
            
        if (cachedRandomMedia == null)
        {
            Logger.WriteLine("MediaFileManager: No cached media available in database");            
            throw new InvalidOperationException("No cached media available");
        }                 
        
        // Update TimesShown in database
        _repository.IncrementTimesShown(cachedRandomMedia.Id);
        
        // Set as current media
        _currentMedia = cachedRandomMedia;
        
        return GetCurrentMediaInfo();
    }

    public SlideshowContext? GetCurrentMediaInfo()
    {
        var cacheFilePath = CurrentFile!;
        var fileInfo = new FileInfo(cacheFilePath);
        
        // Calculate cache statistics
        var (cacheSizeBytes, cacheFileCount) = _cacheManager.GetCurrentCacheSize();
        var cacheSizeGB = cacheSizeBytes / (1024.0 * 1024.0 * 1024.0);
        
        var networkPath = _currentMedia!.Path;
        var extension = Path.GetExtension(networkPath);
        var fileName = Path.GetFileName(networkPath);
        var cacheFileName = Path.GetFileName(cacheFilePath);
        
        return new SlideshowContext
        {
            CurrentMedia = new CurrentMedia
            {
                NetworkPath = networkPath,
                FileName = fileName,
                FileExtension = extension,
                CacheFileName = cacheFileName,
                CacheFilePath = cacheFilePath,
                FileSizeKB = fileInfo.Length / 1024,
                TimesShown = _currentMedia.TimesShown,
                IsVideo = IsVideoFile(cacheFilePath)
            },
            CacheFileCount = cacheFileCount,
            CacheSizeGB = cacheSizeGB,
            IsNetworkUnavailable = IsNetworkUnavailable,
            NetworkUnavailableUntil = _networkUnavailableUntil
        };
    }

    public bool IsVideoFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return MediaExtensions.VideoExtensions.Contains(extension);
    }

    private bool CheckNetworkDriveAvailability()
    {
        if (string.IsNullOrWhiteSpace(_config.NetworkDriveControlFile))
        {
            Logger.WriteLine("MediaFileManager: NetworkDriveControlFile not configured, assuming network is available");
            return true;
        }
        
        try
        {
            Logger.WriteLine($"MediaFileManager: Checking network availability via control file: {_config.NetworkDriveControlFile}");
            
            // Try to read the control file
            if (File.Exists(_config.NetworkDriveControlFile))
            {
                Logger.WriteLine("MediaFileManager: Network drive is available");
                return true;
            }
            else
            {
                Logger.LogWarning($"MediaFileManager: Network control file not found: {_config.NetworkDriveControlFile}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"MediaFileManager: Failed to access network control file: {ex.Message}");
            return false;
        }
    }

}
