using System.Text.RegularExpressions;

namespace ThreeBlindMice.Linux;

class Program
{
	static int Main(string[] args)
	{
		Console.WriteLine("Three Blind Mice — Linux Mouse Receiver");

		var room_code = ParseRoomCode(args);
		if (room_code is null)
		{
			Console.Error.WriteLine("Usage: ThreeBlindMice.Linux tbm://room/<code>");
			Console.Error.WriteLine("       ThreeBlindMice.Linux --room <code>");
			Console.Error.WriteLine("Room code must be 4-8 alphanumeric characters.");
			return 1;
		}

		Console.WriteLine($"Joining room: {room_code}");

		// Overlay requires a running X11 display — guard at runtime
		if (!OperatingSystem.IsLinux())
		{
			Console.Error.WriteLine("X11 overlay is only supported on Linux.");
			return 1;
		}

		using var overlay = new X11Overlay();
		Console.CancelKeyPress += (_, e) =>
		{
			e.Cancel = true;
			overlay.Shutdown();
		};

		overlay.OnRender = (display, window, gc, w, h) =>
		{
			// TODO: draw remote cursors from WebSocket state
		};

		overlay.Init();
		Console.WriteLine($"Overlay running ({overlay.ScreenWidth}x{overlay.ScreenHeight}). Press Ctrl+C to exit.");
		overlay.Run();

		Console.WriteLine("Shutdown complete.");
		return 0;
	}

	/// <summary>
	/// Extract room code from either tbm://room/XXXX URI or --room XXXX args.
	/// Validates that the code is 4-8 alphanumeric characters.
	/// </summary>
	private static string? ParseRoomCode(string[] args)
	{
		var code_pattern = new Regex(@"^[a-zA-Z0-9]{4,8}$");

		for (var i = 0; i < args.Length; i++)
		{
			// tbm://room/<code> protocol URI
			var uri_match = Regex.Match(args[i], @"^tbm://room/([a-zA-Z0-9]{4,8})$");
			if (uri_match.Success)
				return uri_match.Groups[1].Value;

			// --room <code>
			if (args[i] == "--room" && i + 1 < args.Length)
			{
				var candidate = args[i + 1];
				if (code_pattern.IsMatch(candidate))
					return candidate;
			}
		}

		return null;
	}
}
