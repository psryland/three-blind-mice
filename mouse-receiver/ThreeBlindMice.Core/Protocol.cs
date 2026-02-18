using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThreeBlindMice.Core;

// Base message type
public abstract class Message
{
	[JsonPropertyName("type")]
	public abstract string Type { get; }
}

public class CursorMessage : Message
{
	[JsonPropertyName("type")]
	public override string Type => "cursor";

	[JsonPropertyName("user_id")]
	public string User_Id { get; set; } = "";

	[JsonPropertyName("name")]
	public string Name { get; set; } = "";

	[JsonPropertyName("colour")]
	public string Colour { get; set; } = "";

	[JsonPropertyName("x")]
	public double X { get; set; }

	[JsonPropertyName("y")]
	public double Y { get; set; }

	[JsonPropertyName("button")]
	public int Button { get; set; }
}

public class JoinMessage : Message
{
	[JsonPropertyName("type")]
	public override string Type => "join";

	[JsonPropertyName("user_id")]
	public string User_Id { get; set; } = "";

	[JsonPropertyName("name")]
	public string Name { get; set; } = "";

	[JsonPropertyName("colour")]
	public string Colour { get; set; } = "";
}

public class LeaveMessage : Message
{
	[JsonPropertyName("type")]
	public override string Type => "leave";

	[JsonPropertyName("user_id")]
	public string User_Id { get; set; } = "";
}

public class HostConfigMessage : Message
{
	[JsonPropertyName("type")]
	public override string Type => "host_config";

	[JsonPropertyName("aspect_ratio")]
	public double Aspect_Ratio { get; set; }

	[JsonPropertyName("monitor_name")]
	public string Monitor_Name { get; set; } = "";
}

public static class MessageParser
{
	private static readonly JsonSerializerOptions s_options = new()
	{
		PropertyNameCaseInsensitive = true,
		NumberHandling = JsonNumberHandling.Strict,
		MaxDepth = 5,
	};

	public static Message? Parse(string json)
	{
		// Parse to get the type field first
		using var doc = JsonDocument.Parse(json);
		if (!doc.RootElement.TryGetProperty("type", out var type_element))
			return null;

		var type = type_element.GetString();
		return type switch
		{
			"cursor" => JsonSerializer.Deserialize<CursorMessage>(json, s_options),
			"join" => JsonSerializer.Deserialize<JoinMessage>(json, s_options),
			"leave" => JsonSerializer.Deserialize<LeaveMessage>(json, s_options),
			"host_config" => JsonSerializer.Deserialize<HostConfigMessage>(json, s_options),
			_ => null,
		};
	}

	public static string Serialize(Message message)
	{
		return JsonSerializer.Serialize(message, message.GetType(), s_options);
	}
}
