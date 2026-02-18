using System.Text.RegularExpressions;

namespace ThreeBlindMice.Core;

/// <summary>
/// Parses and validates tbm:// protocol URIs.
/// Expected format: tbm://room/{code} where code is 4-8 alphanumeric characters.
/// </summary>
public static partial class TbmUriParser
{
	private static readonly Regex s_room_code_regex = RoomCodeRegex();

	/// <summary>
	/// Tries to extract a room code from a tbm:// URI.
	/// Returns null if the URI is invalid or the room code doesn't match the expected pattern.
	/// </summary>
	public static string? TryParseRoomCode(string uri)
	{
		// Normalise: trim whitespace and trailing slashes
		var trimmed = uri.Trim().TrimEnd('/');

		// Match tbm://room/{code}
		if (!trimmed.StartsWith("tbm://room/", StringComparison.OrdinalIgnoreCase))
			return null;

		var code = trimmed["tbm://room/".Length..];
		return s_room_code_regex.IsMatch(code) ? code : null;
	}

	/// <summary>
	/// Validates a standalone room code (4-8 alphanumeric characters).
	/// </summary>
	public static bool IsValidRoomCode(string code)
	{
		return s_room_code_regex.IsMatch(code);
	}

	[GeneratedRegex(@"^[a-zA-Z0-9]{4,8}$")]
	private static partial Regex RoomCodeRegex();
}
