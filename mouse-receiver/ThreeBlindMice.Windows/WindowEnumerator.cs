using System.Runtime.InteropServices;
using System.Text;

namespace ThreeBlindMice.Windows;

/// <summary>
/// Enumerates visible top-level windows and determines which monitor each is on.
/// </summary>
internal static class WindowEnumerator
{
	internal record WindowInfo(string Title, int Left, int Top, int Width, int Height, int Monitor_Index);

	private static readonly HashSet<string> s_excluded_titles = new(StringComparer.OrdinalIgnoreCase)
	{
		"Program Manager",
		"MSCTFIME UI",
		"Default IME",
	};

	public static List<WindowInfo> Enumerate(List<MonitorEnumerator.MonitorInfo> monitors)
	{
		var windows = new List<WindowInfo>();

		EnumWindows((hwnd, _) =>
		{
			if (!IsWindowVisible(hwnd))
				return true;

			if (IsIconic(hwnd))
				return true;

			var title_length = GetWindowTextLength(hwnd);
			if (title_length <= 0)
				return true;

			var sb = new StringBuilder(title_length + 1);
			GetWindowText(hwnd, sb, sb.Capacity);
			var title = sb.ToString();

			if (string.IsNullOrEmpty(title) || s_excluded_titles.Contains(title))
				return true;

			if (!GetWindowRect(hwnd, out var rect))
				return true;

			var w = rect.right - rect.left;
			var h = rect.bottom - rect.top;

			if (w <= 0 || h <= 0)
				return true;

			var center_x = rect.left + w / 2;
			var center_y = rect.top + h / 2;
			var monitor_index = Find_Monitor_Index(monitors, center_x, center_y);

			windows.Add(new WindowInfo(title, rect.left, rect.top, w, h, monitor_index));
			return true;
		}, IntPtr.Zero);

		return windows;
	}

	/// <summary>
	/// Finds which monitor contains the given point.
	/// Falls back to monitor 0 if the point is outside all monitor bounds.
	/// </summary>
	private static int Find_Monitor_Index(List<MonitorEnumerator.MonitorInfo> monitors, int x, int y)
	{
		for (var i = 0; i < monitors.Count; i++)
		{
			var m = monitors[i];
			if (x >= m.Left && x < m.Left + m.Width && y >= m.Top && y < m.Top + m.Height)
				return m.Index;
		}
		return 0;
	}

	#region P/Invoke

	private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr l_param);

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int left;
		public int top;
		public int right;
		public int bottom;
	}

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool IsIconic(IntPtr hWnd);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowTextLength(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

	#endregion
}
