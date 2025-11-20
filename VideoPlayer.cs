using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PhotoFrame;

public class VideoPlayer
{
    private Process? _vlcProcess;

    public async Task<bool> PlayVideoAsync(string videoPath)
    {
        try
        {
            Logger.WriteLine($"VideoPlayer: Playing video via VLC process: {System.IO.Path.GetFileName(videoPath)}");
            
            ProcessStartInfo startInfo;
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Launch VLC directly in fullscreen mode on Linux
                startInfo = new ProcessStartInfo
                {
                    FileName = "vlc",
                    Arguments = $"--fullscreen --play-and-exit --no-video-title-show --avcodec-hw=any \"{videoPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Logger.WriteLine($"VideoPlayer: Launching VLC on Linux: {startInfo.FileName} {startInfo.Arguments}");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Find VLC on Windows
                string vlcPath = FindVlcOnWindows();
                
                if (string.IsNullOrEmpty(vlcPath))
                {
                    Logger.LogError("VideoPlayer: VLC not found on Windows. Please install VLC.");
                    return false;
                }
                
                startInfo = new ProcessStartInfo
                {
                    FileName = vlcPath,
                    Arguments = $"--fullscreen --play-and-exit --no-video-title-show \"{videoPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
                Logger.WriteLine($"VideoPlayer: Launching VLC on Windows: {startInfo.FileName}");
            }
            else
            {
                Logger.LogError("VideoPlayer: Unsupported platform for VLC playback");
                return false;
            }

            _vlcProcess = Process.Start(startInfo);
            
            if (_vlcProcess == null)
            {
                Logger.LogError("VideoPlayer: Failed to start VLC process");
                return false;
            }

            // Wait for VLC to finish
            await _vlcProcess.WaitForExitAsync();
            
            int exitCode = _vlcProcess.ExitCode;
            Logger.WriteLine($"VideoPlayer: VLC process exited with code: {exitCode}");
            
            _vlcProcess.Dispose();
            _vlcProcess = null;
            
            // Exit code 0 means normal completion
            return exitCode == 0;
        }
        catch (Exception ex)
        {
            Logger.LogError($"VideoPlayer: Error playing video: {ex.Message}", ex);
            return false;
        }
    }

    private string FindVlcOnWindows()
    {
        // Common VLC installation paths on Windows
        string[] possiblePaths = new[]
        {
            @"C:\Program Files\VideoLAN\VLC\vlc.exe",
            @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"VideoLAN\VLC\vlc.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"VideoLAN\VLC\vlc.exe")
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                Logger.WriteLine($"VideoPlayer: Found VLC at: {path}");
                return path;
            }
        }

        Logger.WriteLine("VideoPlayer: VLC not found in standard locations");
        return string.Empty;
    }

    public void Stop()
    {
        // Kill VLC process if still running
        if (_vlcProcess != null && !_vlcProcess.HasExited)
        {
            Logger.WriteLine("VideoPlayer: Killing VLC process");
            _vlcProcess.Kill();
            _vlcProcess.Dispose();
            _vlcProcess = null;
        }
    }
}