using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace Tetris;

public static class MauiProgram
{
	private static bool _keyboardAttached;

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			})
			.ConfigureLifecycleEvents(events =>
			{
#if WINDOWS
				events.AddWindows(windowsLifecycle =>
				{
					windowsLifecycle.OnWindowCreated(window =>
					{
						// Position window near top of screen
						var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
						var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
						var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
						var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
						int screenWidth = displayArea.WorkArea.Width;
						int winWidth = 900;
						int winHeight = 950;
						int x = (screenWidth - winWidth) / 2;
						int y = 20; // Near top of screen
						appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, winWidth, winHeight));

						window.Activated += OnWindowActivated;
					});
				});
#endif
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

#if WINDOWS
	private static void OnWindowActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs e)
	{
		if (_keyboardAttached) return;

		var window = sender as Microsoft.UI.Xaml.Window;
		if (window?.Content == null) return;

		var mauiWindow = Application.Current?.Windows.FirstOrDefault();
		var shell = mauiWindow?.Page as Shell;
		var page = shell?.CurrentPage as Views.GamePage;

		if (page != null)
		{
			WinUI.KeyboardHandler.Attach(window, page);
			_keyboardAttached = true;
		}
	}
#endif
}
