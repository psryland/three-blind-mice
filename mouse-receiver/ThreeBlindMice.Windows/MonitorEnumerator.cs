using System.Runtime.InteropServices;

namespace ThreeBlindMice.Windows;

/// <summary>
/// Enumerates all attached monitors using EnumDisplayMonitors P/Invoke.
/// </summary>
internal static class MonitorEnumerator
{
	/// <summary>
	/// Information about a single display monitor.
	/// </summary>
	internal readonly record struct MonitorInfo(
		int Index,
		string Device,
		int Left,
		int Top,
		int Width,
		int Height,
		bool Is_Primary
	);

	/// <summary>
	/// Returns a list of all monitors in the current display configuration.
	/// The primary monitor is always listed first (index 0).
	/// </summary>
	public static List<MonitorInfo> Enumerate()
	{
		var monitors = new List<MonitorInfo>();

		EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (h_monitor, hdc, lprc_monitor, data) =>
		{
			var info = new MONITORINFOEX();
			info.cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>();

			if (GetMonitorInfo(h_monitor, ref info))
			{
				var bounds = info.rcMonitor;
				var is_primary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0;
				var device = info.szDevice ?? "";

				monitors.Add(new MonitorInfo(
					Index: 0, // assigned after sorting
					Device: device,
					Left: bounds.left,
					Top: bounds.top,
					Width: bounds.right - bounds.left,
					Height: bounds.bottom - bounds.top,
					Is_Primary: is_primary
				));
			}

			return true; // continue enumeration
		}, IntPtr.Zero);

		// Sort so primary is first, then by left position for predictable ordering
		monitors.Sort((a, b) =>
		{
			if (a.Is_Primary != b.Is_Primary)
				return a.Is_Primary ? -1 : 1;
			var cmp = a.Left.CompareTo(b.Left);
			return cmp != 0 ? cmp : a.Top.CompareTo(b.Top);
		});

		// Reassign indices after sorting
		for (var i = 0; i < monitors.Count; i++)
			monitors[i] = monitors[i] with { Index = i };

		return monitors;
	}

	#region P/Invoke

	private const uint MONITORINFOF_PRIMARY = 0x00000001;

	private delegate bool MonitorEnumProc(IntPtr h_monitor, IntPtr hdc_monitor, IntPtr lprc_monitor, IntPtr data);

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int left;
		public int top;
		public int right;
		public int bottom;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct MONITORINFOEX
	{
		public uint cbSize;
		public RECT rcMonitor;
		public RECT rcWork;
		public uint dwFlags;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string szDevice;
	}

	[DllImport("user32.dll")]
	private static extern bool EnumDisplayMonitors(
		IntPtr hdc, IntPtr lprc_clip,
		MonitorEnumProc lpfn_enum, IntPtr data);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool GetMonitorInfo(IntPtr h_monitor, ref MONITORINFOEX lpmi);

	#endregion
}
