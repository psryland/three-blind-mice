using System.Runtime.InteropServices;

namespace ThreeBlindMice.Windows;

/// <summary>
/// Tracks a window by HWND, polling its position and size periodically.
/// Fires On_Bounds_Changed when the window moves or resizes, and
/// On_Window_Lost when the window is closed or destroyed.
/// </summary>
internal sealed class WindowTracker : IDisposable
{
	private readonly IntPtr m_hwnd;
	private CancellationTokenSource? m_cts;
	private Task? m_poll_task;

	public record Bounds(int Left, int Top, int Width, int Height);

	public event Action<Bounds>? On_Bounds_Changed;
	public event Action? On_Window_Lost;

	public WindowTracker(IntPtr hwnd)
	{
		m_hwnd = hwnd;
	}

	public void Start()
	{
		Stop();
		m_cts = new CancellationTokenSource();
		m_poll_task = Poll_Loop(m_cts.Token);
	}

	public void Stop()
	{
		m_cts?.Cancel();
		try { m_poll_task?.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
		m_cts?.Dispose();
		m_cts = null;
		m_poll_task = null;
	}

	private async Task Poll_Loop(CancellationToken ct)
	{
		Bounds? last = null;

		while (!ct.IsCancellationRequested)
		{
			if (!IsWindow(m_hwnd) || !GetWindowRect(m_hwnd, out var rect))
			{
				On_Window_Lost?.Invoke();
				break;
			}

			var current = new Bounds(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
			if (last == null || current != last)
			{
				last = current;
				On_Bounds_Changed?.Invoke(current);
			}

			try { await Task.Delay(100, ct).ConfigureAwait(false); }
			catch (OperationCanceledException) { break; }
		}
	}

	public void Dispose() => Stop();

	#region P/Invoke

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int Left, Top, Right, Bottom;
	}

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	private static extern bool IsWindow(IntPtr hWnd);

	#endregion
}
