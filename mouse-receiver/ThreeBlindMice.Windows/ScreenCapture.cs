using System.Runtime.InteropServices;

namespace ThreeBlindMice.Windows;

/// <summary>
/// Captures monitor screenshots as BMP data URLs using pure Win32 P/Invoke.
/// No System.Drawing or NuGet dependencies required.
/// </summary>
internal static class ScreenCapture
{
	/// <summary>
	/// Captures a monitor region as a base64 data URL (BMP format).
	/// The image is scaled down to thumbnail size for efficient transmission.
	/// </summary>
	public static string? Capture_Monitor(int left, int top, int width, int height, int thumb_width = 320)
	{
		if (width <= 0 || height <= 0 || thumb_width <= 0)
			return null;

		var thumb_height = (int)((long)height * thumb_width / width);
		if (thumb_height <= 0)
			return null;

		var screen_dc = IntPtr.Zero;
		var mem_dc = IntPtr.Zero;
		var dib_bitmap = IntPtr.Zero;
		var old_bitmap = IntPtr.Zero;

		try
		{
			screen_dc = CreateDCW("DISPLAY", null, null, IntPtr.Zero);
			if (screen_dc == IntPtr.Zero)
				return null;

			mem_dc = CreateCompatibleDC(screen_dc);
			if (mem_dc == IntPtr.Zero)
				return null;

			// 24-bit BGR, bottom-up DIB at thumbnail dimensions
			var bmi = new BITMAPINFO();
			bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
			bmi.bmiHeader.biWidth = thumb_width;
			bmi.bmiHeader.biHeight = thumb_height;
			bmi.bmiHeader.biPlanes = 1;
			bmi.bmiHeader.biBitCount = 24;
			bmi.bmiHeader.biCompression = BI_RGB;

			dib_bitmap = CreateDIBSection(mem_dc, ref bmi, DIB_RGB_COLORS, out var bits_ptr, IntPtr.Zero, 0);
			if (dib_bitmap == IntPtr.Zero || bits_ptr == IntPtr.Zero)
				return null;

			old_bitmap = SelectObject(mem_dc, dib_bitmap);

			// HALFTONE gives better quality when downscaling
			SetStretchBltMode(mem_dc, HALFTONE);
			SetBrushOrgEx(mem_dc, 0, 0, IntPtr.Zero);

			var ok = StretchBlt(
				mem_dc, 0, 0, thumb_width, thumb_height,
				screen_dc, left, top, width, height,
				SRCCOPY);

			if (!ok)
				return null;

			GdiFlush();

			// Read pixel data from the DIB section
			var row_stride = ((thumb_width * 3 + 3) / 4) * 4;
			var pixel_data_size = row_stride * thumb_height;
			var pixel_data = new byte[pixel_data_size];
			Marshal.Copy(bits_ptr, pixel_data, 0, pixel_data_size);

			var bmp_bytes = Build_Bmp_File(thumb_width, thumb_height, pixel_data);
			var base64 = Convert.ToBase64String(bmp_bytes);
			return $"data:image/bmp;base64,{base64}";
		}
		catch
		{
			return null;
		}
		finally
		{
			if (old_bitmap != IntPtr.Zero && mem_dc != IntPtr.Zero)
				SelectObject(mem_dc, old_bitmap);
			if (dib_bitmap != IntPtr.Zero)
				DeleteObject(dib_bitmap);
			if (mem_dc != IntPtr.Zero)
				DeleteDC(mem_dc);
			if (screen_dc != IntPtr.Zero)
				DeleteDC(screen_dc);
		}
	}

	/// <summary>
	/// Assembles a valid BMP file from raw 24-bit BGR pixel data.
	/// </summary>
	private static byte[] Build_Bmp_File(int width, int height, byte[] pixel_data)
	{
		const int file_header_size = 14;
		const int dib_header_size = 40;
		var pixel_offset = file_header_size + dib_header_size;
		var file_size = pixel_offset + pixel_data.Length;

		var bmp = new byte[file_size];
		using var ms = new MemoryStream(bmp);
		using var w = new BinaryWriter(ms);

		// BITMAPFILEHEADER (14 bytes)
		w.Write((byte)'B');
		w.Write((byte)'M');
		w.Write(file_size);
		w.Write((short)0); // reserved1
		w.Write((short)0); // reserved2
		w.Write(pixel_offset);

		// BITMAPINFOHEADER (40 bytes)
		w.Write(dib_header_size);
		w.Write(width);
		w.Write(height);
		w.Write((short)1);  // planes
		w.Write((short)24); // bits per pixel
		w.Write(0);         // compression (BI_RGB)
		w.Write(pixel_data.Length);
		w.Write(0);         // x pixels per meter
		w.Write(0);         // y pixels per meter
		w.Write(0);         // colors used
		w.Write(0);         // colors important

		w.Write(pixel_data);

		return bmp;
	}

	#region P/Invoke

	private const uint SRCCOPY = 0x00CC0020;
	private const uint BI_RGB = 0;
	private const uint DIB_RGB_COLORS = 0;
	private const int HALFTONE = 4;

	[StructLayout(LayoutKind.Sequential)]
	private struct BITMAPINFOHEADER
	{
		public uint biSize;
		public int biWidth;
		public int biHeight;
		public ushort biPlanes;
		public ushort biBitCount;
		public uint biCompression;
		public uint biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public uint biClrUsed;
		public uint biClrImportant;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BITMAPINFO
	{
		public BITMAPINFOHEADER bmiHeader;
	}

	[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr CreateDCW(string lpszDriver, string? lpszDevice, string? lpszOutput, IntPtr lpInitData);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

	[DllImport("gdi32.dll")]
	private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(IntPtr ho);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteDC(IntPtr hdc);

	[DllImport("gdi32.dll")]
	private static extern int SetStretchBltMode(IntPtr hdc, int mode);

	[DllImport("gdi32.dll")]
	private static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr lppt);

	[DllImport("gdi32.dll")]
	private static extern bool StretchBlt(
		IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
		IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc,
		uint rop);

	[DllImport("gdi32.dll")]
	private static extern bool GdiFlush();

	#endregion
}
