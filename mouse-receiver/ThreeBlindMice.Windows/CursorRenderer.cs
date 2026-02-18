using System.Runtime.InteropServices;
using ThreeBlindMice.Core;

namespace ThreeBlindMice.Windows;

/// <summary>
/// Renders coloured arrow cursors with name labels on the overlay window using GDI P/Invoke.
/// </summary>
internal sealed class CursorRenderer : IDisposable
{
	private const uint COLOR_KEY = 0x00FF00FF; // magenta COLORREF (0x00BBGGRR)
	private const int TRANSPARENT = 1;

	// Arrow polygon points relative to cursor tip at (0,0), roughly 20×30 pixels
	private static readonly int[] s_arrow_dx = { 0,  0,  6,  9, 13,  9, 15 };
	private static readonly int[] s_arrow_dy = { 0, 21, 17, 26, 24, 16, 16 };
	private const int ARROW_POINT_COUNT = 7;

	private IntPtr m_font;

	public CursorRenderer()
	{
		m_font = CreateFont(
			14, 0, 0, 0,
			400, // FW_NORMAL
			0, 0, 0,
			1, // DEFAULT_CHARSET
			0, 0, 0, 0,
			"Segoe UI");
	}

	/// <summary>
	/// Draws all active cursors onto the given device context.
	/// Non-cursor areas are filled with the colour key so they appear transparent.
	/// </summary>
	public void Render(IntPtr hdc, CursorState state, int screen_width, int screen_height)
	{
		// Fill entire area with colour key for transparency
		var full_rect = new RECT { left = 0, top = 0, right = screen_width, bottom = screen_height };
		var bg_brush = CreateSolidBrush(COLOR_KEY);
		FillRect(hdc, ref full_rect, bg_brush);
		DeleteObject(bg_brush);

		var old_font = SelectObject(hdc, m_font);
		SetBkMode(hdc, TRANSPARENT);

		foreach (var kvp in state.Cursors)
		{
			var cursor = kvp.Value;
			var colour_ref = ParseColour(cursor.Colour);

			// Map normalised [0,1] coordinates to screen pixels
			var cx = (int)(cursor.X * screen_width);
			var cy = (int)(cursor.Y * screen_height);

			// Trail renders behind the cursor arrow
			DrawTrail(hdc, cursor, colour_ref, screen_width, screen_height);
			DrawArrow(hdc, cx, cy, colour_ref);
			DrawLabel(hdc, cx, cy, cursor.Name, colour_ref);
		}

		SelectObject(hdc, old_font);
	}

	private static void DrawTrail(IntPtr hdc, CursorInfo cursor, uint colour_ref, int screen_width, int screen_height)
	{
		var trail = cursor.Trail_Points;
		if (trail.Count < 2)
			return;

		for (var i = 1; i < trail.Count; i++)
		{
			// Newer segments are thicker to simulate fading (no alpha with colour key)
			var age_ratio = (float)i / trail.Count;
			var pen_width = Math.Max(1, (int)(age_ratio * 4));

			var pen = CreatePen(0, pen_width, colour_ref); // PS_SOLID
			var old_pen = SelectObject(hdc, pen);

			var x0 = (int)(trail[i - 1].X * screen_width);
			var y0 = (int)(trail[i - 1].Y * screen_height);
			var x1 = (int)(trail[i].X * screen_width);
			var y1 = (int)(trail[i].Y * screen_height);

			MoveToEx(hdc, x0, y0, IntPtr.Zero);
			LineTo(hdc, x1, y1);

			SelectObject(hdc, old_pen);
			DeleteObject(pen);
		}
	}

	private static void DrawArrow(IntPtr hdc, int cx, int cy, uint colour_ref)
	{
		var pen = CreatePen(0, 1, 0x00000000); // PS_SOLID, black outline
		var brush = CreateSolidBrush(colour_ref);
		var old_pen = SelectObject(hdc, pen);
		var old_brush = SelectObject(hdc, brush);

		var points = new POINT[ARROW_POINT_COUNT];
		for (var i = 0; i < ARROW_POINT_COUNT; i++)
		{
			points[i].x = cx + s_arrow_dx[i];
			points[i].y = cy + s_arrow_dy[i];
		}

		Polygon(hdc, points, ARROW_POINT_COUNT);

		SelectObject(hdc, old_brush);
		SelectObject(hdc, old_pen);
		DeleteObject(brush);
		DeleteObject(pen);
	}

	private static void DrawLabel(IntPtr hdc, int cx, int cy, string name, uint colour_ref)
	{
		if (string.IsNullOrEmpty(name))
			return;

		// Position label to the right of and slightly below the arrow tip
		var label_x = cx + 18;
		var label_y = cy + 4;
		const int padding = 3;

		// Approximate text metrics (width ≈ 7px per char, height ≈ 16px)
		var text_width = name.Length * 7;
		const int text_height = 16;

		// Draw filled background rectangle in the user's colour
		var label_rect = new RECT
		{
			left = label_x - padding,
			top = label_y - padding,
			right = label_x + text_width + padding,
			bottom = label_y + text_height + padding,
		};
		var label_brush = CreateSolidBrush(colour_ref);
		FillRect(hdc, ref label_rect, label_brush);
		DeleteObject(label_brush);

		// Draw the name in white on transparent background
		SetTextColor(hdc, 0x00FFFFFF);
		SetBkMode(hdc, TRANSPARENT);
		TextOut(hdc, label_x, label_y, name, name.Length);
	}

	/// <summary>
	/// Parses a hex colour string "#RRGGBB" to a Win32 COLORREF value (0x00BBGGRR).
	/// </summary>
	public static uint ParseColour(string hex)
	{
		if (hex.Length != 7 || hex[0] != '#')
			return 0x00FFFFFF; // fallback to white

		var r = Convert.ToByte(hex.Substring(1, 2), 16);
		var g = Convert.ToByte(hex.Substring(3, 2), 16);
		var b = Convert.ToByte(hex.Substring(5, 2), 16);
		return (uint)(b << 16 | g << 8 | r);
	}

	public void Dispose()
	{
		if (m_font != IntPtr.Zero)
		{
			DeleteObject(m_font);
			m_font = IntPtr.Zero;
		}
	}

	#region P/Invoke — GDI32 / User32

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT
	{
		public int x;
		public int y;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int left;
		public int top;
		public int right;
		public int bottom;
	}

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreatePen(int iStyle, int cWidth, uint color);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateSolidBrush(uint crColor);

	[DllImport("gdi32.dll")]
	private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

	[DllImport("gdi32.dll")]
	private static extern bool Polygon(IntPtr hdc, POINT[] apt, int cpt);

	[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
	private static extern bool TextOut(IntPtr hdc, int x, int y, string lpString, int c);

	[DllImport("gdi32.dll")]
	private static extern uint SetTextColor(IntPtr hdc, uint color);

	[DllImport("gdi32.dll")]
	private static extern int SetBkMode(IntPtr hdc, int mode);

	[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr CreateFont(
		int cHeight, int cWidth, int cEscapement, int cOrientation,
		int cWeight, uint bItalic, uint bUnderline, uint bStrikeOut,
		uint iCharSet, uint iOutPrecision, uint iClipPrecision,
		uint iQuality, uint iPitchAndFamily, string pszFaceName);

	[DllImport("user32.dll")]
	private static extern int FillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(IntPtr ho);

	[DllImport("gdi32.dll")]
	private static extern bool MoveToEx(IntPtr hdc, int x, int y, IntPtr lppt);

	[DllImport("gdi32.dll")]
	private static extern bool LineTo(IntPtr hdc, int x, int y);

	#endregion
}
