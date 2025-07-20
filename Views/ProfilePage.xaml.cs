using AnonWallClient.Models;
using AnonWallClient.Services;
using AnonWallClient.Background;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace AnonWallClient.Views;

public partial class ProfilePage : ContentPage
{
    private readonly UserProfileService _userProfileService;
    private readonly AppLogService _logger;
    private readonly SettingsService _settingsService;
    private readonly IWallpaperService _wallpaperService;
    private readonly PollingService _pollingService;
    private ObservableCollection<RecentWallpaper> _recentWallpapers = new();

    public ProfilePage(UserProfileService userProfileService, AppLogService logger, SettingsService settingsService, PollingService pollingService)
    {
        InitializeComponent();
        _userProfileService = userProfileService;
        _logger = logger;
        _settingsService = settingsService;
        _pollingService = pollingService;
        _wallpaperService = MauiProgram.Services?.GetService<IWallpaperService>()!;
        
        RecentWallpapersCollection.ItemsSource = _recentWallpapers;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        try
        {
            SetLoadingState(true);

            var profile = await _userProfileService.GetUserProfileAsync();
            if (profile != null)
            {
                DisplayProfile(profile);
                SetLoadingState(false);
            }
            else
            {
                ShowErrorState("Unable to load user profile. Please check your API key and internet connection.");
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"ProfilePage: Error loading profile: {ex.Message}");
            ShowErrorState($"Error loading profile: {ex.Message}");
        }
    }

    private void SetLoadingState(bool isLoading)
    {
        LoadingIndicator.IsVisible = isLoading;
        LoadingIndicator.IsRunning = isLoading;
        MainContent.IsVisible = !isLoading;
        ErrorContent.IsVisible = false;
    }

    private void ShowErrorState(string message)
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        MainContent.IsVisible = false;
        ErrorContent.IsVisible = true;
        ErrorMessageLabel.Text = message;
    }

    private void DisplayProfile(UserProfile profile)
    {
        try
        {
            // Basic user info
            UsernameLabel.Text = profile.Username;
            UserIdLabel.Text = $"ID: {profile.Id}";
            
            // Last online info (if available from HTML)
            if (profile.HasHtmlData && !string.IsNullOrEmpty(profile.LastOnlineText))
            {
                LastOnlineLabel.Text = $"Last online: {profile.LastOnlineText}";
                LastOnlineLabel.IsVisible = true;
            }
            else
            {
                LastOnlineLabel.IsVisible = false;
            }
            
            // Online status
            if (profile.Online)
            {
                OnlineStatusFrame.BackgroundColor = Colors.Green;
                OnlineStatusLabel.Text = "🟢 Online";
            }
            else
            {
                OnlineStatusFrame.BackgroundColor = Colors.Gray;
                OnlineStatusLabel.Text = "⚫ Offline";
            }

            // Enhanced stats
            SetCountLabel.Text = profile.HasHtmlData ? profile.WallpapersSet.ToString() : profile.SetCount.ToString();
            LinksCountLabel.Text = profile.Links.Count.ToString();
            
            if (profile.HasHtmlData)
            {
                OrgasmsLabel.Text = profile.OrgasmsCaused.ToString();
                RecentOrgasmsLabel.Text = profile.RecentOrgasms.ToString();
            }
            else
            {
                OrgasmsLabel.Text = "?";
                RecentOrgasmsLabel.Text = "?";
            }

            // Profile description (HTML only)
            if (profile.HasHtmlData && !string.IsNullOrEmpty(profile.ProfileDescription))
            {
                ProfileDescriptionFrame.IsVisible = true;
                ProfileDescriptionLabel.Text = profile.ProfileDescription;
            }
            else
            {
                ProfileDescriptionFrame.IsVisible = false;
            }

            // Recent wallpapers gallery (HTML only)
            if (profile.HasHtmlData && profile.RecentWallpapers.Any())
            {
                RecentWallpapersSection.IsVisible = true;
                _recentWallpapers.Clear();
                foreach (var wallpaper in profile.RecentWallpapers)
                {
                    _recentWallpapers.Add(wallpaper);
                }
            }
            else
            {
                RecentWallpapersSection.IsVisible = false;
            }

            // Activity chart (HTML only)
            if (profile.HasHtmlData && profile.ActivityChart.Any())
            {
                ActivityChartFrame.IsVisible = true;
                var totalActivity = profile.ActivityChart.Sum(a => a.Value);
                var avgDaily = profile.ActivityChart.Count > 0 ? (double)totalActivity / profile.ActivityChart.Count : 0;
                ActivityChartLabel.Text = $"7-day total: {totalActivity} 💦 (avg: {avgDaily:F1}/day)";
            }
            else
            {
                ActivityChartFrame.IsVisible = false;
            }

            // Status badges
            CreateStatusBadges(profile);

            // Links
            CreateLinksDisplay(profile.Links);
        }
        catch (Exception ex)
        {
            _logger.Add($"ProfilePage: Error displaying profile: {ex.Message}");
        }
    }

    private void CreateStatusBadges(UserProfile profile)
    {
        StatusBadgesLayout.Children.Clear();

        if (profile.Authenticated)
            AddStatusBadge("✅ Authenticated", Colors.Green);

        if (profile.Self)
            AddStatusBadge("🔑 Your Account", Colors.Blue);

        if (profile.Friend)
            AddStatusBadge("👥 Friend", Colors.Orange);

        if (profile.IsSupporter)
            AddStatusBadge("💎 Supporter", Colors.Purple);

        if (profile.IsCutie)
            AddStatusBadge("💖 Cutie", Colors.Pink);

        if (profile.IsReporter)
            AddStatusBadge("🛡️ Reporter", Colors.Red);

        if (profile.Master)
            AddStatusBadge("👑 Master", Colors.Gold);

        if (!string.IsNullOrEmpty(profile.Flair))
            AddStatusBadge($"🏷️ {profile.Flair}", Colors.Cyan);

        if (profile.HasHtmlData)
            AddStatusBadge("🌐 Enhanced Data", Colors.LightBlue);
    }

    private void AddStatusBadge(string text, Color backgroundColor)
    {
        var frame = new Frame
        {
            BackgroundColor = backgroundColor,
            CornerRadius = 12,
            Padding = new Thickness(8, 4),
            HasShadow = false,
            Margin = new Thickness(2)
        };

        var label = new Label
        {
            Text = text,
            FontSize = 10,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold
        };

        frame.Content = label;
        StatusBadgesLayout.Children.Add(frame);
    }

    private void CreateLinksDisplay(List<UserProfileLink> links)
    {
        LinksContainer.Children.Clear();

        if (!links.Any())
        {
            var noLinksLabel = new Label
            {
                Text = "No links found",
                FontAttributes = FontAttributes.Italic,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 20),
                TextColor = Application.Current?.Resources.TryGetValue("SecondaryTextColor", out var secTextColor) == true ? (Color)secTextColor : Colors.Gray
            };
            LinksContainer.Children.Add(noLinksLabel);
            return;
        }

        foreach (var link in links.OrderByDescending(l => l.UpdatedAt))
        {
            CreateEnhancedLinkCard(link);
        }
    }

    private void CreateEnhancedLinkCard(UserProfileLink link)
    {
        // Get theme-aware colors
        var cardBackgroundColor = Application.Current?.Resources.TryGetValue("CardBackgroundColor", out var cardBg) == true ? (Color)cardBg : Colors.DarkGray;
        var primaryTextColor = Application.Current?.Resources.TryGetValue("PrimaryTextColor", out var primaryText) == true ? (Color)primaryText : Colors.White;
        var secondaryTextColor = Application.Current?.Resources.TryGetValue("SecondaryTextColor", out var secondaryText) == true ? (Color)secondaryText : Colors.LightGray;
        var tertiaryTextColor = Application.Current?.Resources.TryGetValue("TertiaryTextColor", out var tertiaryText) == true ? (Color)tertiaryText : Colors.Gray;

        // Check if this link is currently configured
        var currentWallpaperLinkId = _settingsService.GetWallpaperLinkId();
        var currentLockscreenLinkId = _settingsService.GetLockscreenLinkId();
        var isWallpaperConfigured = currentWallpaperLinkId == link.Id.ToString();
        var isLockscreenConfigured = currentLockscreenLinkId == link.Id.ToString();

        var frame = new Frame
        {
            BackgroundColor = cardBackgroundColor,
            CornerRadius = 12,
            Padding = new Thickness(15),
            Margin = new Thickness(0, 5),
            HasShadow = true
        };

        var mainGrid = new Grid
        {
            ColumnDefinitions = 
            {
                new ColumnDefinition { Width = new GridLength(80, GridUnitType.Absolute) },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnSpacing = 10,
            RowSpacing = 5
        };

        // Thumbnail
        var thumbnail = new Image
        {
            Source = link.PostThumbnailUrl,
            WidthRequest = 70,
            HeightRequest = 70,
            Aspect = Aspect.AspectFill
        };
        thumbnail.SetValue(Grid.RowSpanProperty, 5);
        mainGrid.Children.Add(thumbnail);

        // Link Info
        var titleLabel = new Label
        {
            Text = $"Link #{link.Id}",
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            TextColor = primaryTextColor
        };
        titleLabel.SetValue(Grid.ColumnProperty, 1);
        mainGrid.Children.Add(titleLabel);

        var termsLabel = new Label
        {
            Text = string.IsNullOrEmpty(link.Terms) ? "No terms specified" : link.Terms,
            FontSize = 12,
            MaxLines = 2,
            LineBreakMode = LineBreakMode.TailTruncation,
            TextColor = secondaryTextColor
        };
        termsLabel.SetValue(Grid.ColumnProperty, 1);
        termsLabel.SetValue(Grid.RowProperty, 1);
        mainGrid.Children.Add(termsLabel);

        // Enhanced info from HTML
        var enhancedInfo = new List<string>();
        if (!string.IsNullOrEmpty(link.Theme))
            enhancedInfo.Add($"Theme: {link.Theme}");
        if (link.Abilities.Any())
            enhancedInfo.Add($"Abilities: {string.Join(", ", link.Abilities)}");
        if (!string.IsNullOrEmpty(link.DeviceInUse))
            enhancedInfo.Add($"Device: {link.DeviceInUse}");

        if (enhancedInfo.Any())
        {
            var enhancedLabel = new Label
            {
                Text = string.Join(" • ", enhancedInfo),
                FontSize = 10,
                TextColor = tertiaryTextColor
            };
            enhancedLabel.SetValue(Grid.ColumnProperty, 1);
            enhancedLabel.SetValue(Grid.RowProperty, 2);
            mainGrid.Children.Add(enhancedLabel);
        }

        var infoLabel = new Label
        {
            Text = $"Updated: {link.UpdatedAt:MMM dd, yyyy} • {(link.Online ? "🟢 Online" : "⚫ Offline")}",
            FontSize = 10,
            TextColor = tertiaryTextColor
        };
        infoLabel.SetValue(Grid.ColumnProperty, 1);
        infoLabel.SetValue(Grid.RowProperty, 3);
        mainGrid.Children.Add(infoLabel);

        // Response info (if available)
        if (link.HasResponse)
        {
            var responseFrame = new Frame
            {
                BackgroundColor = Colors.LightBlue,
                CornerRadius = 6,
                Padding = new Thickness(6, 3),
                Margin = new Thickness(0, 5, 0, 0)
            };

            var responseLabel = new Label
            {
                Text = $"{link.ResponseEmoji} {link.ResponseDisplayText}",
                FontSize = 10,
                TextColor = Colors.DarkBlue,
                FontAttributes = FontAttributes.Bold
            };

            responseFrame.Content = responseLabel;
            responseFrame.SetValue(Grid.ColumnProperty, 1);
            responseFrame.SetValue(Grid.RowProperty, 4);
            mainGrid.Children.Add(responseFrame);
        }

        // Action Buttons with Configuration States
        var buttonsStack = new VerticalStackLayout
        {
            Spacing = 5
        };

        // Wallpaper Button with status indicator
        var wallpaperButton = new Button
        {
            FontSize = 12,
            WidthRequest = 40,
            HeightRequest = 35,
            CornerRadius = 8,
            TextColor = Colors.White,
            Padding = new Thickness(0)
        };

        if (isWallpaperConfigured)
        {
            wallpaperButton.Text = "✓";
            wallpaperButton.BackgroundColor = Colors.Green;
        }
        else
        {
            wallpaperButton.Text = "🖥️";
            wallpaperButton.BackgroundColor = Colors.Blue;
        }

        wallpaperButton.Clicked += (s, e) => OnSetLinkIdClicked(link, WallpaperType.Wallpaper);

        // Lockscreen Button with status indicator
        var lockscreenButton = new Button
        {
            FontSize = 12,
            WidthRequest = 40,
            HeightRequest = 35,
            CornerRadius = 8,
            TextColor = Colors.White,
            Padding = new Thickness(0)
        };

        if (isLockscreenConfigured)
        {
            lockscreenButton.Text = "✓";
            lockscreenButton.BackgroundColor = Colors.Green;
        }
        else
        {
            lockscreenButton.Text = "🔒";
            lockscreenButton.BackgroundColor = Colors.Orange;
        }

        lockscreenButton.Clicked += (s, e) => OnSetLinkIdClicked(link, WallpaperType.Lockscreen);

        buttonsStack.Children.Add(wallpaperButton);
        buttonsStack.Children.Add(lockscreenButton);

        buttonsStack.SetValue(Grid.ColumnProperty, 2);
        buttonsStack.SetValue(Grid.RowSpanProperty, 5);
        mainGrid.Children.Add(buttonsStack);

        frame.Content = mainGrid;
        LinksContainer.Children.Add(frame);
    }

    private async void OnSetLinkIdClicked(UserProfileLink link, WallpaperType wallpaperType)
    {
        try
        {
            var typeText = wallpaperType == WallpaperType.Lockscreen ? "lockscreen" : "wallpaper";
            var linkIdString = link.Id.ToString();
            
            // Get current configured link ID for this type
            var currentLinkId = wallpaperType == WallpaperType.Lockscreen 
                ? _settingsService.GetLockscreenLinkId() 
                : _settingsService.GetWallpaperLinkId();

            if (currentLinkId == linkIdString)
            {
                // Already configured - show info
                await ShowToastAsync($"✓ Link #{link.Id} is already configured for {typeText}");
                return;
            }

            await ShowToastAsync($"Setting Link #{link.Id} as {typeText} source...");

            // Update the settings configuration
            if (wallpaperType == WallpaperType.Lockscreen)
            {
                _settingsService.SetLockscreenLinkId(linkIdString);
            }
            else
            {
                _settingsService.SetWallpaperLinkId(linkIdString);
            }

            // If we're in shared link mode, update both
            if (_settingsService.GetLinkIdMode() == LinkIdMode.SharedLink)
            {
                _settingsService.SetSharedLinkId(linkIdString);
            }

            // Restart polling with new configuration
            _pollingService.EnablePolling();

            await ShowToastAsync($"✅ {typeText.Substring(0, 1).ToUpper() + typeText.Substring(1)} source configured! Polling restarted.");

            // Refresh the UI to show updated checkmarks
            await RefreshLinksDisplay();
        }
        catch (Exception ex)
        {
            _logger.Add($"ProfilePage: Error setting link ID: {ex.Message}");
            await ShowToastAsync($"Error: {ex.Message}", true);
        }
    }

    private async Task RefreshLinksDisplay()
    {
        try
        {
            // Get current profile data
            var profile = await _userProfileService.GetUserProfileAsync(forceRefresh: false);
            if (profile != null)
            {
                // Recreate links display with updated configuration states
                CreateLinksDisplay(profile.Links);
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"ProfilePage: Error refreshing links display: {ex.Message}");
        }
    }

    private async Task ShowToastAsync(string message, bool isError = false)
    {
#if ANDROID || IOS || MACCATALYST
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => 
                Toast.Make(message, isError ? ToastDuration.Long : ToastDuration.Short).Show());
        }
        catch
        {
            await DisplayAlert(isError ? "Error" : "Success", message, "OK");
        }
#else
        await DisplayAlert(isError ? "Error" : "Success", message, "OK");
#endif
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        _userProfileService.ClearCache();
        await LoadProfileAsync();
    }

    private async void OnRetryClicked(object sender, EventArgs e)
    {
        await LoadProfileAsync();
    }
}
