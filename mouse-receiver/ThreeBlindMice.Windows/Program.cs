using ThreeBlindMice.Core;

namespace ThreeBlindMice.Windows;

class Program
{
	static async Task Main(string[] args)
	{
		Console.WriteLine("Three Blind Mice — Windows Mouse Receiver");

		// Parse session code from args: either a tbm:// URI or --session <code>
		var session_code = ParseSessionCode(args);
		if (session_code == null)
		{
			Console.Error.WriteLine("Usage:");
			Console.Error.WriteLine("  ThreeBlindMice.Windows tbm://session/<code>");
			Console.Error.WriteLine("  ThreeBlindMice.Windows --session <code> [--monitor N]");
			Environment.Exit(1);
			return;
		}

		Console.WriteLine($"Session code: {session_code}");

		// Register the tbm:// protocol handler if not already done
		if (!ProtocolRegistrar.Is_Registered())
		{
			ProtocolRegistrar.Register();
			Console.WriteLine("Registered tbm:// protocol handler.");
		}

		// Enumerate monitors and select the requested one
		var monitors = MonitorEnumerator.Enumerate();
		Console.WriteLine($"Detected {monitors.Count} monitor(s):");
		foreach (var mon in monitors)
		{
			var primary_tag = mon.Is_Primary ? " (primary)" : "";
			Console.WriteLine($"  [{mon.Index}] {mon.Device}  {mon.Width}x{mon.Height} at ({mon.Left},{mon.Top}){primary_tag}");
		}

		var monitor_index = ParseMonitorIndex(args);
		if (monitor_index < 0 || monitor_index >= monitors.Count)
		{
			if (monitor_index != 0)
				Console.WriteLine($"Monitor index {monitor_index} out of range, falling back to primary.");
			monitor_index = 0;
		}

		var selected = monitors[monitor_index];
		Console.WriteLine($"Using monitor [{selected.Index}] {selected.Device} — {selected.Width}x{selected.Height} at ({selected.Left},{selected.Top})");

		var exit = new ManualResetEventSlim(false);

		// Create and start the overlay window on the selected monitor
		using var overlay = new Win32Overlay(selected.Left, selected.Top, selected.Width, selected.Height);
		overlay.Start();
		Console.WriteLine("Overlay window created.");

		// Create cursor state and renderer
		var cursor_state = new CursorState();
		using var renderer = new CursorRenderer();

		// Hook overlay paint to render cursors
		overlay.On_Paint = (hdc, w, h) =>
		{
			cursor_state.Remove_Inactive();
			renderer.Render(hdc, cursor_state, w, h);
		};

		// Create PubSub client and wire up message handling
		using var pubsub = new PubSubClient();
		pubsub.On_Message += msg =>
		{
			switch (msg)
			{
				case CursorMessage cursor_msg:
					cursor_state.Update_Cursor(cursor_msg);
					overlay.RequestRepaint();
					break;
				case JoinMessage join_msg:
					cursor_state.Add_User(join_msg);
					overlay.RequestRepaint();
					break;
				case LeaveMessage leave_msg:
					cursor_state.Remove_User(leave_msg.User_Id);
					overlay.RequestRepaint();
					break;
			}
		};

		pubsub.On_Connected += () => Console.WriteLine("Connected to Web PubSub.");
		pubsub.On_Disconnected += () => Console.WriteLine("Disconnected from Web PubSub.");
		pubsub.On_Error += err => Console.Error.WriteLine($"PubSub error: {err}");

		// Connect to Web PubSub
		const string negotiate_url = "https://mango-beach-0f96e6000.1.azurestaticapps.net/api/negotiate";
		var host_id = $"host-{Guid.NewGuid():N}";
		await pubsub.Connect(negotiate_url, session_code, host_id);

		// Create system tray icon
		using var tray = new TrayIcon(() =>
		{
			overlay.Shutdown();
			exit.Set();
		});
		tray.Show(session_code);

		// Handle Ctrl+C for clean shutdown
		Console.CancelKeyPress += (_, e) =>
		{
			e.Cancel = true;
			overlay.Shutdown();
			exit.Set();
		};

		Console.WriteLine("Running. Right-click tray icon or press Ctrl+C to exit.");
		exit.Wait();

		await pubsub.Disconnect();
		Console.WriteLine("Exiting.");
	}

	private static string? ParseSessionCode(string[] args)
	{
		if (args.Length == 0)
			return null;

		// Try tbm:// URI first (e.g. launched via protocol handler)
		if (args[0].StartsWith("tbm://", StringComparison.OrdinalIgnoreCase))
			return TbmUriParser.TryParseSessionCode(args[0]);

		// Try --session <code>
		for (var i = 0; i < args.Length - 1; i++)
		{
			if (args[i] == "--session")
			{
				var code = args[i + 1];
				return TbmUriParser.IsValidSessionCode(code) ? code : null;
			}
		}

		return null;
	}

	/// <summary>
	/// Parses the --monitor N argument. Returns 0 (primary) if not specified.
	/// </summary>
	private static int ParseMonitorIndex(string[] args)
	{
		for (var i = 0; i < args.Length - 1; i++)
		{
			if (args[i] == "--monitor" && int.TryParse(args[i + 1], out var index))
				return index;
		}
		return 0;
	}
}
