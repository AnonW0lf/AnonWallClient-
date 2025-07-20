namespace AnonWallClient.Services;

public interface IAutoStartService
{
    Task<bool> EnableAutoStartAsync(int intervalHours = 1);
    Task<bool> DisableAutoStartAsync();
    Task<bool> IsAutoStartEnabledAsync();
    Task<string> GetAutoStartStatusAsync();
    Task<bool> UpdateAutoStartIntervalAsync(int intervalHours);
    bool IsSupported { get; }
    string PlatformName { get; }
}
