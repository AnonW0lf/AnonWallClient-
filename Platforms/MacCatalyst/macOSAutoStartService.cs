using AnonWallClient.Services;

namespace AnonWallClient.Platforms.MacCatalyst;

public class macOSAutoStartService : IAutoStartService
{
    private readonly AppLogService _logger;
    
    public bool IsSupported => true;
    public string PlatformName => "macOS (Launch Agents)";

    public macOSAutoStartService(AppLogService logger)
    {
        _logger = logger;
    }

    public async Task<bool> EnableAutoStartAsync(int intervalHours = 1)
    {
        try
        {
            _logger.Add($"macOS AutoStart: Creating Launch Agent with {intervalHours} hour interval...");
            
            var launchAgentPath = GetLaunchAgentPath();
            var plistContent = CreateLaunchAgentPlist(intervalHours);
            
            // Ensure the LaunchAgents directory exists
            var launchAgentsDir = Path.GetDirectoryName(launchAgentPath);
            if (!Directory.Exists(launchAgentsDir))
            {
                Directory.CreateDirectory(launchAgentsDir!);
            }
            
            await File.WriteAllTextAsync(launchAgentPath, plistContent);
            
            // Load the launch agent
            var loadResult = await ExecuteCommandAsync("launchctl", $"load {launchAgentPath}");
            if (loadResult.Success)
            {
                _logger.Add("macOS AutoStart: Launch Agent created and loaded successfully.");
                return true;
            }
            else
            {
                _logger.Add($"macOS AutoStart: Failed to load Launch Agent: {loadResult.Error}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"macOS AutoStart: Error enabling: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DisableAutoStartAsync()
    {
        try
        {
            _logger.Add("macOS AutoStart: Removing Launch Agent...");
            
            var launchAgentPath = GetLaunchAgentPath();
            
            // Unload the launch agent if it exists
            if (File.Exists(launchAgentPath))
            {
                await ExecuteCommandAsync("launchctl", $"unload {launchAgentPath}");
                File.Delete(launchAgentPath);
            }
            
            _logger.Add("macOS AutoStart: Launch Agent removed successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Add($"macOS AutoStart: Error disabling: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsAutoStartEnabledAsync()
    {
        var launchAgentPath = GetLaunchAgentPath();
        return File.Exists(launchAgentPath);
    }

    public async Task<string> GetAutoStartStatusAsync()
    {
        try
        {
            var launchAgentPath = GetLaunchAgentPath();
            if (!File.Exists(launchAgentPath))
            {
                return "Not configured";
            }

            // Try to read the interval from the plist file
            var content = await File.ReadAllTextAsync(launchAgentPath);
            if (content.Contains("<key>StartInterval</key>"))
            {
                var lines = content.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("<key>StartInterval</key>") && i + 1 < lines.Length)
                    {
                        var intervalLine = lines[i + 1];
                        var startPos = intervalLine.IndexOf(">") + 1;
                        var endPos = intervalLine.IndexOf("<", startPos);
                        if (startPos > 0 && endPos > startPos)
                        {
                            var intervalSeconds = intervalLine.Substring(startPos, endPos - startPos);
                            if (int.TryParse(intervalSeconds, out int seconds))
                            {
                                var hours = seconds / 3600;
                                return $"Enabled (every {hours} hours)";
                            }
                        }
                    }
                }
            }

            return "Enabled";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<bool> UpdateAutoStartIntervalAsync(int intervalHours)
    {
        if (await IsAutoStartEnabledAsync())
        {
            return await EnableAutoStartAsync(intervalHours);
        }
        return false;
    }

    private string GetLaunchAgentPath()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDir, "Library", "LaunchAgents", "com.anonwallclient.autostart.plist");
    }

    private string CreateLaunchAgentPlist(int intervalHours)
    {
        var executablePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        var intervalSeconds = intervalHours * 3600;

        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>com.anonwallclient.autostart</string>
    <key>ProgramArguments</key>
    <array>
        <string>{executablePath}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>StartInterval</key>
    <integer>{intervalSeconds}</integer>
    <key>StandardOutPath</key>
    <string>/tmp/anonwallclient.out</string>
    <key>StandardErrorPath</key>
    <string>/tmp/anonwallclient.err</string>
</dict>
</plist>";
    }

    private async Task<(bool Success, string Error)> ExecuteCommandAsync(string command, string arguments)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                var error = await process.StandardError.ReadToEndAsync();
                return (process.ExitCode == 0, error);
            }

            return (false, "Failed to start process");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
