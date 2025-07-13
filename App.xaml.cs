namespace AnonWallClient;

public partial class App : Application
{
    // The main page is now the AppShell
    public App(AppShell shell)
    {
        InitializeComponent();
        MainPage = shell;
    }

    // We no longer need the OnAppearing logic here, it will be handled by the pages
}