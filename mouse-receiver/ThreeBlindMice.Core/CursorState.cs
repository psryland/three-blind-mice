using System.Collections.Concurrent;

namespace ThreeBlindMice.Core;

public struct TrailPoint
{
	public double X { get; set; }
	public double Y { get; set; }
	public DateTime Timestamp { get; set; }
}

public class CursorInfo
{
	public string User_Id { get; set; } = "";
	public string Name { get; set; } = "";
	public string Colour { get; set; } = "";
	public double X { get; set; }
	public double Y { get; set; }
	public int Button { get; set; }
	public DateTime Last_Updated { get; set; } = DateTime.UtcNow;

	// Laser pointer trail — populated while Button == 1
	public List<TrailPoint> Trail_Points { get; } = new();
	public const int MAX_TRAIL_POINTS = 50;
}

public class CursorState
{
	private readonly ConcurrentDictionary<string, CursorInfo> m_cursors = new();
	private const int MAX_USERS = 10;
	private const double INACTIVITY_TIMEOUT_SECONDS = 3.0;

	public IReadOnlyDictionary<string, CursorInfo> Cursors => m_cursors;

	public void Update_Cursor(CursorMessage msg)
	{
		// Security: validate and clamp inputs
		var user_id = msg.User_Id.Length > 50 ? msg.User_Id[..50] : msg.User_Id;
		var name = msg.Name.Length > 20 ? msg.Name[..20] : msg.Name;
		var x = Math.Clamp(msg.X, 0.0, 1.0);
		var y = Math.Clamp(msg.Y, 0.0, 1.0);
		var button = Math.Clamp(msg.Button, 0, 2);

		if (!Is_Valid_Colour(msg.Colour))
			return;

		if (m_cursors.Count >= MAX_USERS && !m_cursors.ContainsKey(user_id))
			return;

		m_cursors.AddOrUpdate(user_id,
			_ =>
			{
				var info = new CursorInfo
				{
					User_Id = user_id,
					Name = name,
					Colour = msg.Colour,
					X = x,
					Y = y,
					Button = button,
					Last_Updated = DateTime.UtcNow,
				};
				Update_Trail(info, x, y, button);
				return info;
			},
			(_, existing) =>
			{
				existing.Name = name;
				existing.Colour = msg.Colour;
				existing.X = x;
				existing.Y = y;
				existing.Button = button;
				existing.Last_Updated = DateTime.UtcNow;
				Update_Trail(existing, x, y, button);
				return existing;
			});
	}

	public void Add_User(JoinMessage msg)
	{
		var user_id = msg.User_Id.Length > 50 ? msg.User_Id[..50] : msg.User_Id;
		var name = msg.Name.Length > 20 ? msg.Name[..20] : msg.Name;
		if (!Is_Valid_Colour(msg.Colour) || m_cursors.Count >= MAX_USERS)
			return;

		m_cursors.TryAdd(user_id, new CursorInfo
		{
			User_Id = user_id,
			Name = name,
			Colour = msg.Colour,
		});
	}

	public void Remove_User(string user_id)
	{
		m_cursors.TryRemove(user_id, out _);
	}

	public void Remove_Inactive()
	{
		var cutoff = DateTime.UtcNow.AddSeconds(-INACTIVITY_TIMEOUT_SECONDS);
		foreach (var kvp in m_cursors)
		{
			if (kvp.Value.Last_Updated < cutoff)
				m_cursors.TryRemove(kvp.Key, out _);
		}
	}

	private static void Update_Trail(CursorInfo info, double x, double y, int button)
	{
		if (button == 1)
		{
			info.Trail_Points.Add(new TrailPoint { X = x, Y = y, Timestamp = DateTime.UtcNow });

			// Trim oldest points to stay within budget
			while (info.Trail_Points.Count > CursorInfo.MAX_TRAIL_POINTS)
				info.Trail_Points.RemoveAt(0);
		}
		else
		{
			info.Trail_Points.Clear();
		}
	}

	private static bool Is_Valid_Colour(string colour)
	{
		if (colour.Length != 7 || colour[0] != '#')
			return false;
		for (int i = 1; i < 7; i++)
		{
			var c = colour[i];
			if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
				return false;
		}
		return true;
	}
}
