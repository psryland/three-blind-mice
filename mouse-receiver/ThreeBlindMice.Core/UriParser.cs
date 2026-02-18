using System.Text.RegularExpressions;

namespace ThreeBlindMice.Core;

/// <summary>
/// Parses tbm:// protocol URIs. Never passes input to shell/exec.
/// </summary>
public static partial class TbmUriParser
{
	// Room code must be 4–8 alphanumeric characters
	[GeneratedRegex(@"^[a-zA-Z0-9]{4,8}$")]
	private static partial Regex Room_Code_Regex();

	/// <summary>
	/// Parses a tbm://room/&lt;code&gt; URI and returns the room code, or null if invalid.
	/// </summary>
	public static string? Parse_Room_Code(string uri)
	{
		if (string.IsNullOrWhiteSpace(uri))
			return null;

		// Validate the URI scheme and structure
		if (!uri.StartsWith("tbm://room/", StringComparison.OrdinalIgnoreCase))
			return null;

		var code = uri["tbm://room/".Length..];

		// Strip any trailing slash or query string that might have been appended
		var separator_index = code.IndexOfAny(['/', '?', '#']);
		if (separator_index >= 0)
			code = code[..separator_index];

		// Validate the room code with strict regex
		if (!Room_Code_Regex().IsMatch(code))
			return null;

		return code;
	}
}
