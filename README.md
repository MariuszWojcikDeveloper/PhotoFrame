# PhotoFrame

A cross-platform digital photo frame application that displays photos and videos from local cache and network locations. Built with Avalonia UI and .NET 9, featuring smart screen detection via TP-Link Tapo smart plugs to automatically pause when the display is off.

## Features

- 📸 **Photo & Video Slideshow** - Supports multiple image and video formats with automatic rotation based on EXIF data
- 🌐 **Network & Cache Management** - Intelligently caches media from network drives and falls back to cache when network is unavailable
- 🔌 **Smart Screen Detection** - Integrates with TP-Link Tapo smart plugs to detect when TV/monitor is on/off and pause slideshow accordingly
- 📊 **SQLite Database** - Tracks media files and viewing statistics
- ⚡ **Cross-platform** - Runs on Windows, Linux (including Raspberry Pi), and macOS
- 🎬 **VLC Integration** - Uses LibVLC for high-quality video playback

## Prerequisites

- **.NET 9.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **VLC Media Player** - Required for video playback
  - Windows: [Download VLC](https://www.videolan.org/vlc/)
  - Linux: `sudo apt install vlc` or `sudo pacman -S vlc`
  - macOS: `brew install vlc`

## Installation

1. Clone the repository:
```bash
git clone https://github.com/yourusername/PhotoFrame.git
cd PhotoFrame
```

2. Copy the example configuration file:
```bash
# Windows
copy photoframe-config.example.json photoframe-config.json

# Linux/macOS
cp photoframe-config.example.json photoframe-config.json
```

3. Edit `photoframe-config.json` with your settings (see Configuration section below)

4. Build the application:
```bash
dotnet build
```

5. Run:
```bash
dotnet run
```

## Configuration

The application uses JSON configuration files and supports environment variables for sensitive credentials.

### Configuration File

Edit `photoframe-config.json` with your settings:

```json
{
  "PhotoFrame": {
    "FolderPath": "C:\\PhotoFrame\\Cache",
    "NetworkLocationPhoto": "\\\\your-nas\\photos\\family",
    "NetworkLocationVideos": "\\\\your-nas\\videos\\family",
    "NetworkDriveControlFile": "\\\\your-nas\\photos\\.connection_test",
    "CacheSizeGB": 1,
    "NetworkRefreshPercentage": 10,
    "SlideshowInterval": 20,
    "EnableLogging": true,
    "LogFilePath": "photoframe.log"
  }
}
```

### Configuration Options

| Option | Description | Example |
|--------|-------------|---------|
| `FolderPath` | Local directory for cached media files | `C:\PhotoFrame\Cache` or `/home/pi/photoframe/cache` |
| `NetworkLocationPhoto` | Network path to photos | `\\nas\photos` or `/mnt/nas/photos` |
| `NetworkLocationVideos` | Network path to videos | `\\nas\videos` or `/mnt/nas/videos` |
| `NetworkDriveControlFile` | File to test network connectivity | `\\nas\.connection_test` |
| `CacheSizeGB` | Maximum cache size in GB | `1` |
| `NetworkRefreshPercentage` | % of time to refresh from network vs cache | `10` (10% network, 90% cache) |
| `SlideshowInterval` | Seconds between images | `20` |
| `EnableLogging` | Enable file logging | `true` or `false` |
| `LogFilePath` | Log file path | `photoframe.log` |

### Tapo Smart Plug Configuration (Optional)

For automatic screen detection, configure TP-Link Tapo smart plug credentials via **environment variables** (recommended) or config file.

#### Using Environment Variables (Recommended)

**Windows (PowerShell):**
```powershell
$env:PHOTOFRAME_PhotoFrame__TapoEmail = "your-email@example.com"
$env:PHOTOFRAME_PhotoFrame__TapoPassword = "your-password"
$env:PHOTOFRAME_PhotoFrame__TapoDeviceIp = "192.168.1.100"
```

**Linux/macOS (bash):**
```bash
export PHOTOFRAME_PhotoFrame__TapoEmail="your-email@example.com"
export PHOTOFRAME_PhotoFrame__TapoPassword="your-password"
export PHOTOFRAME_PhotoFrame__TapoDeviceIp="192.168.1.100"
```

To make these permanent:
- **Windows**: Add to System Environment Variables via System Properties
- **Linux**: Add export commands to `~/.bashrc` or `~/.profile`
- **Raspberry Pi (systemd service)**: Add `Environment=` lines to service file

#### Using Config File (Not Recommended)

Alternatively, add to `photoframe-config.json` (note: this stores credentials in plain text):
```json
{
  "PhotoFrame": {
    ...
    "TapoEmail": "your-email@example.com",
    "TapoPassword": "your-password",
    "TapoDeviceIp": "192.168.1.100"
  }
}
```

## Supported Media Formats

### Images
- JPG/JPEG
- PNG
- BMP
- GIF
- TIFF
- WebP

### Videos
- MP4
- AVI
- MOV
- WMV
- MKV
- WebM
- M4V

## Keyboard Controls

| Key | Action |
|-----|--------|
| **Right Arrow** | Next media |
| **Space** | Toggle slideshow (pause/resume) |
| **F11** | Toggle fullscreen |
| **Escape** | Exit application |

## How It Works

### Cache Management
1. On startup, the app scans the local cache directory
2. Based on `NetworkRefreshPercentage`, it decides whether to load from network or cache
3. Network media is copied to cache and tracked in SQLite database
4. Cache is automatically managed to stay within `CacheSizeGB` limit, removing most-shown items first (ensuring variety)

### Smart Screen Detection
- If Tapo credentials are configured, the app monitors the smart plug state
- When the plug (and TV/monitor) is OFF, the slideshow automatically pauses
- When the plug turns ON, the slideshow resumes automatically
- This saves bandwidth and reduces wear on network drives when not in use

### Network Failover
- If network locations become unavailable, the app automatically switches to cache-only mode
- After network failure, it waits 1 hour before attempting to access the network again
- During cache-only mode, a warning displays on screen

## Building from Source

### Development Build
```bash
dotnet build
dotnet run
```

### Release Build
```bash
dotnet build -c Release
```

### Publishing for Deployment

**Windows:**
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

**Linux x64:**
```bash
dotnet publish -c Release -r linux-x64 --self-contained
```

**Raspberry Pi (ARM64):**
```bash
dotnet publish -c Release -r linux-arm64 --self-contained
```

The published files will be in `bin/Release/net9.0/{runtime}/publish/`

## Deployment on Raspberry Pi

1. Publish for ARM64:
```bash
dotnet publish -c Release -r linux-arm64 --self-contained
```

2. Copy files to Raspberry Pi:
```bash
scp -r bin/Release/net9.0/linux-arm64/publish/* pi@raspberrypi:/home/pi/photoframe/
```

3. Copy and configure the config file:
```bash
scp photoframe-config-pi.example.json pi@raspberrypi:/home/pi/photoframe/photoframe-config.json
# Edit the file on Pi with your paths
```

4. Install VLC on Pi:
```bash
sudo apt update
sudo apt install vlc
```

5. Set environment variables for Tapo (edit `~/.bashrc`):
```bash
export PHOTOFRAME_PhotoFrame__TapoEmail="your-email@example.com"
export PHOTOFRAME_PhotoFrame__TapoPassword="your-password"
export PHOTOFRAME_PhotoFrame__TapoDeviceIp="192.168.1.100"
```

6. Make executable and run:
```bash
chmod +x /home/pi/photoframe/PhotoFrame
/home/pi/photoframe/PhotoFrame
```

### Create Systemd Service (Optional)

Create `/etc/systemd/system/photoframe.service`:
```ini
[Unit]
Description=PhotoFrame Digital Photo Frame
After=network.target

[Service]
Type=simple
User=pi
WorkingDirectory=/home/pi/photoframe
ExecStart=/home/pi/photoframe/PhotoFrame
Restart=always
Environment="PHOTOFRAME_PhotoFrame__TapoEmail=your-email@example.com"
Environment="PHOTOFRAME_PhotoFrame__TapoPassword=your-password"
Environment="PHOTOFRAME_PhotoFrame__TapoDeviceIp=192.168.1.100"
Environment="DISPLAY=:0"

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl daemon-reload
sudo systemctl enable photoframe
sudo systemctl start photoframe
```

## Dependencies

- **Avalonia UI** (11.x) - Cross-platform UI framework
- **LibVLCSharp** (3.x) - VLC media player bindings
- **Entity Framework Core** (9.x) - SQLite database ORM
- **Smdn.TPSmartHomeDevices.Tapo** (2.x) - TP-Link Tapo smart plug integration
- **MetadataExtractor** - EXIF data reading for photo rotation

## Troubleshooting

### Videos won't play
- Ensure VLC is installed and in system PATH
- Check logs for VLC-related errors

### Network locations not accessible
- Verify network paths are correct for your OS (Windows: `\\server\share`, Linux: `/mnt/nas`)
- Test connectivity to `NetworkDriveControlFile` location
- Check SMB/NFS mount on Linux

### Tapo smart plug not working
- Verify credentials are correct
- Ensure device IP is reachable from the machine
- Check that the Tapo device is a P105 or compatible model
- Review logs for Tapo-related errors

### High CPU usage
- Reduce `NetworkRefreshPercentage` to rely more on cache
- Increase `SlideshowInterval` for longer display times
- Check if VLC processes are lingering (kill them manually if needed)

## License

This project is open source and available under the MIT License.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.