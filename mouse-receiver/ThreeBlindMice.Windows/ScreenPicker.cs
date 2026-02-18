using System.Runtime.InteropServices;
using System.Text;

namespace ThreeBlindMice.Windows;

/// <summary>
/// Interactive screen pickers for window selection and rectangle region selection.
/// Window picker: user clicks on a window to select it (polling-based).
/// Rectangle picker: user drags on a full-screen overlay to select a region.
/// </summary>
internal static class ScreenPicker
{
	public record PickedWindow(string Title, IntPtr Hwnd, int Left, int Top, int Width, int Height, int Monitor_Index);
	public record PickedRectangle(int Left, int Top, int Width, int Height);

	/// <summary>
	/// Waits for the user to click on a window, then identifies it via WindowFromPoint.
	/// Press ESC to cancel. Times out after 30 seconds.
	/// </summary>
	public static async Task<PickedWindow?> Pick_Window_Async(
		List<MonitorEnumerator.MonitorInfo> monitors,
		CancellationToken cancel = default)
	{
		Console.WriteLine("Window picker active — click on a window to select it (ESC to cancel)...");

		// Wait for any currently-held button to be released
		while (!cancel.IsCancellationRequested && (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0)
			await Task.Delay(50, cancel);

		// Brief delay so the triggering click doesn't get captured
		await Task.Delay(300, cancel);

		var deadline = Environment.TickCount64 + 30_000;

		while (!cancel.IsCancellationRequested && Environment.TickCount64 < deadline)
		{
			if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
			{
				Console.WriteLine("Window picker cancelled.");
				return null;
			}

			if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0)
			{
				GetCursorPos(out var point);
				var hwnd = WindowFromPoint(point);

				// Walk up to the top-level parent window
				IntPtr parent;
				while ((parent = GetParent(hwnd)) != IntPtr.Zero)
					hwnd = parent;

				var sb = new StringBuilder(256);
				GetWindowText(hwnd, sb, sb.Capacity);
				GetWindowRect(hwnd, out var rect);

				var title = sb.ToString();
				if (string.IsNullOrEmpty(title))
				{
					Console.WriteLine("Window picker: no identifiable window at cursor.");
					return null;
				}

				var w = rect.Right - rect.Left;
				var h = rect.Bottom - rect.Top;
				var cx = rect.Left + w / 2;
				var cy = rect.Top + h / 2;
				var monitor_index = Find_Monitor_Index(monitors, cx, cy);

				Console.WriteLine($"Window picker: selected '{title}' ({w}x{h})");
				return new PickedWindow(title, hwnd, rect.Left, rect.Top, w, h, monitor_index);
			}

			await Task.Delay(30, cancel);
		}

		Console.WriteLine("Window picker timed out.");
		return null;
	}

	/// <summary>
	/// Shows a full-screen semi-transparent overlay. User clicks and drags to select
	/// a rectangular region. ESC cancels.
	/// </summary>
	public static Task<PickedRectangle?> Pick_Rectangle_Async(CancellationToken cancel = default)
	{
		var tcs = new TaskCompletionSource<PickedRectangle?>();
		var thread = new Thread(() =>
		{
			try
			{
				var result = Run_Rectangle_Picker(cancel);
				tcs.TrySetResult(result);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"Rectangle picker error: {ex.Message}");
				tcs.TrySetResult(null);
			}
		});
		thread.SetApartmentState(ApartmentState.STA);
		thread.IsBackground = true;
		thread.Start();
		return tcs.Task;
	}

	#region Rectangle picker implementation

	// Shared state for the rectangle picker's WndProc
	private static bool s_dragging;
	private static POINT s_start_pt;
	private static POINT s_end_pt;
	private static PickedRectangle? s_result;
	private static int s_vx, s_vy;

	// Must be stored as a field to prevent the delegate from being GC'd
	private static WndProc? s_rect_wnd_proc;

	private static PickedRectangle? Run_Rectangle_Picker(CancellationToken cancel)
	{
		s_vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
		s_vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
		var vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
		var vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);

		s_dragging = false;
		s_result = null;

		Console.WriteLine("Rectangle picker active — drag to select a region (ESC to cancel)...");

		s_rect_wnd_proc = RectPickerWndProc;
		var class_name = $"TBMRectPicker_{Environment.CurrentManagedThreadId}";
		var wc = new WNDCLASSEX
		{
			cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
			lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_rect_wnd_proc),
			hInstance = GetModuleHandle(null),
			lpszClassName = class_name,
			hCursor = LoadCursor(IntPtr.Zero, IDC_CROSS),
		};
		RegisterClassEx(ref wc);

		var hwnd = CreateWindowEx(
			WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
			class_name, "",
			WS_POPUP | WS_VISIBLE,
			s_vx, s_vy, vw, vh,
			IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);

		// Combine alpha dimming + magenta color key:
		// non-magenta pixels are rendered at ~30% opacity (dim overlay)
		// magenta pixels are fully transparent (selection cutout)
		SetLayeredWindowAttributes(hwnd, COLORREF_MAGENTA, 80, LWA_ALPHA | LWA_COLORKEY);

		ShowWindow(hwnd, SW_SHOW);
		UpdateWindow(hwnd);
		SetForegroundWindow(hwnd);

		while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
		{
			if (cancel.IsCancellationRequested)
			{
				DestroyWindow(hwnd);
				break;
			}
			TranslateMessage(ref msg);
			DispatchMessage(ref msg);
		}

		UnregisterClass(class_name, GetModuleHandle(null));

		Console.WriteLine(s_result != null
			? $"Rectangle picker: selected ({s_result.Left},{s_result.Top}) {s_result.Width}x{s_result.Height}"
			: "Rectangle picker cancelled.");

		return s_result;
	}

	private static IntPtr RectPickerWndProc(IntPtr hwnd, uint msg, IntPtr wp, IntPtr lp)
	{
		switch (msg)
		{
			case WM_LBUTTONDOWN:
			{
				s_dragging = true;
				var cx = (short)(lp.ToInt64() & 0xFFFF);
				var cy = (short)((lp.ToInt64() >> 16) & 0xFFFF);
				s_start_pt = new POINT(cx + s_vx, cy + s_vy);
				s_end_pt = s_start_pt;
				SetCapture(hwnd);
				return IntPtr.Zero;
			}

			case WM_MOUSEMOVE:
			{
				if (s_dragging)
				{
					var cx = (short)(lp.ToInt64() & 0xFFFF);
					var cy = (short)((lp.ToInt64() >> 16) & 0xFFFF);
					s_end_pt = new POINT(cx + s_vx, cy + s_vy);
					InvalidateRect(hwnd, IntPtr.Zero, true);
				}
				return IntPtr.Zero;
			}

			case WM_LBUTTONUP:
			{
				if (s_dragging)
				{
					s_dragging = false;
					ReleaseCapture();

					var cx = (short)(lp.ToInt64() & 0xFFFF);
					var cy = (short)((lp.ToInt64() >> 16) & 0xFFFF);
					s_end_pt = new POINT(cx + s_vx, cy + s_vy);

					var left = Math.Min(s_start_pt.X, s_end_pt.X);
					var top = Math.Min(s_start_pt.Y, s_end_pt.Y);
					var right = Math.Max(s_start_pt.X, s_end_pt.X);
					var bottom = Math.Max(s_start_pt.Y, s_end_pt.Y);
					var w = right - left;
					var h = bottom - top;

					// Minimum size to avoid accidental clicks
					if (w > 10 && h > 10)
						s_result = new PickedRectangle(left, top, w, h);

					PostQuitMessage(0);
				}
				return IntPtr.Zero;
			}

			case WM_KEYDOWN:
			{
				if ((int)wp == VK_ESCAPE)
					PostQuitMessage(0);
				return IntPtr.Zero;
			}

			case WM_PAINT:
			{
				var ps = new PAINTSTRUCT();
				var hdc = BeginPaint(hwnd, ref ps);

				GetClientRect(hwnd, out var client);

				// Fill with black (dimming overlay, rendered at ~30% opacity via LWA_ALPHA)
				var black_brush = CreateSolidBrush(0x00000000);
				FillRect(hdc, ref client, black_brush);
				DeleteObject(black_brush);

				if (s_dragging)
				{
					// Convert screen coords to client coords
					var sl = Math.Min(s_start_pt.X, s_end_pt.X) - s_vx;
					var st = Math.Min(s_start_pt.Y, s_end_pt.Y) - s_vy;
					var sr = Math.Max(s_start_pt.X, s_end_pt.X) - s_vx;
					var sb = Math.Max(s_start_pt.Y, s_end_pt.Y) - s_vy;

					// Fill selection area with magenta (fully transparent via color key)
					// so the user can see the desktop clearly through the selection
					var magenta_brush = CreateSolidBrush(COLORREF_MAGENTA);
					var sel_rect = new RECT { Left = sl, Top = st, Right = sr, Bottom = sb };
					FillRect(hdc, ref sel_rect, magenta_brush);
					DeleteObject(magenta_brush);

					// Draw a visible border around the selection (outside the magenta area)
					var pen = CreatePen(PS_SOLID, 3, 0x00F48442); // Blue (BGR)
					var old_pen = SelectObject(hdc, pen);
					var null_brush = GetStockObject(NULL_BRUSH);
					var old_brush = SelectObject(hdc, null_brush);
					NativeRectangle(hdc, sl - 2, st - 2, sr + 2, sb + 2);
					SelectObject(hdc, old_pen);
					SelectObject(hdc, old_brush);
					DeleteObject(pen);
				}

				EndPaint(hwnd, ref ps);
				return IntPtr.Zero;
			}

			case WM_SETCURSOR:
			{
				SetCursor(LoadCursor(IntPtr.Zero, IDC_CROSS));
				return (IntPtr)1;
			}

			case WM_DESTROY:
			{
				PostQuitMessage(0);
				return IntPtr.Zero;
			}

			default:
				return DefWindowProc(hwnd, msg, wp, lp);
		}
	}

	#endregion

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

	private const int VK_LBUTTON = 0x01;
	private const int VK_ESCAPE = 0x1B;

	private const int SM_XVIRTUALSCREEN = 76;
	private const int SM_YVIRTUALSCREEN = 77;
	private const int SM_CXVIRTUALSCREEN = 78;
	private const int SM_CYVIRTUALSCREEN = 79;

	private const uint WS_POPUP = 0x80000000;
	private const uint WS_VISIBLE = 0x10000000;
	private const int WS_EX_LAYERED = 0x80000;
	private const int WS_EX_TOPMOST = 0x8;
	private const int WS_EX_TOOLWINDOW = 0x80;

	private const uint LWA_COLORKEY = 0x01;
	private const uint LWA_ALPHA = 0x02;
	private const int COLORREF_MAGENTA = 0x00FF00FF; // BGR

	private const int SW_SHOW = 5;
	private const int PS_SOLID = 0;
	private const int NULL_BRUSH = 5;

	private const uint WM_LBUTTONDOWN = 0x0201;
	private const uint WM_LBUTTONUP = 0x0202;
	private const uint WM_MOUSEMOVE = 0x0200;
	private const uint WM_KEYDOWN = 0x0100;
	private const uint WM_PAINT = 0x000F;
	private const uint WM_SETCURSOR = 0x0020;
	private const uint WM_DESTROY = 0x0002;

	private static readonly IntPtr IDC_CROSS = (IntPtr)32515;

	private delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT
	{
		public int X;
		public int Y;
		public POINT(int x, int y) { X = x; Y = y; }
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MSG
	{
		public IntPtr hwnd;
		public uint message;
		public IntPtr wParam;
		public IntPtr lParam;
		public uint time;
		public POINT pt;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct PAINTSTRUCT
	{
		public IntPtr hdc;
		public bool fErase;
		public RECT rcPaint;
		public bool fRestore;
		public bool fIncUpdate;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public byte[] rgbReserved;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct WNDCLASSEX
	{
		public uint cbSize;
		public uint style;
		public IntPtr lpfnWndProc;
		public int cbClsExtra;
		public int cbWndExtra;
		public IntPtr hInstance;
		public IntPtr hIcon;
		public IntPtr hCursor;
		public IntPtr hbrBackground;
		public string lpszMenuName;
		public string lpszClassName;
		public IntPtr hIconSm;
	}

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int vKey);

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out POINT lpPoint);

	[DllImport("user32.dll")]
	private static extern IntPtr WindowFromPoint(POINT point);

	[DllImport("user32.dll")]
	private static extern IntPtr GetParent(IntPtr hWnd);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	private static extern int GetSystemMetrics(int nIndex);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr GetModuleHandle(string? lpModuleName);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr CreateWindowEx(
		int dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
		int x, int y, int nWidth, int nHeight,
		IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

	[DllImport("user32.dll")]
	private static extern bool DestroyWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern bool UpdateWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, int crKey, byte bAlpha, uint dwFlags);

	[DllImport("user32.dll")]
	private static extern IntPtr SetCapture(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

	[DllImport("user32.dll")]
	private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	private static extern IntPtr BeginPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

	[DllImport("user32.dll")]
	private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

	[DllImport("user32.dll")]
	private static extern int FillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateSolidBrush(int crColor);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreatePen(int fnPenStyle, int nWidth, int crColor);

	[DllImport("gdi32.dll")]
	private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

	[DllImport("gdi32.dll")]
	private static extern IntPtr GetStockObject(int fnObject);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(IntPtr hObject);

	[DllImport("gdi32.dll", EntryPoint = "Rectangle")]
	private static extern bool NativeRectangle(IntPtr hdc, int left, int top, int right, int bottom);

	[DllImport("user32.dll")]
	private static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

	[DllImport("user32.dll")]
	private static extern IntPtr SetCursor(IntPtr hCursor);

	[DllImport("user32.dll")]
	private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

	[DllImport("user32.dll")]
	private static extern bool TranslateMessage(ref MSG lpMsg);

	[DllImport("user32.dll")]
	private static extern IntPtr DispatchMessage(ref MSG lpMsg);

	[DllImport("user32.dll")]
	private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern void PostQuitMessage(int nExitCode);

	#endregion
}
