namespace PhotoFrame;

public class CurrentMedia
{
    public string NetworkPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string CacheFileName { get; set; } = string.Empty;
    public string CacheFilePath { get; set; } = string.Empty;
    public long FileSizeKB { get; set; }
    public int TimesShown { get; set; }
    public bool IsVideo { get; set; }
}
