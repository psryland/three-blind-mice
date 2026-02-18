using System.Runtime.InteropServices;

namespace ThreeBlindMice.Linux;

/// <summary>
/// P/Invoke declarations for libX11 and libXext.
/// These are declarations only — they compile on any platform but will fail at runtime without X11.
/// </summary>
internal static class X11Native
{
	private const string LibX11 = "libX11.so.6";
	private const string LibXext = "libXext.so.6";

	// -- Display management --

	[DllImport(LibX11)]
	public static extern IntPtr XOpenDisplay(string? display);

	[DllImport(LibX11)]
	public static extern int XCloseDisplay(IntPtr display);

	[DllImport(LibX11)]
	public static extern int XDefaultScreen(IntPtr display);

	[DllImport(LibX11)]
	public static extern IntPtr XRootWindow(IntPtr display, int screen);

	[DllImport(LibX11)]
	public static extern int XDisplayWidth(IntPtr display, int screen);

	[DllImport(LibX11)]
	public static extern int XDisplayHeight(IntPtr display, int screen);

	// -- Window management --

	[DllImport(LibX11)]
	public static extern IntPtr XCreateWindow(
		IntPtr display,
		IntPtr parent,
		int x, int y,
		uint width, uint height,
		uint border_width,
		int depth,
		uint @class,
		IntPtr visual,
		ulong value_mask,
		ref XSetWindowAttributes attributes);

	[DllImport(LibX11)]
	public static extern int XDestroyWindow(IntPtr display, IntPtr window);

	[DllImport(LibX11)]
	public static extern int XMapWindow(IntPtr display, IntPtr window);

	[DllImport(LibX11)]
	public static extern int XSelectInput(IntPtr display, IntPtr window, long event_mask);

	// -- Events --

	[DllImport(LibX11)]
	public static extern int XNextEvent(IntPtr display, IntPtr event_return);

	[DllImport(LibX11)]
	public static extern int XPending(IntPtr display);

	// -- Atoms and properties --

	[DllImport(LibX11)]
	public static extern IntPtr XInternAtom(IntPtr display, string atom_name, bool only_if_exists);

	[DllImport(LibX11)]
	public static extern int XChangeProperty(
		IntPtr display,
		IntPtr window,
		IntPtr property,
		IntPtr type,
		int format,
		int mode,
		IntPtr data,
		int nelements);

	// -- Drawing (GC-based) --

	[DllImport(LibX11)]
	public static extern IntPtr XCreateGC(IntPtr display, IntPtr drawable, ulong value_mask, IntPtr values);

	[DllImport(LibX11)]
	public static extern int XFreeGC(IntPtr display, IntPtr gc);

	[DllImport(LibX11)]
	public static extern int XSetForeground(IntPtr display, IntPtr gc, ulong foreground);

	[DllImport(LibX11)]
	public static extern int XFillPolygon(
		IntPtr display,
		IntPtr drawable,
		IntPtr gc,
		XPoint[] points,
		int npoints,
		int shape,
		int mode);

	[DllImport(LibX11)]
	public static extern int XDrawString(
		IntPtr display,
		IntPtr drawable,
		IntPtr gc,
		int x, int y,
		string str,
		int length);

	[DllImport(LibX11)]
	public static extern int XClearWindow(IntPtr display, IntPtr window);

	// -- Sync/flush --

	[DllImport(LibX11)]
	public static extern int XFlush(IntPtr display);

	[DllImport(LibX11)]
	public static extern int XSync(IntPtr display, bool discard);

	// -- Visual info --

	[DllImport(LibX11)]
	public static extern IntPtr XDefaultVisual(IntPtr display, int screen);

	[DllImport(LibX11)]
	public static extern int XDefaultDepth(IntPtr display, int screen);

	[DllImport(LibX11)]
	public static extern IntPtr XDefaultColormap(IntPtr display, int screen);

	[DllImport(LibX11)]
	public static extern int XMatchVisualInfo(IntPtr display, int screen, int depth, int @class, out XVisualInfo info);

	// -- XShape extension (click-through) --

	[DllImport(LibXext)]
	public static extern void XShapeCombineRectangles(
		IntPtr display,
		IntPtr window,
		int dest_kind,
		int x_off, int y_off,
		XRectangle[]? rectangles,
		int n_rects,
		int op,
		int ordering);

	// -- Constants --

	// Window class
	public const uint InputOutput = 1;
	public const uint InputOnly = 2;

	// CW value mask bits
	public const ulong CWBackPixmap = 1L << 0;
	public const ulong CWBackPixel = 1L << 1;
	public const ulong CWBorderPixel = 1L << 3;
	public const ulong CWOverrideRedirect = 1L << 9;
	public const ulong CWColormap = 1L << 13;
	public const ulong CWEventMask = 1L << 11;

	// Event masks
	public const long ExposureMask = 1L << 15;
	public const long StructureNotifyMask = 1L << 17;
	public const long VisibilityChangeMask = 1L << 16;

	// Property mode
	public const int PropModeReplace = 0;

	// Atom type
	public static readonly IntPtr XA_ATOM = new(4);
	public static readonly IntPtr XA_CARDINAL = new(6);

	// XShape kinds
	public const int ShapeBounding = 0;
	public const int ShapeInput = 2;

	// XShape operations
	public const int ShapeSet = 0;

	// XShape ordering
	public const int Unsorted = 0;

	// Visual class for XMatchVisualInfo
	public const int TrueColor = 4;

	// XFillPolygon shape hint
	public const int Convex = 1;

	// XFillPolygon coord mode
	public const int CoordModeOrigin = 0;

	// -- Structs --

	[StructLayout(LayoutKind.Sequential)]
	public struct XSetWindowAttributes
	{
		public IntPtr background_pixmap;
		public ulong background_pixel;
		public ulong border_pixmap;
		public ulong border_pixel;
		public int bit_gravity;
		public int win_gravity;
		public int backing_store;
		public ulong backing_planes;
		public ulong backing_pixel;
		public int save_under;
		public long event_mask;
		public long do_not_propagate_mask;
		public int override_redirect;
		public IntPtr colormap;
		public IntPtr cursor;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct XVisualInfo
	{
		public IntPtr visual;
		public IntPtr visualid;
		public int screen;
		public int depth;
		public int @class;
		public ulong red_mask;
		public ulong green_mask;
		public ulong blue_mask;
		public int colormap_size;
		public int bits_per_rgb;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct XRectangle
	{
		public short x;
		public short y;
		public ushort width;
		public ushort height;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct XPoint
	{
		public short x;
		public short y;

		public XPoint(short x, short y)
		{
			this.x = x;
			this.y = y;
		}
	}

	// XCreateColormap
	[DllImport(LibX11)]
	public static extern IntPtr XCreateColormap(IntPtr display, IntPtr window, IntPtr visual, int alloc);

	public const int AllocNone = 0;
}
