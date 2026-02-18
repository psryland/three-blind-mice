namespace ThreeBlindMice.Linux;

/// <summary>
/// Stub system tray icon for Linux.
/// A full implementation would use the StatusNotifierItem D-Bus interface
/// (freedesktop.org spec) or the legacy XEmbed system tray protocol.
/// For now, status is written to stdout as a minimal notification mechanism.
/// </summary>
internal sealed class TrayIcon
{
	public void Show()
	{
		Console.WriteLine("[Tray] Three Blind Mice is running.");
	}

	public void Update_Room(string code)
	{
		Console.WriteLine($"[Tray] Connected to room: {code}");
	}

	public void Shutdown()
	{
		Console.WriteLine("[Tray] Shutting down.");
	}
}
