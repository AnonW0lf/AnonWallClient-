using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace AnonWallClient.Services;

public class WallpaperHistoryItem
{
    public string ImageUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? Description { get; set; }
    public DateTime SetTime { get; set; }
    // Walltaker API fields
    public string? PostUrl { get; set; }
    public string? SourceUrl { get; set; }
    public string? Uploader { get; set; }
    public int? Score { get; set; }
    public bool? Nsfw { get; set; }
    public bool? Blacklisted { get; set; }
    public bool? Favorite { get; set; }
    public string? Notes { get; set; }
}

public class WallpaperHistoryService
{
    private readonly string _historyPath;
    private readonly SettingsService _settingsService;
    private List<WallpaperHistoryItem> _history = new();

    public WallpaperHistoryService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _historyPath = Path.Combine(documents, "AnonWallClient.wallpaper_history.json");
        Load();
    }

    public IReadOnlyList<WallpaperHistoryItem> History => _history;
    
    // Event to notify when a new wallpaper is added
    public event EventHandler<WallpaperHistoryItem>? WallpaperAdded;
    
    // Event to notify when history is cleared
    public event EventHandler? HistoryCleared;

    public void AddWallpaper(WallpaperHistoryItem item)
    {
        var maxHistory = _settingsService.GetMaxHistoryLimit();
        
        // If max history is 0, don't save any history
        if (maxHistory == 0)
        {
            // Still notify subscribers for UI updates, but don't save
            WallpaperAdded?.Invoke(this, item);
            return;
        }
        
        // Check for duplicates based on ImageUrl to prevent duplicate entries
        var existingItem = _history.FirstOrDefault(h => h.ImageUrl == item.ImageUrl);
        if (existingItem != null)
        {
            // If the item already exists, update it instead of adding a duplicate
            // This handles cases where response data might have changed
            var index = _history.IndexOf(existingItem);
            _history[index] = item;
            Save();
            
            // Notify subscribers that the wallpaper was updated (not newly added)
            WallpaperAdded?.Invoke(this, item);
            return;
        }

        // Add new item if no duplicate found
        _history.Insert(0, item);
        if (_history.Count > maxHistory)
            _history.RemoveAt(_history.Count - 1);
        Save();
        
        // Notify subscribers that a new wallpaper was added
        WallpaperAdded?.Invoke(this, item);
    }

    public void ClearHistory()
    {
        _history.Clear();
        Save();
        
        // Notify subscribers that history was cleared
        HistoryCleared?.Invoke(this, EventArgs.Empty);
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_historyPath, json);
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_historyPath))
            {
                var json = File.ReadAllText(_historyPath);
                _history = JsonSerializer.Deserialize<List<WallpaperHistoryItem>>(json) ?? new List<WallpaperHistoryItem>();
                
                // Remove duplicates that might already exist
                RemoveDuplicates();
                
                // Trim history to current max limit if needed
                TrimHistoryToLimit();
            }
        }
        catch (Exception ex)
        {
            // If the file is corrupt or incompatible, reset history and backup the bad file
            var backupPath = _historyPath + ".bak";
            try { File.Copy(_historyPath, backupPath, true); } catch { }
            _history = new List<WallpaperHistoryItem>();
            Save();
        }
    }

    private void RemoveDuplicates()
    {
        // Remove duplicates based on ImageUrl, keeping the first occurrence (most recent)
        var seen = new HashSet<string>();
        var deduplicated = new List<WallpaperHistoryItem>();
        
        foreach (var item in _history)
        {
            if (!string.IsNullOrEmpty(item.ImageUrl) && seen.Add(item.ImageUrl))
            {
                deduplicated.Add(item);
            }
        }
        
        // Only save if we actually removed duplicates
        if (deduplicated.Count != _history.Count)
        {
            _history = deduplicated;
            Save();
        }
    }

    private void TrimHistoryToLimit()
    {
        var maxHistory = _settingsService.GetMaxHistoryLimit();
        
        // If max history is 0, clear all history
        if (maxHistory == 0)
        {
            if (_history.Count > 0)
            {
                _history.Clear();
                Save();
            }
            return;
        }
        
        // Trim to max limit if we exceed it
        if (_history.Count > maxHistory)
        {
            _history = _history.Take(maxHistory).ToList();
            Save();
        }
    }
}