using System;
using System.Threading.Tasks;

namespace PhotoFrame.Services;

public interface ITapoSmartPlugMonitor : IDisposable
{
    Task<bool> IsScreenOnAsync();
}
