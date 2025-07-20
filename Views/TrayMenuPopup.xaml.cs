using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;

namespace AnonWallClient.Views;

public partial class TrayMenuPopup : Popup
{
    public TrayMenuPopup(Action onOpen, Action onPanic, Action onExit)
    {
        InitializeComponent();
        OpenButton.Clicked += (s, e) => { onOpen(); Close(); };
        PanicButton.Clicked += (s, e) => { onPanic(); Close(); };
        ExitButton.Clicked += (s, e) => { onExit(); Close(); };
    }
}
