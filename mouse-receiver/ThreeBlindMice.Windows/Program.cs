using ThreeBlindMice.Core;

namespace ThreeBlindMice.Windows;

class Program
{
	static void Main(string[] args)
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

		// Create and start the overlay window
		using var overlay = new Win32Overlay();
		overlay.Start();
		Console.WriteLine("Overlay window created. Press Ctrl+C to exit.");

		// TODO: Connect to Web PubSub and begin receiving cursor updates
		Console.WriteLine($"[Placeholder] Would connect to Web PubSub for room: {room_code}");

		// Handle Ctrl+C for clean shutdown
		var exit = new ManualResetEventSlim(false);
		Console.CancelKeyPress += (_, e) =>
		{
			e.Cancel = true;
			overlay.Shutdown();
			exit.Set();
		};

		exit.Wait();
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
