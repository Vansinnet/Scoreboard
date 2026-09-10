using System;
using System.Collections.Generic;
using System.Linq;

namespace Scoreboard
{
    // Plain data and aggregation: no Unity/game dependencies.
    public sealed class Counters
    {
        public const long MaximumValue = 1000000000;
        public long Deaths;
        public long Kills;
        public long Bosses;

        public bool IsValid() => Deaths >= 0 && Kills >= 0 && Bosses >= 0 &&
            Deaths <= MaximumValue && Kills <= MaximumValue && Bosses <= MaximumValue;

        public bool IsAggregateValid(int streams) => Deaths >= 0 && Kills >= 0 && Bosses >= 0 &&
            Deaths <= MaximumValue * streams && Kills <= MaximumValue * streams && Bosses <= MaximumValue * streams;

    }

    public sealed class PlayerRecord
    {
        public long Id;
        public string Name = "";
        public double Seconds;
        public long LastSeenUtc;
        public Counters Totals = new Counters();
        // High-water marks survive restarts alongside totals (one atomic document).
        public Dictionary<string, Counters> Streams = new Dictionary<string, Counters>();
    }

    public sealed class Database
    {
        public const int MaximumPlayers = 1000;
        public const int MaximumStreamsPerPlayer = 8;
        public const double MaximumSeconds = 3155760000;
        public int Schema = 1;
        public long World;
        public Dictionary<long, PlayerRecord> Players = new Dictionary<long, PlayerRecord>();

        public bool IsValid(long expectedWorld)
        {
            if (Schema != 1 || World != expectedWorld || Players == null) return false;
            if (Players.Count > MaximumPlayers) return false;
            foreach (var pair in Players)
            {
                var row = pair.Value;
                if (row == null || row.Id != pair.Key || row.Streams == null || row.Totals == null ||
                    !row.Totals.IsAggregateValid(MaximumStreamsPerPlayer) || row.Streams.Count > MaximumStreamsPerPlayer ||
                    row.Name == null || row.Name != CleanName(row.Name) || row.LastSeenUtc < 0 || row.LastSeenUtc > DateTime.MaxValue.Ticks ||
                    row.Seconds < 0 || row.Seconds > MaximumSeconds || double.IsNaN(row.Seconds) || double.IsInfinity(row.Seconds)) return false;
                var sum = new Counters();
                foreach (var stream in row.Streams)
                {
                    if (!Guid.TryParseExact(stream.Key, "N", out _) || stream.Value == null || !stream.Value.IsValid()) return false;
                    sum.Deaths += stream.Value.Deaths;
                    sum.Kills += stream.Value.Kills;
                    sum.Bosses += stream.Value.Bosses;
                }
                if (sum.Deaths != row.Totals.Deaths || sum.Kills != row.Totals.Kills || sum.Bosses != row.Totals.Bosses) return false;
            }
            return true;
        }

        public PlayerRecord Touch(long id, string name)
        {
            if (id == 0) return null;
            if (!Players.TryGetValue(id, out var row))
            {
                if (Players.Count >= MaximumPlayers) return null;
                Players.Add(id, row = new PlayerRecord { Id = id });
            }
            row.Name = CleanName(name);
            return row;
        }

        public bool Apply(long id, string stream, Counters incoming)
        {
            if (!Guid.TryParseExact(stream, "N", out _) || incoming == null || !incoming.IsValid()) return false;
            if (!Players.TryGetValue(id, out var row)) return false;
            if (!row.Streams.TryGetValue(stream, out var previous))
            {
                if (row.Streams.Count >= MaximumStreamsPerPlayer) return false;
                previous = new Counters();
            }
            var next = new Counters
            {
                Deaths = Math.Max(previous.Deaths, incoming.Deaths),
                Kills = Math.Max(previous.Kills, incoming.Kills),
                Bosses = Math.Max(previous.Bosses, incoming.Bosses)
            };
            long deaths;
            long kills;
            long bosses;
            try
            {
                deaths = checked(row.Totals.Deaths + next.Deaths - previous.Deaths);
                kills = checked(row.Totals.Kills + next.Kills - previous.Kills);
                bosses = checked(row.Totals.Bosses + next.Bosses - previous.Bosses);
            }
            catch (OverflowException) { return false; }
            if (deaths > Counters.MaximumValue * MaximumStreamsPerPlayer ||
                kills > Counters.MaximumValue * MaximumStreamsPerPlayer ||
                bosses > Counters.MaximumValue * MaximumStreamsPerPlayer) return false;
            row.Totals.Deaths = deaths;
            row.Totals.Kills = kills;
            row.Totals.Bosses = bosses;
            row.Streams[stream] = next;
            return true;
        }

        public static string CleanName(string name) => new string((name ?? "").Where(c =>
            !char.IsControl(c) && c != '<' && c != '>').Take(64).ToArray());
    }

    public sealed class ClientJournal
    {
        public int Schema = 1;
        public string Stream = Guid.NewGuid().ToString("N");
        public Counters Totals = new Counters();
    }

    public sealed class BoardRow
    {
        public string Name;
        public bool Online;
        public bool Reporting;
        public double Seconds;
        public long Deaths;
        public long Kills;
        public long Bosses;
    }
}
