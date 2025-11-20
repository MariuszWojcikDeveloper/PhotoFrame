using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Options;
using PhotoFrame.DataAccess;

namespace PhotoFrame;

public class CacheManager
{
    private readonly PhotoFrameConfig _config;
    private readonly DatabaseMediaRepository _repository;
    private readonly string[] _supportedExtensions;

    public CacheManager(IOptions<PhotoFrameConfig> config, DatabaseMediaRepository repository, string[] supportedExtensions)
    {
        _config = config.Value;
        _repository = repository;
        _supportedExtensions = supportedExtensions;
    }

    public void EnsureCacheFolderExists()
    {
        if (!Directory.Exists(_config.FolderPath))
        {
            Directory.CreateDirectory(_config.FolderPath);
            Logger.WriteLine($"CacheManager: Created local storage folder: {_config.FolderPath}");
        }
    }

    public void CleanupLocalStorage()
    {
        Logger.WriteLine("CacheManager: Starting local storage cleanup");
        
        var localFiles = Directory.GetFiles(_config.FolderPath, "*.*", SearchOption.AllDirectories)
            .Where(file => _supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
            .ToList();
        
        int removedCount = 0;
        int resetCount = 0;
        
        // Check each local file
        foreach (var localFile in localFiles)
        {
            var fileName = Path.GetFileName(localFile);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            
            // Try to parse the filename as a database ID
            if (int.TryParse(fileNameWithoutExt, out int mediaId))
            {
                var media = _repository.FindById(mediaId);
                
                if (media == null || media.TimesShown == 0)
                {
                    // File exists but TimesShown is 0 or null - remove the file
                    File.Delete(localFile);
                    removedCount++;
                    Logger.WriteLine($"CacheManager: Removed local file with TimesShown=0: {localFile}");
                }
            }
            else
            {
                // Filename is not a valid ID - remove it
                File.Delete(localFile);
                removedCount++;
                Logger.WriteLine($"CacheManager: Removed local file with invalid name: {localFile}");
            }
        }
        
        // Check database for files that should be reset
        var mediaWithTimesShown = _repository.GetAllCachedMedia();
        
        foreach (var media in mediaWithTimesShown)
        {
            var extension = Path.GetExtension(media.Path);
            var expectedLocalPath = Path.Combine(_config.FolderPath, $"{media.Id}{extension}");
            
            if (!File.Exists(expectedLocalPath))
            {
                // Database shows TimesShown but file is missing - reset to 0
                _repository.ResetTimesShown(media.Id);
                resetCount++;
                Logger.WriteLine($"CacheManager: Reset TimesShown for missing file: {media.Path}");
            }
        }
        
        if (resetCount > 0)
        {
            _repository.SaveChanges();
        }
        
        Logger.WriteLine($"CacheManager: Cleanup complete - Removed {removedCount} files, Reset {resetCount} database entries");    
    }

    public (long bytes, int fileCount) GetCurrentCacheSize()
    {
        var directory = new DirectoryInfo(_config.FolderPath);
        var cacheFiles = directory.EnumerateFiles("*.*", SearchOption.AllDirectories).ToList();
        var totalBytes = cacheFiles.Sum(file => file.Length);
        return (totalBytes, cacheFiles.Count);
    }

    public void EvictCacheIfNeeded(long newFileSizeBytes)
    {
        try
        {
            var (currentCacheSize, _) = GetCurrentCacheSize();
            var cacheLimitBytes = (long)(_config.CacheSizeGB * 1024.0 * 1024.0 * 1024.0);
            
            // Check if adding the new file would exceed the limit
            if (currentCacheSize + newFileSizeBytes <= cacheLimitBytes)
            {
                // No eviction needed
                return;
            }
            
            Logger.WriteLine($"CacheManager: Cache would exceed limit. Current: {currentCacheSize / (1024.0 * 1024.0 * 1024.0):F2} GB, New file: {newFileSizeBytes / (1024.0 * 1024.0):F2} MB");
            
            // Calculate how much space we need to free
            var spaceNeeded = (currentCacheSize + newFileSizeBytes) - cacheLimitBytes;
            var spaceFreed = 0L;
            var filesEvicted = 0;
            
            // Get cached media files ordered by TimesShown (highest first)
            var cachedMedia = _repository.GetCachedMediaOrderedForEviction();
            
            foreach (var media in cachedMedia)
            {
                if (spaceFreed >= spaceNeeded)
                {
                    break;
                }
                
                var localPath = GetLocalPath(media);
                
                if (File.Exists(localPath))
                {
                    var fileInfo = new FileInfo(localPath);
                    var fileSize = fileInfo.Length;
                    
                    // Delete the file
                    File.Delete(localPath);
                    
                    // Update database - set TimesShown to 0
                    _repository.ResetTimesShown(media.Id);
                    
                    spaceFreed += fileSize;
                    filesEvicted++;
                    
                    Logger.WriteLine($"CacheManager: Evicted file (TimesShown: {media.TimesShown}): {media.Path} - Freed {fileSize / (1024.0 * 1024.0):F2} MB");
                }
            }
            
            if (filesEvicted > 0)
            {
                _repository.SaveChanges();
                Logger.WriteLine($"CacheManager: Eviction complete - Removed {filesEvicted} files, Freed {spaceFreed / (1024.0 * 1024.0 * 1024.0):F2} GB");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"CacheManager: Error evicting cache: {ex.Message}", ex);
        }
    }

    public bool CopyFileToCache(MediaFile mediaFile, Func<bool> checkNetworkAvailability, Action onNetworkUnavailable)
    {
        try
        {
            // Use database ID as filename with original extension
            var extension = Path.GetExtension(mediaFile.Path);
            var newFileName = $"{mediaFile.Id}{extension}";
            var destinationPath = Path.Combine(_config.FolderPath, newFileName);
            
            // Check if file already exists in cache
            if (File.Exists(destinationPath))
            {
                Logger.WriteLine($"CacheManager: File already exists in cache: {destinationPath}");
                return true;
            }
            
            // File not in cache - need to copy from network
            // Check if source file exists on network
            if (!File.Exists(mediaFile.Path))
            {
                Logger.WriteLine($"CacheManager: Source file does not exist: {mediaFile.Path}");
                
                // Check if network drive is available
                if (!checkNetworkAvailability())
                {
                    Logger.LogWarning("CacheManager: Network drive appears to be unavailable");
                    onNetworkUnavailable();
                }
                else
                {
                    // Network is available but file doesn't exist - it was deleted
                    Logger.WriteLine($"CacheManager: File was deleted from network drive, removing from database: {mediaFile.Path}");
                    _repository.DeleteMedia(mediaFile.Id);
                }
                
                return false;
            }
            
            // Get the size of the file to be copied
            var sourceFileInfo = new FileInfo(mediaFile.Path);
            var sourceFileSize = sourceFileInfo.Length;
            
            // Check if adding this file would exceed cache limit and evict if needed
            EvictCacheIfNeeded(sourceFileSize);
                       
            // Copy the file
            File.Copy(mediaFile.Path, destinationPath, overwrite: false);
            
            Logger.WriteLine($"CacheManager: Copied file to cache: {destinationPath}");
            
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"CacheManager: Error copying file: {ex.Message}", ex);
            
            // Check if network drive is available
            if (!checkNetworkAvailability())
            {
                Logger.LogWarning("CacheManager: Network drive appears to be unavailable");
                onNetworkUnavailable();
            }
            
            return false;
        }
    }

    public string GetLocalPath(MediaFile mediaFile)
    {
        var extension = Path.GetExtension(mediaFile.Path);
        return Path.Combine(_config.FolderPath, $"{mediaFile.Id}{extension}");
    }
}
