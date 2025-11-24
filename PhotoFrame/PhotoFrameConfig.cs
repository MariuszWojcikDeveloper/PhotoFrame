using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace PhotoFrame;

public class PhotoFrameConfig
{
    public const string SectionName = "PhotoFrame";
    
    [Required(ErrorMessage = "FolderPath is required")]
    [DirectoryExists(ErrorMessage = "Directory does not exist")]
    public string FolderPath { get; set; } = string.Empty;
    
    public string? NetworkLocationPhoto { get; set; }
    
    public string? NetworkLocationVideos { get; set; }
    
    public string? NetworkDriveControlFile { get; set; }
    
    [Range(1, 1000, ErrorMessage = "CacheSizeGB must be between 1 and 1000 GB")]
    public int CacheSizeGB { get; set; } = 10;
    
    [Range(0, 100, ErrorMessage = "NetworkRefreshPercentage must be between 0 and 100")]
    public int NetworkRefreshPercentage { get; set; } = 10;
    
    [Range(1, 3600, ErrorMessage = "SlideshowInterval must be between 1 and 3600 seconds")]
    public int SlideshowInterval { get; set; } = 60;
    
    public bool EnableLogging { get; set; } = true;
    
    [Required(ErrorMessage = "LogFilePath is required")]
    public string LogFilePath { get; set; } = "photoframe.log";
    
    // Custom validation attributes
    public class DirectoryExistsAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value is string path && !string.IsNullOrWhiteSpace(path))
            {
                return Directory.Exists(path);
            }
            return false;
        }
    }
}

// Options validation class
public class PhotoFrameConfigValidation : IValidateOptions<PhotoFrameConfig>
{
    public ValidateOptionsResult Validate(string? name, PhotoFrameConfig options)
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        
        if (!Validator.TryValidateObject(options, context, results, true))
        {
            var errors = results.Select(r => r.ErrorMessage ?? "Unknown validation error");
            return ValidateOptionsResult.Fail(errors);
        }
        
        return ValidateOptionsResult.Success;
    }
}