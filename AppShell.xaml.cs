using AnonWallClient.Views;
using AnonWallClient.Services;
namespace AnonWallClient;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("HistoryPage", typeof(HistoryPage));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            // Check if a Link ID has been saved before
            var savedLinkId = MauiProgram.Services?.GetService<SettingsService>()?.GetLinkId() ?? string.Empty;

            // If no Link ID is found, force the user to the Settings page
            if (string.IsNullOrEmpty(savedLinkId))
            {
                try
                {
                    await Current.GoToAsync("//SettingsPage");
                }
                catch (Exception navEx)
                {
                    await Shell.Current.DisplayAlert("Navigation Error", $"Failed to navigate: {navEx.Message}", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Startup Error", $"An error occurred: {ex.Message}", "OK");
        }
    }
}