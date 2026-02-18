using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ThreeBlindMice.Core;

/// <summary>
/// WebSocket client for Azure Web PubSub using the json.webpubsub.azure.v1 subprotocol.
/// Zero NuGet dependencies — built entirely on System.Net.WebSockets.ClientWebSocket.
/// </summary>
public class PubSubClient : IDisposable
{
	private ClientWebSocket? m_ws;
	private CancellationTokenSource? m_cts;
	private Task? m_receive_task;
	private string m_negotiate_url = "";
	private string m_room = "";
	private string m_user_id = "";
	private bool m_disposed;

	// Reconnection backoff parameters
	private const int INITIAL_BACKOFF_MS = 1000;
	private const int MAX_BACKOFF_MS = 30000;

	// Security limits
	private const int MAX_MESSAGE_SIZE = 4096;
	private const int MAX_MESSAGES_PER_SECOND = 30;
	private readonly ConcurrentDictionary<string, (int count, long window_start)> m_rate_limits = new();

	public event Action<Message>? On_Message;
	public event Action<string>? On_Error;
	public event Action? On_Connected;
	public event Action? On_Disconnected;

	public bool Is_Connected => m_ws?.State == WebSocketState.Open;

	public async Task Connect(string negotiate_url, string room, string user_id)
	{
		m_negotiate_url = negotiate_url;
		m_room = room;
		m_user_id = user_id;
		m_cts = new CancellationTokenSource();

		await Connect_Internal(m_cts.Token);
	}

	public async Task Disconnect()
	{
		if (m_cts != null)
		{
			await m_cts.CancelAsync();
		}

		if (m_ws?.State == WebSocketState.Open)
		{
			try
			{
				using var close_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
				await m_ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", close_cts.Token);
			}
			catch (Exception)
			{
				// Best-effort close
			}
		}

		if (m_receive_task != null)
		{
			try
			{
				await m_receive_task;
			}
			catch (OperationCanceledException)
			{
				// Expected on cancellation
			}
		}

		m_ws?.Dispose();
		m_ws = null;
	}

	private async Task Connect_Internal(CancellationToken ct)
	{
		// Negotiate to get the WebSocket URL
		var ws_url = await Negotiate(ct);
		if (ws_url == null)
			return;

		m_ws = new ClientWebSocket();
		m_ws.Options.AddSubProtocol("json.webpubsub.azure.v1");

		await m_ws.ConnectAsync(new Uri(ws_url), ct);
		On_Connected?.Invoke();

		// Join the room group
		await Join_Group(m_room, ct);

		// Start the receive loop in the background
		m_receive_task = Task.Run(() => Receive_Loop(ct), ct);
	}

	private async Task<string?> Negotiate(CancellationToken ct)
	{
		try
		{
			using var http = new HttpClient();
			var request_body = JsonSerializer.Serialize(new { user_id = m_user_id });
			var content = new StringContent(request_body, Encoding.UTF8, "application/json");
			var response = await http.PostAsync(m_negotiate_url, content, ct);
			response.EnsureSuccessStatusCode();

			var json = await response.Content.ReadAsStringAsync(ct);
			using var doc = JsonDocument.Parse(json);

			if (doc.RootElement.TryGetProperty("url", out var url_element))
				return url_element.GetString();

			return null;
		}
		catch (Exception ex)
		{
			On_Error?.Invoke($"Negotiate failed: {ex.Message}");
			return null;
		}
	}

	private async Task Join_Group(string group, CancellationToken ct)
	{
		// Send a joinGroup message per the Web PubSub json subprotocol
		var join_msg = JsonSerializer.Serialize(new
		{
			type = "joinGroup",
			group = group,
		});

		var bytes = Encoding.UTF8.GetBytes(join_msg);
		await m_ws!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
	}

	private async Task Receive_Loop(CancellationToken ct)
	{
		var buffer = new byte[4096];

		while (!ct.IsCancellationRequested)
		{
			try
			{
				if (m_ws?.State != WebSocketState.Open)
				{
					On_Disconnected?.Invoke();
					await Reconnect(ct);
					continue;
				}

				var result = await m_ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

				if (result.MessageType == WebSocketMessageType.Close)
				{
					On_Disconnected?.Invoke();
					await Reconnect(ct);
					continue;
				}

				if (result.MessageType == WebSocketMessageType.Text)
				{
					// Accumulate the full message if it spans multiple frames
					var message_bytes = new MemoryStream();
					message_bytes.Write(buffer, 0, result.Count);

					while (!result.EndOfMessage)
					{
						result = await m_ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
						message_bytes.Write(buffer, 0, result.Count);
					}

					// Reject messages larger than 4KB
					if (message_bytes.Length > MAX_MESSAGE_SIZE)
						continue;

					var raw_json = Encoding.UTF8.GetString(message_bytes.ToArray());
					Process_Raw_Message(raw_json);
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (WebSocketException)
			{
				On_Disconnected?.Invoke();
				await Reconnect(ct);
			}
			catch (Exception ex)
			{
				On_Error?.Invoke($"Receive error: {ex.Message}");
				await Reconnect(ct);
			}
		}
	}

	private void Process_Raw_Message(string raw_json)
	{
		try
		{
			// Web PubSub json subprotocol wraps group messages in an envelope
			using var doc = JsonDocument.Parse(raw_json);
			var root = doc.RootElement;

			if (!root.TryGetProperty("type", out var type_prop))
				return;

			var envelope_type = type_prop.GetString();

			if (envelope_type == "message" && root.TryGetProperty("data", out var data_prop))
			{
				// Data can be a string or an object — handle both
				string inner_json;
				if (data_prop.ValueKind == JsonValueKind.String)
					inner_json = data_prop.GetString()!;
				else
					inner_json = data_prop.GetRawText();

				var msg = MessageParser.Parse(inner_json);
				if (msg == null)
					return;

				// Per-user rate limit for cursor messages (drop excess beyond 30/sec)
				if (msg is CursorMessage cursor_msg && Is_Rate_Limited(cursor_msg.User_Id))
					return;

				On_Message?.Invoke(msg);
			}
		}
		catch (JsonException)
		{
			// Ignore malformed messages
		}
	}

	private bool Is_Rate_Limited(string user_id)
	{
		var now = Environment.TickCount64;
		var state = m_rate_limits.AddOrUpdate(user_id,
			_ => (1, now),
			(_, existing) =>
			{
				if (now - existing.window_start >= 1000)
					return (1, now);
				return (existing.count + 1, existing.window_start);
			});
		return state.count > MAX_MESSAGES_PER_SECOND;
	}

	private async Task Reconnect(CancellationToken ct)
	{
		var backoff_ms = INITIAL_BACKOFF_MS;

		while (!ct.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(backoff_ms, ct);

				m_ws?.Dispose();
				m_ws = null;

				await Connect_Internal(ct);
				return;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				On_Error?.Invoke($"Reconnect failed: {ex.Message}");

				// Exponential backoff: 1s, 2s, 4s, 8s, ... max 30s
				backoff_ms = Math.Min(backoff_ms * 2, MAX_BACKOFF_MS);
			}
		}
	}

	public void Dispose()
	{
		if (m_disposed) return;
		m_disposed = true;

		m_cts?.Cancel();
		m_ws?.Dispose();
		m_cts?.Dispose();
		GC.SuppressFinalize(this);
	}
}
