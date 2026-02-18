using System.Runtime.InteropServices;
using static ThreeBlindMice.Linux.X11Native;

namespace ThreeBlindMice.Linux;

/// <summary>
/// Transparent, always-on-top, click-through overlay window using X11.
/// Covers the full screen and allows rendering of remote mouse cursors.
/// </summary>
internal sealed class X11Overlay : IDisposable
{
	private IntPtr m_display;
	private IntPtr m_window;
	private IntPtr m_gc;
	private int m_screen;
	private int m_width;
	private int m_height;
	private volatile bool m_running;
	private bool m_disposed;

	/// <summary>
	/// Callback invoked each frame to draw cursor overlays.
	/// Parameters: display, window, gc, screen_width, screen_height.
	/// </summary>
	public Action<IntPtr, IntPtr, IntPtr, int, int>? OnRender { get; set; }

	public int ScreenWidth => m_width;
	public int ScreenHeight => m_height;

	/// <summary>
	/// Initialise the X11 display, create the overlay window, and configure it.
	/// </summary>
	public void Init()
	{
		m_display = XOpenDisplay(null);
		if (m_display == IntPtr.Zero)
			throw new InvalidOperationException("Failed to open X11 display. Is DISPLAY set?");

		m_screen = XDefaultScreen(m_display);
		m_width = XDisplayWidth(m_display, m_screen);
		m_height = XDisplayHeight(m_display, m_screen);
		var root = XRootWindow(m_display, m_screen);

		// Find a 32-bit TrueColor visual for ARGB transparency
		if (XMatchVisualInfo(m_display, m_screen, 32, TrueColor, out var visual_info) == 0)
			throw new InvalidOperationException("No 32-bit TrueColor visual available.");

		var colormap = XCreateColormap(m_display, root, visual_info.visual, AllocNone);

		var attrs = new XSetWindowAttributes
		{
			override_redirect = 1,
			colormap = colormap,
			background_pixel = 0,
			border_pixel = 0,
			event_mask = ExposureMask | StructureNotifyMask | VisibilityChangeMask,
		};

		var mask = CWOverrideRedirect | CWColormap | CWBackPixel | CWBorderPixel | CWEventMask;

		m_window = XCreateWindow(
			m_display, root,
			0, 0, (uint)m_width, (uint)m_height,
			0,
			visual_info.depth,
			InputOutput,
			visual_info.visual,
			mask,
			ref attrs);

		if (m_window == IntPtr.Zero)
			throw new InvalidOperationException("Failed to create X11 overlay window.");

		SetWindowProperties();
		SetClickThrough();

		m_gc = XCreateGC(m_display, m_window, 0, IntPtr.Zero);

		XMapWindow(m_display, m_window);
		XFlush(m_display);
	}

	/// <summary>
	/// Process X11 events and invoke the render callback. Blocks until Shutdown() is called.
	/// </summary>
	public void Run()
	{
		m_running = true;

		// Allocate a buffer for XEvent (256 bytes covers all X11 event structs)
		var event_buffer = Marshal.AllocHGlobal(256);
		try
		{
			while (m_running)
			{
				// Drain all pending events
				while (XPending(m_display) > 0)
				{
					XNextEvent(m_display, event_buffer);
					// We don't need to handle specific events — just drain the queue
				}

				// Clear and redraw
				XClearWindow(m_display, m_window);
				OnRender?.Invoke(m_display, m_window, m_gc, m_width, m_height);
				XFlush(m_display);

				// ~60fps throttle
				Thread.Sleep(16);
			}
		}
		finally
		{
			Marshal.FreeHGlobal(event_buffer);
		}
	}

	/// <summary>
	/// Signal the event loop to stop.
	/// </summary>
	public void Shutdown()
	{
		m_running = false;
	}

	public void Dispose()
	{
		if (m_disposed) return;
		m_disposed = true;

		if (m_gc != IntPtr.Zero)
		{
			XFreeGC(m_display, m_gc);
			m_gc = IntPtr.Zero;
		}

		if (m_window != IntPtr.Zero)
		{
			XDestroyWindow(m_display, m_window);
			m_window = IntPtr.Zero;
		}

		if (m_display != IntPtr.Zero)
		{
			XCloseDisplay(m_display);
			m_display = IntPtr.Zero;
		}
	}

	/// <summary>
	/// Set EWMH properties for always-on-top and dock (skip-taskbar) behaviour.
	/// </summary>
	private void SetWindowProperties()
	{
		var wm_state = XInternAtom(m_display, "_NET_WM_STATE", false);
		var wm_state_above = XInternAtom(m_display, "_NET_WM_STATE_ABOVE", false);
		var wm_window_type = XInternAtom(m_display, "_NET_WM_WINDOW_TYPE", false);
		var wm_window_type_dock = XInternAtom(m_display, "_NET_WM_WINDOW_TYPE_DOCK", false);

		// Always on top
		var above_data = Marshal.AllocHGlobal(IntPtr.Size);
		try
		{
			Marshal.WriteIntPtr(above_data, wm_state_above);
			XChangeProperty(m_display, m_window, wm_state, XA_ATOM, 32, PropModeReplace, above_data, 1);
		}
		finally
		{
			Marshal.FreeHGlobal(above_data);
		}

		// Dock type — skip taskbar/pager
		var dock_data = Marshal.AllocHGlobal(IntPtr.Size);
		try
		{
			Marshal.WriteIntPtr(dock_data, wm_window_type_dock);
			XChangeProperty(m_display, m_window, wm_window_type, XA_ATOM, 32, PropModeReplace, dock_data, 1);
		}
		finally
		{
			Marshal.FreeHGlobal(dock_data);
		}
	}

	/// <summary>
	/// Use XShape to create an empty input region — all clicks pass through the overlay.
	/// </summary>
	private void SetClickThrough()
	{
		// Setting an empty rectangle array with ShapeSet makes the entire input region empty
		XShapeCombineRectangles(
			m_display,
			m_window,
			ShapeInput,
			0, 0,
			null,
			0,
			ShapeSet,
			Unsorted);
	}
}
