using ThreeBlindMice.Core;

namespace ThreeBlindMice.Windows;

class Program
{
	static async Task Main(string[] args)
	{
		Console.WriteLine("Three Blind Mice — Windows Mouse Receiver");

		// Parse room code from args: either a tbm:// URI or --room <code>
		var room_code = ParseRoomCode(args);
		if (room_code == null)
		{
			Console.Error.WriteLine("Usage:");
			Console.Error.WriteLine("  ThreeBlindMice.Windows tbm://room/<code>");
			Console.Error.WriteLine("  ThreeBlindMice.Windows --room <code>");
			Environment.Exit(1);
			return;
		}

		Console.WriteLine($"Room code: {room_code}");

		// Register the tbm:// protocol handler if not already done
		if (!ProtocolRegistrar.Is_Registered())
		{
			ProtocolRegistrar.Register();
			Console.WriteLine("Registered tbm:// protocol handler.");
		}

		var exit = new ManualResetEventSlim(false);

		// Create and start the overlay window
		using var overlay = new Win32Overlay();
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
		const string negotiate_url = "https://three-blind-mice.rylogic.co.nz/api/negotiate";
		var host_id = $"host-{Guid.NewGuid():N}";
		await pubsub.Connect(negotiate_url, room_code, host_id);

		// Create system tray icon
		using var tray = new TrayIcon(() =>
		{
			overlay.Shutdown();
			exit.Set();
		});
		tray.Show(room_code);

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

	private static string? ParseRoomCode(string[] args)
	{
		if (args.Length == 0)
			return null;

		// Try tbm:// URI first (e.g. launched via protocol handler)
		if (args[0].StartsWith("tbm://", StringComparison.OrdinalIgnoreCase))
			return TbmUriParser.TryParseRoomCode(args[0]);

		// Try --room <code>
		if (args.Length >= 2 && args[0] == "--room")
		{
			var code = args[1];
			return TbmUriParser.IsValidRoomCode(code) ? code : null;
		}

		return null;
	}
}
