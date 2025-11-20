using System;

namespace PhotoFrame;

public class SlideshowContext
{
    public CurrentMedia? CurrentMedia { get; set; }
    public int CacheFileCount { get; set; }
    public double CacheSizeGB { get; set; }
    public bool IsNetworkUnavailable { get; set; }
    public DateTime? NetworkUnavailableUntil { get; set; }
}
