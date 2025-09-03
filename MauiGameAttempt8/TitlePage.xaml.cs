namespace MauiGameAttempt8;
#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;
using Microsoft.UI.Xaml.Input;
using Windows;
using Windows.UI.Core;
using Windows.System;
#endif
public partial class TitlePage : ContentPage
{
	public TitlePage()
	{
		InitializeComponent();
    }
    void makeFullScreen()
    {
#if WINDOWS
    var window = App.Current.Windows[0].Handler.PlatformView as Microsoft.UI.Xaml.Window;
    IntPtr hwnd = WindowNative.GetWindowHandle(window);
    WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
    AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

    if (appWindow.Presenter is OverlappedPresenter presenter)
    {
            presenter.Maximize();
    }
#endif
    }

    private void StartButton_Pressed(object sender, EventArgs e)
    {
        Navigation.PushAsync(new GameplayPage());
    }

    private async void TitlePage_Appeared(object sender, EventArgs e)
    {
        while (App.Current.Windows.Count == 0)
        {
            await Task.Delay(100);
        }
        while (App.Current.Windows[0].Handler == null)
        {
            await Task.Delay(100);
        }
        makeFullScreen();
    }
}