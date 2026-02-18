using Microsoft.Win32;

namespace ThreeBlindMice.Windows;

/// <summary>
/// Registers the tbm:// custom URI protocol in the current user's registry
/// so that clicking tbm:// links in a browser launches this application.
/// </summary>
internal static class ProtocolRegistrar
{
	private const string ProtocolKey = @"Software\Classes\tbm";

	/// <summary>
	/// Returns true if the tbm:// protocol is already registered for the current user.
	/// </summary>
	public static bool Is_Registered()
	{
		using var key = Registry.CurrentUser.OpenSubKey(ProtocolKey);
		return key?.GetValue("URL Protocol") != null;
	}

	/// <summary>
	/// Registers the tbm:// protocol handler pointing to the current executable.
	/// Writes to HKCU so no elevation is required.
	/// </summary>
	public static void Register()
	{
		var exe_path = Environment.ProcessPath
			?? throw new InvalidOperationException("Cannot determine executable path.");

		using var key = Registry.CurrentUser.CreateSubKey(ProtocolKey);
		key.SetValue("", "URL:TBM Protocol");
		key.SetValue("URL Protocol", "");

		using var command_key = key.CreateSubKey(@"shell\open\command");
		command_key.SetValue("", $"\"{exe_path}\" \"%1\"");
	}
}
