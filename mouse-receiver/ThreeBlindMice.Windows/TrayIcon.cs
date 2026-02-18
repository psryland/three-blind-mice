using System.Runtime.InteropServices;

namespace ThreeBlindMice.Windows;

/// <summary>
/// Manages a system tray icon with a context menu using Win32 P/Invoke.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
	private const uint WM_APP_TRAY = 0x8002;
	private const uint WM_RBUTTONUP = 0x0205;
	private const uint WM_COMMAND = 0x0111;
	private const uint WM_DESTROY = 0x0002;
	private const uint WM_CLOSE = 0x0010;

	private const uint NIM_ADD = 0x00000000;
	private const uint NIM_MODIFY = 0x00000001;
	private const uint NIM_DELETE = 0x00000002;
	private const uint NIF_MESSAGE = 0x00000001;
	private const uint NIF_ICON = 0x00000002;
	private const uint NIF_TIP = 0x00000004;

	private const uint MF_STRING = 0x00000000;
	private const uint MF_GRAYED = 0x00000001;
	private const uint MF_SEPARATOR = 0x00000800;
	private const uint TPM_BOTTOMALIGN = 0x0020;
	private const uint TPM_LEFTALIGN = 0x0000;

	private const int IDM_QUIT = 1001;
	private const int IDM_ROOM = 1002;
	private const uint IDI_APPLICATION = 32512;

	private IntPtr m_hwnd;
	private IntPtr m_icon;
	private Thread? m_thread;
	private volatile bool m_disposed;
	private string m_room_code = "";
	private readonly Action m_on_quit;
	private readonly WndProcDelegate m_wnd_proc_delegate;

	private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

	public TrayIcon(Action on_quit)
	{
		m_on_quit = on_quit;
		m_wnd_proc_delegate = WndProc;
	}

	/// <summary>
	/// Creates the tray icon on a dedicated STA thread.
	/// </summary>
	public void Show(string room_code)
	{
		m_room_code = room_code;
		var ready = new ManualResetEventSlim(false);

		m_thread = new Thread(() =>
		{
			CreateMessageWindow();
			AddTrayIcon();
			ready.Set();
			RunMessagePump();
		})
		{
			Name = "TrayIcon",
			IsBackground = true,
		};
		m_thread.SetApartmentState(ApartmentState.STA);
		m_thread.Start();
		ready.Wait();
	}

	/// <summary>
	/// Updates the tooltip to reflect a new room code.
	/// </summary>
	public void Update_Room(string code)
	{
		m_room_code = code;
		if (m_hwnd == IntPtr.Zero)
			return;

		var nid = MakeNotifyIconData();
		nid.uFlags = NIF_TIP;
		nid.szTip = $"Three Blind Mice - Room: {code}";
		Shell_NotifyIcon(NIM_MODIFY, ref nid);
	}

	/// <summary>
	/// Removes the tray icon and shuts down the message pump.
	/// </summary>
	public void Shutdown()
	{
		if (m_hwnd != IntPtr.Zero)
		{
			var nid = MakeNotifyIconData();
			Shell_NotifyIcon(NIM_DELETE, ref nid);
			PostMessage(m_hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
		}
		m_thread?.Join(TimeSpan.FromSeconds(3));
	}

	public void Dispose()
	{
		if (m_disposed)
			return;

		m_disposed = true;
		Shutdown();
	}

	private void CreateMessageWindow()
	{
		var wc = new WNDCLASSEX
		{
			cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
			lpfnWndProc = Marshal.GetFunctionPointerForDelegate(m_wnd_proc_delegate),
			hInstance = GetModuleHandle(null),
			lpszClassName = "ThreeBlindMiceTray",
		};

		RegisterClassEx(ref wc);

		m_hwnd = CreateWindowEx(
			0, wc.lpszClassName, "", 0,
			0, 0, 0, 0,
			IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);

		m_icon = LoadIcon(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
	}

	private void AddTrayIcon()
	{
		var nid = MakeNotifyIconData();
		nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
		nid.uCallbackMessage = WM_APP_TRAY;
		nid.hIcon = m_icon;
		nid.szTip = $"Three Blind Mice - Room: {m_room_code}";
		Shell_NotifyIcon(NIM_ADD, ref nid);
	}

	private NOTIFYICONDATA MakeNotifyIconData()
	{
		return new NOTIFYICONDATA
		{
			cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
			hWnd = m_hwnd,
			uID = 1,
		};
	}

	private void ShowContextMenu()
	{
		var menu = CreatePopupMenu();
		InsertMenu(menu, 0, MF_STRING | MF_GRAYED, (IntPtr)IDM_ROOM, $"Room: {m_room_code}");
		InsertMenu(menu, 1, MF_SEPARATOR, IntPtr.Zero, null);
		InsertMenu(menu, 2, MF_STRING, (IntPtr)IDM_QUIT, "Quit");

		// Required so the menu dismisses when the user clicks elsewhere
		GetCursorPos(out var pt);
		SetForegroundWindow(m_hwnd);
		TrackPopupMenu(menu, TPM_LEFTALIGN | TPM_BOTTOMALIGN, pt.x, pt.y, 0, m_hwnd, IntPtr.Zero);
		DestroyMenu(menu);
	}

	private static void RunMessagePump()
	{
		while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
		{
			TranslateMessage(ref msg);
			DispatchMessage(ref msg);
		}
	}

	private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr w_param, IntPtr l_param)
	{
		switch (msg)
		{
			case WM_APP_TRAY:
				if ((uint)l_param == WM_RBUTTONUP)
					ShowContextMenu();
				return IntPtr.Zero;

			case WM_COMMAND:
				var cmd_id = (int)(w_param.ToInt64() & 0xFFFF);
				if (cmd_id == IDM_QUIT)
					m_on_quit();
				return IntPtr.Zero;

			case WM_CLOSE:
				DestroyWindow(hwnd);
				return IntPtr.Zero;

			case WM_DESTROY:
				PostQuitMessage(0);
				return IntPtr.Zero;

			default:
				return DefWindowProc(hwnd, msg, w_param, l_param);
		}
	}

	#region P/Invoke

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct NOTIFYICONDATA
	{
		public uint cbSize;
		public IntPtr hWnd;
		public uint uID;
		public uint uFlags;
		public uint uCallbackMessage;
		public IntPtr hIcon;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string szTip;
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
		[MarshalAs(UnmanagedType.LPWStr)]
		public string lpszMenuName;
		[MarshalAs(UnmanagedType.LPWStr)]
		public string lpszClassName;
		public IntPtr hIconSm;
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
	private struct POINT
	{
		public int x;
		public int y;
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

	[DllImport("user32.dll")]
	private static extern IntPtr CreatePopupMenu();

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, IntPtr uIDNewItem, string? lpNewItem);

	[DllImport("user32.dll")]
	private static extern bool TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

	[DllImport("user32.dll")]
	private static extern bool DestroyMenu(IntPtr hMenu);

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out POINT lpPoint);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern IntPtr CreateWindowEx(
		uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
		int x, int y, int nWidth, int nHeight,
		IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

	[DllImport("user32.dll")]
	private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

	[DllImport("user32.dll")]
	private static extern bool TranslateMessage(ref MSG lpMsg);

	[DllImport("user32.dll")]
	private static extern IntPtr DispatchMessage(ref MSG lpMsg);

	[DllImport("user32.dll")]
	private static extern IntPtr DefWindowProc(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern void PostQuitMessage(int nExitCode);

	[DllImport("user32.dll")]
	private static extern bool DestroyWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr GetModuleHandle(string? lpModuleName);

	#endregion
}
