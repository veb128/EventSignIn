using EventCheckIn.Services;

namespace EventCheckIn;

public partial class App : Application
{
	readonly SyncService _sync;

	public App(SyncService sync)
	{
		InitializeComponent();
		_sync = sync;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		_sync.Start(); // background upload of saved check-ins to the host
		return new Window(new AppShell());
	}
}
