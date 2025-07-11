using System.Collections.ObjectModel;

namespace AnonWallClient.Services;

public class AppLogService
{
    // A collection that notifies the UI when it's updated
    public ObservableCollection<string> Logs { get; } = new();

    public void Add(string message)
    {
        // Add a timestamp to the message
        string entry = $"{DateTime.Now:HH:mm:ss} - {message}";

        // All UI updates must happen on the main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Add the new log to the top of the list
            Logs.Insert(0, entry);

            // Optional: Keep the log from growing too large
            if (Logs.Count > 100)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }
        });
    }
}