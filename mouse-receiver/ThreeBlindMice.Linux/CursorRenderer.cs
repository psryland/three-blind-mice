using ThreeBlindMice.Core;
using static ThreeBlindMice.Linux.X11Native;

namespace ThreeBlindMice.Linux;

/// <summary>
/// Draws coloured arrow cursors with name labels on the X11 overlay window.
/// Each remote user's cursor is rendered as an arrow polygon with a text label.
/// </summary>
internal static class CursorRenderer
{
	// Arrow cursor shape (~20x30px), defined as offsets from the tip
	private static readonly (int dx, int dy)[] ArrowShape =
	[
		(0, 0),
		(0, 26),
		(6, 20),
		(12, 30),
		(16, 28),
		(10, 18),
		(18, 18),
	];

	/// <summary>
	/// Render all active cursors onto the overlay window.
	/// Normalised [0,1] coordinates are mapped to screen pixel coordinates.
	/// </summary>
	public static void Render(IntPtr display, IntPtr window, IntPtr gc, CursorState state, int screen_width, int screen_height)
	{
		// Remove cursors that haven't sent updates recently
		state.Remove_Inactive();

		foreach (var kvp in state.Cursors)
		{
			var cursor = kvp.Value;

			// Map normalised [0,1] to screen pixels
			var px = (int)(cursor.X * screen_width);
			var py = (int)(cursor.Y * screen_height);

			// Parse the user's colour and set it as the foreground
			var colour = Parse_Hex_Colour(cursor.Colour);
			XSetForeground(display, gc, colour);

			// Build the arrow polygon at the cursor position
			var points = new XPoint[ArrowShape.Length];
			for (var i = 0; i < ArrowShape.Length; i++)
			{
				points[i] = new XPoint(
					(short)(px + ArrowShape[i].dx),
					(short)(py + ArrowShape[i].dy));
			}

			XFillPolygon(display, window, gc, points, points.Length, Convex, CoordModeOrigin);

			// Draw the user's name to the right of the arrow
			var label = cursor.Name;
			if (!string.IsNullOrEmpty(label))
			{
				var label_x = px + 20;
				var label_y = py + 14;
				XDrawString(display, window, gc, label_x, label_y, label, label.Length);
			}
		}
	}

	/// <summary>
	/// Parse a "#RRGGBB" hex colour string into an X11 pixel value (0xAARRGGBB with full alpha).
	/// </summary>
	private static ulong Parse_Hex_Colour(string hex)
	{
		if (hex.Length != 7 || hex[0] != '#')
			return 0xFFFFFFFF; // Default to white on invalid input

		var r = Convert.ToByte(hex.Substring(1, 2), 16);
		var g = Convert.ToByte(hex.Substring(3, 2), 16);
		var b = Convert.ToByte(hex.Substring(5, 2), 16);

		// X11 32-bit ARGB pixel: full alpha + RGB
		return 0xFF000000UL | ((ulong)r << 16) | ((ulong)g << 8) | b;
	}
}
