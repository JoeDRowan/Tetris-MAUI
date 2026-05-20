using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Tetris.Views;

namespace Tetris.WinUI;

/// <summary>
/// Windows-specific keyboard input handler for the Tetris game.
/// Hooks into the WinUI window's key events.
/// </summary>
public static class KeyboardHandler
{
    public static void Attach(Microsoft.UI.Xaml.Window window, GamePage gamePage)
    {
        window.Content.KeyDown += (sender, args) =>
        {
            var key = MapKey(args.Key);
            if (key != null && gamePage.HandleKeyDown(key))
            {
                args.Handled = true;
            }
        };

        window.Content.KeyUp += (sender, args) =>
        {
            var key = MapKey(args.Key);
            if (key != null && gamePage.HandleKeyUp(key))
            {
                args.Handled = true;
            }
        };
    }

    private static string? MapKey(Windows.System.VirtualKey key) => key switch
    {
        Windows.System.VirtualKey.Left => "left",
        Windows.System.VirtualKey.Right => "right",
        Windows.System.VirtualKey.Up => "up",
        Windows.System.VirtualKey.Down => "down",
        Windows.System.VirtualKey.Space => "space",
        Windows.System.VirtualKey.Escape => "escape",
        Windows.System.VirtualKey.A => "a",
        Windows.System.VirtualKey.D => "d",
        Windows.System.VirtualKey.W => "w",
        Windows.System.VirtualKey.S => "s",
        Windows.System.VirtualKey.P => "p",
        Windows.System.VirtualKey.E => "e",
        _ => null
    };
}
