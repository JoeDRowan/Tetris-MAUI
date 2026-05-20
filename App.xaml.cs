namespace Tetris;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell())
		{
			Title = "Tetris",
			Width = 600,
			Height = 750
		};
		return window;
	}
}