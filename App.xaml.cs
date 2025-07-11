using AnonWallClient.Background;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace AnonWallClient;

public partial class App : Application
{
    private readonly MainPage _mainPage;

    public App(MainPage mainPage, PollingService pollingService)
    {
        InitializeComponent();
        _mainPage = mainPage;

        // This starts the background task, but the service will wait for our command.
        Task.Run(() => pollingService.StartPollingAsync(new CancellationToken()));
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_mainPage);
    }
}