using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Smdn.TPSmartHomeDevices.Tapo;

namespace PhotoFrame.Services;

public class TapoSmartPlugMonitor : ITapoSmartPlugMonitor
{
    private readonly string? _email;
    private readonly string? _password;
    private readonly string? _deviceIp;
    private readonly bool _isConfigured;
    private P105? _plug;

    public TapoSmartPlugMonitor(string? email, string? password, string? deviceIp)
    {
        _email = email;
        _password = password;
        _deviceIp = deviceIp;
        _isConfigured = !string.IsNullOrWhiteSpace(deviceIp);

        if (_isConfigured)
        {
            Logger.WriteLine($"TapoSmartPlugMonitor: Initialized for device {deviceIp}");
        }
        else
        {
            Logger.WriteLine("TapoSmartPlugMonitor: Not configured (no device IP provided)");
        }
    }

    public async Task<bool> IsScreenOnAsync()
    {
        try
        {
            // If not configured, assume screen is on
            if (!_isConfigured)
            {
                Logger.WriteLine("TapoSmartPlugMonitor: Not configured, assuming screen is on");
                return true;
            }

            if (string.IsNullOrWhiteSpace(_email) || 
                string.IsNullOrWhiteSpace(_password) || 
                string.IsNullOrWhiteSpace(_deviceIp))
            {
                Logger.WriteLine("TapoSmartPlugMonitor: Credentials not configured, assuming screen is on");
                return true;
            }

            try
            {
                // Create plug instance if not already created
                // The library handles session management automatically
                if (_plug == null)
                {
                    if (!IPAddress.TryParse(_deviceIp, out var ipAddress))
                    {
                        Logger.LogError($"TapoSmartPlugMonitor: Invalid IP address: {_deviceIp}", null);
                        return true;
                    }
                    
                    Logger.WriteLine($"TapoSmartPlugMonitor: Creating P105 instance for device at {_deviceIp}");
                    _plug = new P105(ipAddress, _email, _password);
                    Logger.WriteLine($"TapoSmartPlugMonitor: P105 instance created");
                }

                // Get on/off state - no cancellation token since library doesn't respect it
                Logger.WriteLine("TapoSmartPlugMonitor: Starting Tapo plug state check...");
                var isOn = await _plug.GetOnOffStateAsync();
                Logger.WriteLine($"TapoSmartPlugMonitor: Tapo plug state: {(isOn ? "ON" : "OFF")}");
                
                return isOn;
            }
            catch (Exception ex)
            {
                Logger.LogError($"TapoSmartPlugMonitor: Error checking plug state: {ex.Message}, assuming screen is on", ex);
                
                // Dispose and recreate plug instance on error for clean retry
                _plug?.Dispose();
                _plug = null;
                
                // If Tapo check failed, assume screen is on to avoid stopping slideshow unnecessarily
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"TapoSmartPlugMonitor: Unexpected error: {ex.Message}, assuming screen is on", ex);
            // If we can't determine, assume screen is on to avoid stopping slideshow unnecessarily
            return true;
        }
    }
    
    public void Dispose()
    {
        _plug?.Dispose();
    }
}
