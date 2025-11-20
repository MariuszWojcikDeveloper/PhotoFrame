using System;
using System.Threading.Tasks;

namespace PhotoFrame.Services;

/// <summary>
/// Coordinates media navigation, slideshow control, and screen state monitoring.
/// Handles all business logic for advancing media and responding to state changes.
/// </summary>
public interface ISlideshowCoordinator
{
    event EventHandler<CurrentMedia>? MediaChanged;
    bool IsSlideshowEnabled { get; }
    double SlideshowInterval { get; }
    Task StartAsync();
    Task ShowNextMediaAsync();
    void ToggleSlideshow();
    void OnVideoCompleted();
    void OnVideoCancelled();
    SlideshowContext? GetCurrentMediaInfo();
}
