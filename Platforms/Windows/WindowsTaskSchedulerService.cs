using Microsoft.Win32.TaskScheduler;
using System.Security.Principal;
using AnonWallClient.Services;
using TaskSchedulerTask = Microsoft.Win32.TaskScheduler.Task;

namespace AnonWallClient.Platforms.Windows;

public class WindowsTaskSchedulerService
{
    private readonly AppLogService _logger;
    private readonly SettingsService _settingsService;
    private const string TASK_NAME = "AnonWallClient_AutoStart";
    private const string TASK_FOLDER = "\\AnonWallClient\\";

    public WindowsTaskSchedulerService(AppLogService logger, SettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    public async System.Threading.Tasks.Task<bool> EnableAutoStartAsync(int intervalHours = 1)
    {
        try
        {
            _logger.Add("Windows Task Scheduler: Attempting to create autostart task...");

            // Check if running as administrator
            if (!IsRunningAsAdministrator())
            {
                _logger.Add("Windows Task Scheduler: Administrator privileges required to create scheduled tasks.");
                return await PromptForAdminAndRetryAsync(intervalHours);
            }

            using var taskService = new TaskService();
            
            // Remove existing task if it exists
            await RemoveExistingTaskAsync(taskService);

            // Create a new task definition
            var taskDefinition = taskService.NewTask();
            taskDefinition.RegistrationInfo.Description = "Automatically starts AnonWallClient with wallpaper polling service";
            taskDefinition.RegistrationInfo.Author = "AnonWallClient";
            taskDefinition.Principal.RunLevel = TaskRunLevel.Highest; // Run with elevated privileges
            taskDefinition.Principal.LogonType = TaskLogonType.InteractiveToken;

            // Set task settings
            taskDefinition.Settings.AllowDemandStart = true;
            taskDefinition.Settings.DisallowStartIfOnBatteries = false;
            taskDefinition.Settings.StopIfGoingOnBatteries = false;
            taskDefinition.Settings.AllowHardTerminate = true;
            taskDefinition.Settings.StartWhenAvailable = true;
            taskDefinition.Settings.RunOnlyIfNetworkAvailable = true;
            taskDefinition.Settings.ExecutionTimeLimit = TimeSpan.Zero; // No time limit
            taskDefinition.Settings.RestartCount = 3;
            taskDefinition.Settings.RestartInterval = TimeSpan.FromMinutes(5);

            // Add startup trigger (at logon)
            var logonTrigger = new LogonTrigger();
            logonTrigger.Enabled = true;
            logonTrigger.Delay = TimeSpan.FromSeconds(30); // Wait 30 seconds after logon
            taskDefinition.Triggers.Add(logonTrigger);

            // Add interval trigger (run every X hours)
            if (intervalHours > 0)
            {
                var dailyTrigger = new DailyTrigger();
                dailyTrigger.Enabled = true;
                dailyTrigger.StartBoundary = DateTime.Today.AddHours(DateTime.Now.Hour + 1); // Start in next hour
                dailyTrigger.Repetition.Interval = TimeSpan.FromHours(intervalHours);
                dailyTrigger.Repetition.Duration = TimeSpan.FromDays(365); // Repeat for a year
                taskDefinition.Triggers.Add(dailyTrigger);
            }

            // Add the action (what to execute)
            var executablePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (executablePath.EndsWith(".dll"))
            {
                // If we got a DLL path, we need to run it with dotnet
                var action = new ExecAction("dotnet", executablePath);
                action.WorkingDirectory = Path.GetDirectoryName(executablePath);
                taskDefinition.Actions.Add(action);
            }
            else
            {
                // Direct executable
                var action = new ExecAction(executablePath);
                action.WorkingDirectory = Path.GetDirectoryName(executablePath);
                taskDefinition.Actions.Add(action);
            }

            // Register the task
            var registeredTask = taskService.RootFolder.RegisterTaskDefinition(
                TASK_NAME,
                taskDefinition,
                TaskCreation.CreateOrUpdate,
                null, // Use current user
                null, // No password needed for interactive token
                TaskLogonType.InteractiveToken);

            _logger.Add($"Windows Task Scheduler: Autostart task created successfully. Interval: {intervalHours} hours");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.Add("Windows Task Scheduler: Access denied - administrator privileges required.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows Task Scheduler: Error creating autostart task: {ex.Message}");
            return false;
        }
    }

    public async System.Threading.Tasks.Task<bool> DisableAutoStartAsync()
    {
        try
        {
            _logger.Add("Windows Task Scheduler: Removing autostart task...");

            if (!IsRunningAsAdministrator())
            {
                _logger.Add("Windows Task Scheduler: Administrator privileges required to remove scheduled tasks.");
                return await PromptForAdminAndDisableAsync();
            }

            using var taskService = new TaskService();
            await RemoveExistingTaskAsync(taskService);

            _logger.Add("Windows Task Scheduler: Autostart task removed successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows Task Scheduler: Error removing autostart task: {ex.Message}");
            return false;
        }
    }

    public async System.Threading.Tasks.Task<bool> IsAutoStartEnabledAsync()
    {
        try
        {
            using var taskService = new TaskService();
            var scheduledTask = taskService.GetTask(TASK_NAME);
            return scheduledTask != null && scheduledTask.Enabled;
        }
        catch
        {
            return false;
        }
    }

    public async System.Threading.Tasks.Task<string> GetAutoStartStatusAsync()
    {
        try
        {
            using var taskService = new TaskService();
            var scheduledTask = taskService.GetTask(TASK_NAME);
            
            if (scheduledTask == null)
            {
                return "Not configured";
            }

            if (!scheduledTask.Enabled)
            {
                return "Disabled";
            }

            var intervalTrigger = scheduledTask.Definition.Triggers.OfType<DailyTrigger>().FirstOrDefault();
            if (intervalTrigger?.Repetition.Interval != null)
            {
                var hours = intervalTrigger.Repetition.Interval.TotalHours;
                return $"Enabled (every {hours} hours)";
            }

            return "Enabled (login only)";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private async System.Threading.Tasks.Task RemoveExistingTaskAsync(TaskService taskService)
    {
        try
        {
            var existingTask = taskService.GetTask(TASK_NAME);
            if (existingTask != null)
            {
                taskService.RootFolder.DeleteTask(TASK_NAME, false);
                _logger.Add("Windows Task Scheduler: Removed existing autostart task.");
            }
        }
        catch (FileNotFoundException)
        {
            // Task doesn't exist, which is fine
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows Task Scheduler: Warning - could not remove existing task: {ex.Message}");
        }
    }

    private bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private async System.Threading.Tasks.Task<bool> PromptForAdminAndRetryAsync(int intervalHours)
    {
        try
        {
            // Try to restart the current process with admin privileges
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location,
                Arguments = $"--enable-autostart --interval-hours {intervalHours}",
                Verb = "runas", // Request admin privileges
                UseShellExecute = true
            };

            var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows Task Scheduler: Error requesting admin privileges: {ex.Message}");
            return false;
        }
    }

    private async System.Threading.Tasks.Task<bool> PromptForAdminAndDisableAsync()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location,
                Arguments = "--disable-autostart",
                Verb = "runas",
                UseShellExecute = true
            };

            var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows Task Scheduler: Error requesting admin privileges for disable: {ex.Message}");
            return false;
        }
    }

    public async System.Threading.Tasks.Task<bool> UpdateAutoStartIntervalAsync(int intervalHours)
    {
        if (await IsAutoStartEnabledAsync())
        {
            return await EnableAutoStartAsync(intervalHours);
        }
        return false;
    }
}
