using Cartex.Mobile.Agent.Services;

namespace Cartex.Mobile.Agent;

public partial class App : Application
{
	public App()
	{
		Loc.Instance.InitAsync().GetAwaiter().GetResult();
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}