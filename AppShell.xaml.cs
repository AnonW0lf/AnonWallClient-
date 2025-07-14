using AnonWallClient.Views;
namespace AnonWallClient;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Check if a Link ID has been saved before
        var savedLinkId = Preferences.Get("link_id", string.Empty);

        // If no Link ID is found, force the user to the Settings page
        if (string.IsNullOrEmpty(savedLinkId))
        {
            await Current.GoToAsync("//SettingsPage");
        }
    }
}