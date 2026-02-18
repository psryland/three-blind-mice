using System.Text.RegularExpressions;

namespace ThreeBlindMice.Core;

/// <summary>
/// Parses and validates tbm:// protocol URIs.
/// Expected format: tbm://session/{code} where code is 4-8 alphanumeric characters.
/// </summary>
public static partial class TbmUriParser
{
	private static readonly Regex s_session_code_regex = SessionCodeRegex();

	/// <summary>
	/// Tries to extract a session code from a tbm:// URI.
	/// Returns null if the URI is invalid or the session code doesn't match the expected pattern.
	/// </summary>
	public static string? TryParseSessionCode(string uri)
	{
		// Normalise: trim whitespace and trailing slashes
		var trimmed = uri.Trim().TrimEnd('/');

		// Match tbm://session/{code}
		if (!trimmed.StartsWith("tbm://session/", StringComparison.OrdinalIgnoreCase))
			return null;

		var code = trimmed["tbm://session/".Length..];
		return s_session_code_regex.IsMatch(code) ? code : null;
	}

	/// <summary>
	/// Validates a standalone session code (4-8 alphanumeric characters).
	/// </summary>
	public static bool IsValidSessionCode(string code)
	{
		return s_session_code_regex.IsMatch(code);
	}

	[GeneratedRegex(@"^[a-zA-Z0-9]{4,8}$")]
	private static partial Regex SessionCodeRegex();
}
