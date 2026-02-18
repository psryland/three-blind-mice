using ThreeBlindMice.Core;

namespace ThreeBlindMice.Linux;

class Program
{
	private const string NegotiateUrl = "https://three-blind-mice.rylogic.co.nz/api/negotiate";

	static async Task<int> Main(string[] args)
	{
		Console.WriteLine("Three Blind Mice — Linux Mouse Receiver");

		var session_code = ParseSessionCode(args);
		if (session_code is null)
		{
			Console.Error.WriteLine("Usage: ThreeBlindMice.Linux tbm://session/<code>");
			Console.Error.WriteLine("       ThreeBlindMice.Linux --session <code>");
			Console.Error.WriteLine("Session code must be 4-8 alphanumeric characters.");
			return 1;
		}

		Console.WriteLine($"Joining session: {session_code}");

		// Overlay requires a running X11 display — guard at runtime
		if (!OperatingSystem.IsLinux())
		{
			Console.Error.WriteLine("X11 overlay is only supported on Linux.");
			return 1;
		}

		var cursor_state = new CursorState();
		var tray = new TrayIcon();
		tray.Show();

		// Connect to Web PubSub for cursor updates
		using var pubsub = new PubSubClient();
		var user_id = $"host-{Guid.NewGuid():N}"[..16];

		pubsub.On_Message += msg =>
		{
			switch (msg)
			{
				case CursorMessage cursor_msg:
					cursor_state.Update_Cursor(cursor_msg);
					break;
				case JoinMessage join_msg:
					cursor_state.Add_User(join_msg);
					break;
				case LeaveMessage leave_msg:
					cursor_state.Remove_User(leave_msg.User_Id);
					break;
			}
		};

		pubsub.On_Connected += () =>
		{
			Console.WriteLine("Connected to Web PubSub.");
			tray.Update_Session(session_code);
		};

		pubsub.On_Disconnected += () =>
		{
			Console.WriteLine("Disconnected from Web PubSub. Reconnecting...");
		};

		pubsub.On_Error += err =>
		{
			Console.Error.WriteLine($"PubSub error: {err}");
		};

		// Start the WebSocket connection in the background
		_ = Task.Run(async () =>
		{
			try
			{
				await pubsub.Connect(NegotiateUrl, session_code, user_id);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"Failed to connect: {ex.Message}");
			}
		});

		// Create and run the overlay
		using var overlay = new X11Overlay();
		Console.CancelKeyPress += (_, e) =>
		{
			e.Cancel = true;
			overlay.Shutdown();
		};

		overlay.OnRender = (display, window, gc, w, h) =>
		{
			CursorRenderer.Render(display, window, gc, cursor_state, w, h);
		};

		overlay.Init();
		Console.WriteLine($"Overlay running ({overlay.ScreenWidth}x{overlay.ScreenHeight}). Press Ctrl+C to exit.");
		overlay.Run();

		// Clean shutdown
		tray.Shutdown();
		await pubsub.Disconnect();

		Console.WriteLine("Shutdown complete.");
		return 0;
	}

	/// <summary>
	/// Extract session code from either tbm://session/XXXX URI or --session XXXX args.
	/// Uses the shared TbmUriParser from Core for validation.
	/// </summary>
	private static string? ParseSessionCode(string[] args)
	{
		if (args.Length == 0)
			return null;

		// Try tbm:// URI first (e.g. launched via protocol handler)
		if (args[0].StartsWith("tbm://", StringComparison.OrdinalIgnoreCase))
			return TbmUriParser.TryParseSessionCode(args[0]);

		// Try --session <code>
		if (args.Length >= 2 && args[0] == "--session")
		{
			var code = args[1];
			return TbmUriParser.IsValidSessionCode(code) ? code : null;
		}

		return null;
	}
}
