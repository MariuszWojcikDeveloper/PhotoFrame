using System;
using System.Collections.Generic;
using System.Linq;

namespace PhotoFrame.DataAccess;

public class DatabaseMediaRepository
{
    private readonly MediaDbContext _dbContext;
    private readonly Random _random = new Random();

    public DatabaseMediaRepository(MediaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void EnsureDatabaseCreated()
    {
        _dbContext.Database.EnsureCreated();
        Logger.WriteLine("DatabaseMediaRepository: Database initialized");
    }

    public MediaFile? GetRandomCachedMedia()
    {
        try
        {
            var cachedMedia = _dbContext.Media.Where(m => m.TimesShown > 0).ToList();
            
            if (cachedMedia.Count == 0)
            {
                Logger.WriteLine("DatabaseMediaRepository: No cached media files in database");
                return null;
            }
            
            var randomIndex = _random.Next(0, cachedMedia.Count);
            var mediaFile = cachedMedia[randomIndex];
            
            Logger.WriteLine($"DatabaseMediaRepository: Selected random cached media: {mediaFile.Path}");
            
            return mediaFile;
        }
        catch (Exception ex)
        {
            Logger.LogError($"DatabaseMediaRepository: Error getting random cached media: {ex.Message}", ex);
            return null;
        }
    }

    public MediaFile? GetRandomUncachedMedia()
    {
        try
        {
            var uncachedMedia = _dbContext.Media.Where(m => m.TimesShown == 0).ToList();
            
            if (uncachedMedia.Count == 0)
            {
                Logger.WriteLine("DatabaseMediaRepository: No uncached media files in database");
                return null;
            }
            
            var randomIndex = _random.Next(0, uncachedMedia.Count);
            var mediaFile = uncachedMedia[randomIndex];
            
            Logger.WriteLine($"DatabaseMediaRepository: Selected random uncached media: {mediaFile.Path}");
            
            return mediaFile;
        }
        catch (Exception ex)
        {
            Logger.LogError($"DatabaseMediaRepository: Error getting random uncached media: {ex.Message}", ex);
            return null;
        }
    }

    public void IncrementTimesShown(int mediaId)
    {
        var media = _dbContext.Media.Find(mediaId);
            
        if (media != null)
        {
            media.TimesShown++;
            _dbContext.SaveChanges();
            
            Logger.WriteLine($"DatabaseMediaRepository: Updated TimesShown to {media.TimesShown} for: {media.Path}");
        }
    }

    public void SaveFiles(List<string> filePaths)
    {
        try
        {
            Logger.WriteLine($"DatabaseMediaRepository: Saving {filePaths.Count} files to database");
            
            var existingPaths = _dbContext.Media.Select(m => m.Path).ToHashSet();
            var newFiles = filePaths.Where(path => !existingPaths.Contains(path)).ToList();
            
            if (newFiles.Count > 0)
            {
                var mediaEntities = newFiles.Select(path => new MediaFile { Path = path }).ToList();
                _dbContext.Media.AddRange(mediaEntities);
                _dbContext.SaveChanges();
                
                Logger.WriteLine($"DatabaseMediaRepository: Added {newFiles.Count} new files to database");
            }
            else
            {
                Logger.WriteLine("DatabaseMediaRepository: No new files to add to database");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"DatabaseMediaRepository: Error saving files to database: {ex.Message}", ex);
        }
    }

    public MediaFile? FindById(int mediaId)
    {
        return _dbContext.Media.Find(mediaId);
    }

    public List<MediaFile> GetAllCachedMedia()
    {
        return _dbContext.Media.Where(m => m.TimesShown > 0).ToList();
    }

    public List<MediaFile> GetCachedMediaOrderedForEviction()
    {
        return _dbContext.Media
            .Where(m => m.TimesShown > 0)
            .OrderByDescending(m => m.TimesShown)
            .ThenBy(m => m.Id)
            .ToList();
    }

    public void ResetTimesShown(int mediaId)
    {
        var media = _dbContext.Media.Find(mediaId);
        if (media != null)
        {
            media.TimesShown = 0;
        }
    }

    public void SaveChanges()
    {
        _dbContext.SaveChanges();
    }

    public void DeleteMedia(int mediaId)
    {
        var media = _dbContext.Media.Find(mediaId);
        if (media != null)
        {
            _dbContext.Media.Remove(media);
            _dbContext.SaveChanges();
            Logger.WriteLine($"DatabaseMediaRepository: Deleted media from database: {media.Path}");
        }
    }
}
