using System.Linq;

namespace PhotoFrame;

public static class MediaExtensions
{
    public static readonly string[] PhotoExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" };
    public static readonly string[] VideoExtensions = { ".mp4", ".avi", ".mov", ".wmv", ".mkv", ".webm", ".m4v" };
    public static readonly string[] AllExtensions = PhotoExtensions.Concat(VideoExtensions).ToArray();
}
