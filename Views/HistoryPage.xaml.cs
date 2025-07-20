using AnonWallClient.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace AnonWallClient.Views;

public partial class HistoryPage : ContentPage
{
    private readonly WallpaperHistoryService _historyService;
    private readonly SettingsService _settingsService;
    public ObservableCollection<WallpaperHistoryItem> Wallpapers { get; } = new();

    public HistoryPage(WallpaperHistoryService historyService, SettingsService settingsService)
    {
        InitializeComponent();
        _historyService = historyService;
        _settingsService = settingsService;
        
        // Subscribe to wallpaper added events to auto-refresh
        _historyService.WallpaperAdded += OnWallpaperAdded;
        _historyService.HistoryCleared += OnHistoryCleared;
        
        // Platform-specific optimizations for CollectionView
        ConfigureCollectionViewForPlatform();
        
        LoadWallpapers();
        BindingContext = this;
    }

    private void ConfigureCollectionViewForPlatform()
    {
        // Configure CollectionView for better performance on different platforms
#if ANDROID
        // On Android, enable item recycling for better memory management
        if (HistoryCollectionView != null)
        {
            // These optimizations help with scrolling performance on Android
            HistoryCollectionView.RemainingItemsThreshold = 3;
            HistoryCollectionView.RemainingItemsThresholdReachedCommand = null; // Disable if not using infinite scroll
        }
#elif IOS
        // iOS specific optimizations
        if (HistoryCollectionView != null)
        {
            HistoryCollectionView.RemainingItemsThreshold = 5;
        }
#endif

        // Handle screen size changes for responsive design
        this.SizeChanged += OnPageSizeChanged;
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        // Adjust layout based on screen width
        if (Width > 0)
        {
            // For very small screens (< 400), reduce margins
            var margin = Width < 400 ? new Thickness(5, 2) : new Thickness(10, 5);
            
            // Apply margin changes to collection view if needed
            if (HistoryCollectionView != null)
            {
                HistoryCollectionView.Margin = Width < 400 ? new Thickness(5) : new Thickness(0);
            }
        }
    }

    private void LoadWallpapers()
    {
        try
        {
            Wallpapers.Clear();
            
            // Check if history is disabled
            if (!_settingsService.IsHistoryEnabled())
            {
                // History is disabled, don't load anything
                return;
            }
            
            foreach (var item in _historyService.History)
            {
                // Defensive: skip null or incomplete items
                if (item != null && !string.IsNullOrEmpty(item.ImageUrl))
                    Wallpapers.Add(item);
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() => 
                DisplayAlert("Error", $"Failed to load wallpaper history: {ex.Message}", "OK"));
        }
    }

    private void OnWallpaperAdded(object? sender, WallpaperHistoryItem newWallpaper)
    {
        // Update UI on main thread when a new wallpaper is added
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (newWallpaper != null && !string.IsNullOrEmpty(newWallpaper.ImageUrl))
            {
                Wallpapers.Insert(0, newWallpaper);
                
                // Remove excess items if we exceed max history
                var maxHistory = _settingsService.GetMaxHistoryLimit();
                if (maxHistory > 0) // Only manage UI limit if history is enabled
                {
                    while (Wallpapers.Count > maxHistory)
                    {
                        Wallpapers.RemoveAt(Wallpapers.Count - 1);
                    }
                }
                else
                {
                    // If history is disabled (0), clear the UI
                    Wallpapers.Clear();
                }
            }
        });
    }

    private void OnHistoryCleared(object? sender, EventArgs e)
    {
        // Clear UI when history is cleared
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Wallpapers.Clear();
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Unsubscribe to prevent memory leaks
        _historyService.WallpaperAdded -= OnWallpaperAdded;
        _historyService.HistoryCleared -= OnHistoryCleared;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Re-subscribe when page appears (in case it was unsubscribed)
        _historyService.WallpaperAdded -= OnWallpaperAdded; // Remove first to avoid double subscription
        _historyService.WallpaperAdded += OnWallpaperAdded;
        _historyService.HistoryCleared -= OnHistoryCleared;
        _historyService.HistoryCleared += OnHistoryCleared;
        
        // Configure for device type
        ConfigureForDeviceType();
        
        // Update empty state based on history settings
        UpdateEmptyState();
        
        // Reload in case items were added while page was not visible
        LoadWallpapers();
    }

    private void UpdateEmptyState()
    {
        if (!_settingsService.IsHistoryEnabled())
        {
            // History is disabled
            EmptyStateIcon.Text = "??";
            EmptyStateTitle.Text = "History is disabled";
            EmptyStateMessage.Text = "Enable history in settings to track wallpapers";
            EmptyStateButton.Text = "Enable History";
        }
        else
        {
            // History is enabled but empty
            EmptyStateIcon.Text = "??";
            EmptyStateTitle.Text = "No wallpaper history yet";
            EmptyStateMessage.Text = "Wallpapers you've set will appear here";
            EmptyStateButton.Text = "Go to Settings";
        }
    }

    private void ConfigureForDeviceType()
    {
        // Adjust layout for different device types
        if (DeviceInfo.Idiom == DeviceIdiom.Phone)
        {
            // For phones, use more compact layout
            if (HistoryCollectionView != null)
            {
                HistoryCollectionView.ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems;
            }
        }
        else if (DeviceInfo.Idiom == DeviceIdiom.Tablet)
        {
            // For tablets, we can use more spacing
            if (HistoryCollectionView != null)
            {
                HistoryCollectionView.Margin = new Thickness(20);
            }
        }
    }

    private async Task ShowToastOrAlertAsync(string message, bool isError = false)
    {
#if ANDROID || IOS || MACCATALYST
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => Toast.Make(message, isError ? ToastDuration.Long : ToastDuration.Short).Show());
        }
        catch
        {
            await DisplayAlert(isError ? "Error" : "Success", message, "OK");
        }
#else
        await DisplayAlert(isError ? "Error" : "Success", message, "OK");
#endif
    }

    private async void OnResetHistoryClicked(object sender, EventArgs e)
    {
        try
        {
            bool confirm = await DisplayAlert("Confirm Reset", "Are you sure you want to clear all wallpaper history? This action cannot be undone.", "Yes", "No");
            if (confirm)
            {
                _historyService.ClearHistory();
                await ShowToastOrAlertAsync("Wallpaper history has been cleared.");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to reset history: {ex.Message}", "OK");
        }
    }

    private async void OnGoToSettingsClicked(object sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("//SettingsPage");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Navigation Error", $"Failed to navigate to settings: {ex.Message}", "OK");
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is WallpaperHistoryItem item)
        {
            try
            {
                // Disable button during save operation to prevent multiple clicks
                btn.IsEnabled = false;
                btn.Text = "Saving...";
                
                var saveFolder = MauiProgram.Services?.GetService<SettingsService>()?.GetWallpaperSaveFolder();
                if (string.IsNullOrEmpty(saveFolder))
                {
                    await DisplayAlert("Error", "Failed to determine save folder.", "OK");
                    return;
                }

                // Ensure the save folder exists
                try
                {
                    Directory.CreateDirectory(saveFolder);
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to create save folder: {ex.Message}", "OK");
                    return;
                }

                if (string.IsNullOrEmpty(item.ImageUrl))
                {
                    await DisplayAlert("Error", "No image URL available for this wallpaper.", "OK");
                    return;
                }
                
                var fileName = Path.GetFileName(new Uri(item.ImageUrl).LocalPath);
                if (string.IsNullOrEmpty(fileName) || fileName == "/")
                {
                    // Generate a filename if we can't get one from the URL
                    var extension = item.ImageUrl.Contains(".jpg") ? ".jpg" : 
                                   item.ImageUrl.Contains(".png") ? ".png" : 
                                   item.ImageUrl.Contains(".gif") ? ".gif" : ".jpg";
                    fileName = $"wallpaper_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
                }
                
                var destPath = Path.Combine(saveFolder, fileName);
                
                using var client = new HttpClient();
                byte[] bytes = null;
                try
                {
                    bytes = await client.GetByteArrayAsync(item.ImageUrl);
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to download image: {ex.Message}", "OK");
                    return;
                }
                try
                {
                    await File.WriteAllBytesAsync(destPath, bytes);
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to save file: {ex.Message}", "OK");
                    return;
                }
                
                await ShowToastOrAlertAsync($"Saved to {destPath}");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
            }
            finally
            {
                // Re-enable button
                btn.IsEnabled = true;
                btn.Text = "Save";
            }
        }
    }

    private async void OnImageTapped(object sender, EventArgs e)
    {
        await ShowFullImageAsync(sender, e);
    }

    private async void OnViewFullClicked(object sender, EventArgs e)
    {
        await ShowFullImageAsync(sender, e);
    }

    private async Task ShowFullImageAsync(object sender, EventArgs e)
    {
        WallpaperHistoryItem? item = null;

        // Get the wallpaper item from different sender types
        if (sender is Button btn && btn.CommandParameter is WallpaperHistoryItem buttonItem)
        {
            item = buttonItem;
        }
        else if (sender is Image img && img.BindingContext is WallpaperHistoryItem imageItem)
        {
            item = imageItem;
        }
        else if (sender is Frame frame && frame.BindingContext is WallpaperHistoryItem frameItem)
        {
            item = frameItem;
        }
        else if (e is TappedEventArgs tappedArgs && tappedArgs.Parameter is WallpaperHistoryItem tappedItem)
        {
            item = tappedItem;
        }

        if (item == null)
        {
            await DisplayAlert("Error", "Could not load wallpaper details.", "OK");
            return;
        }

        try
        {
            await ShowImageViewerModal(item);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to show full image: {ex.Message}", "OK");
        }
    }

    private async Task ShowImageViewerModal(WallpaperHistoryItem item)
    {
        // Create a modal page for viewing the full-size image
        var modalPage = new ContentPage
        {
            Title = "Wallpaper Viewer",
            BackgroundColor = Colors.Black
        };

        // Create the main layout
        var mainLayout = new Grid
        {
            RowDefinitions = 
            {
                new RowDefinition { Height = GridLength.Auto }, // Header
                new RowDefinition { Height = GridLength.Star }, // Image
                new RowDefinition { Height = GridLength.Auto }  // Footer
            }
        };

        // Header with close button and title
        var headerLayout = new Grid
        {
            ColumnDefinitions = 
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            BackgroundColor = Color.FromArgb("#80000000"),
            Padding = new Thickness(15, 10)
        };

        var closeButton = new Button
        {
            Text = "?",
            FontSize = 18,
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.White,
            WidthRequest = 40,
            HeightRequest = 40
        };
        closeButton.Clicked += async (s, e) => await Navigation.PopModalAsync();

        var titleLabel = new Label
        {
            Text = item.Description ?? "Wallpaper",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        var saveButton = new Button
        {
            Text = "Save",
            FontSize = 14,
            BackgroundColor = Color.FromArgb("#007ACC"),
            TextColor = Colors.White,
            CornerRadius = 5,
            Padding = new Thickness(15, 8)
        };
        saveButton.Clicked += async (s, e) => await SaveFromViewer(item, saveButton);

        headerLayout.Add(closeButton, 0, 0);
        headerLayout.Add(titleLabel, 1, 0);
        headerLayout.Add(saveButton, 2, 0);

        // Main image with scroll view for zooming and panning
        var scrollView = new ScrollView
        {
            Orientation = ScrollOrientation.Both,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            BackgroundColor = Colors.Black
        };

        var fullImage = new Image
        {
            Source = item.ImageUrl, // Use full-size image URL
            Aspect = Aspect.AspectFit,
            BackgroundColor = Colors.Black
        };

        // Add loading indicator
        var loadingIndicator = new ActivityIndicator
        {
            IsRunning = true,
            Color = Colors.White,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };

        var imageContainer = new Grid();
        imageContainer.Add(fullImage);
        imageContainer.Add(loadingIndicator);

        // Hide loading indicator when image loads
        fullImage.PropertyChanged += (s, e) => 
        {
            if (e.PropertyName == nameof(Image.IsLoading))
            {
                loadingIndicator.IsVisible = fullImage.IsLoading;
            }
        };

        scrollView.Content = imageContainer;

        // Footer with image details
        var footerLayout = new StackLayout
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            Padding = new Thickness(15, 10),
            Spacing = 5
        };

        var detailsLabel = new Label
        {
            Text = $"Set: {item.SetTime:g}",
            FontSize = 12,
            TextColor = Colors.White
        };

        var uploaderLabel = new Label
        {
            Text = $"Uploader: {item.Uploader ?? "Unknown"}",
            FontSize = 12,
            TextColor = Colors.LightGray,
            IsVisible = !string.IsNullOrEmpty(item.Uploader)
        };

        var notesLabel = new Label
        {
            Text = item.Notes,
            FontSize = 11,
            TextColor = Colors.LightGray,
            IsVisible = !string.IsNullOrEmpty(item.Notes),
            LineBreakMode = LineBreakMode.WordWrap
        };

        footerLayout.Children.Add(detailsLabel);
        if (!string.IsNullOrEmpty(item.Uploader))
            footerLayout.Children.Add(uploaderLabel);
        if (!string.IsNullOrEmpty(item.Notes))
            footerLayout.Children.Add(notesLabel);

        imageContainer.InputTransparent = false; // Ensure image container receives gestures

        // Add tap gesture to hide/show UI
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) => 
        {
            headerLayout.IsVisible = !headerLayout.IsVisible;
            footerLayout.IsVisible = !footerLayout.IsVisible;
        };
        imageContainer.GestureRecognizers.Add(tapGesture);

        // Add pinch gesture for zooming (basic implementation)
        var pinchGesture = new PinchGestureRecognizer();
        pinchGesture.PinchUpdated += (s, e) =>
        {
            if (e.Status == GestureStatus.Running)
            {
                // Calculate the scale factor
                var newScale = Math.Max(0.5, Math.Min(3.0, fullImage.Scale * e.Scale));
                fullImage.Scale = newScale;
            }
        };
        imageContainer.GestureRecognizers.Add(pinchGesture);

        // Assemble the layout
        mainLayout.Add(headerLayout, 0, 0);
        mainLayout.Add(scrollView, 0, 1);
        mainLayout.Add(footerLayout, 0, 2);

        modalPage.Content = mainLayout;

        // Present the modal
        await Navigation.PushModalAsync(modalPage);
    }

    private async Task SaveFromViewer(WallpaperHistoryItem item, Button saveButton)
    {
        var originalText = saveButton.Text;
        try
        {
            saveButton.IsEnabled = false;
            saveButton.Text = "Saving...";
            
            var saveFolder = _settingsService.GetWallpaperSaveFolder();
            if (string.IsNullOrEmpty(saveFolder))
            {
                await DisplayAlert("Error", "Failed to determine save folder.", "OK");
                return;
            }

            Directory.CreateDirectory(saveFolder);

            var fileName = Path.GetFileName(new Uri(item.ImageUrl).LocalPath);
            if (string.IsNullOrEmpty(fileName) || fileName == "/")
            {
                var extension = item.ImageUrl.Contains(".jpg") ? ".jpg" : 
                               item.ImageUrl.Contains(".png") ? ".png" : 
                               item.ImageUrl.Contains(".gif") ? ".gif" : ".jpg";
                fileName = $"wallpaper_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
            }
            
            var destPath = Path.Combine(saveFolder, fileName);
            
            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(item.ImageUrl);
            await File.WriteAllBytesAsync(destPath, bytes);
            
            await ShowToastOrAlertAsync($"Saved to {destPath}");
            saveButton.Text = "Saved ?";
            await Task.Delay(2000); // Show confirmation for 2 seconds
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
        finally
        {
            saveButton.IsEnabled = true;
            saveButton.Text = originalText;
        }
    }
}
