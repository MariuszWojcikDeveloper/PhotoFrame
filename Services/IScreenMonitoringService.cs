using System;
using System.Threading.Tasks;

namespace PhotoFrame.Services;

public interface IScreenMonitoringService
{
    event EventHandler<bool>? ScreenStateChanged;
    void Initialize(double checkIntervalSeconds);
    Task<bool> CheckScreenStateAsync();
    void StartPeriodicCheck();
    void StopPeriodicCheck();
}
