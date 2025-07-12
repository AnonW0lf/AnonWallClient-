using AnonWallClient.Background;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace AnonWallClient;

public partial class App : Application
{
    private readonly MainPage _mainPage;

    public App(MainPage mainPage)
    {
        InitializeComponent();
        _mainPage = mainPage;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_mainPage);
    }
}