using System;
using System.IO;
using Scoreboard;

internal static class Program
{
    private static int checks;

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }

    private static int Main()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Scoreboard.Tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new Database { World = 123 };
            database.Touch(1, "<b>Viking</b>\n");
            var stream = Guid.NewGuid().ToString("N");
            var totals = new Counters { Deaths = 2, Kills = 7, Bosses = 1 };
            Check(database.Apply(1, stream, totals), "First report accepted");
            Check(database.Apply(1, stream, totals), "Retry accepted");
            Check(database.Players[1].Totals.Kills == 7, "Retry must not duplicate kills");
            totals.Kills = 9;
            Check(database.Apply(1, stream, totals) && database.Players[1].Totals.Kills == 9, "Cumulative delta");
            Check(database.Apply(1, stream, new Counters { Deaths = 1, Kills = 8, Bosses = 2 }), "Older counters are tolerated");
            Check(database.Players[1].Totals.Kills == 9 && database.Players[1].Totals.Deaths == 2 && database.Players[1].Totals.Bosses == 2,
                "Each counter advances independently");
            Check(!database.Apply(1, stream, new Counters { Deaths = -1 }), "Negative values rejected");
            Check(!database.Apply(1, "bad", totals), "Invalid stream rejected");
            Check(!database.Apply(99, stream, totals), "Unknown player rejected");
            var otherStream = Guid.NewGuid().ToString("N");
            Check(database.Apply(1, otherStream, new Counters { Kills = 3 }) && database.Players[1].Totals.Kills == 12, "New client stream adds once");
            database.Touch(2, "Viking");
            Check(database.Apply(2, stream, totals) && database.Players[2].Totals.Kills == 9, "Identities are isolated");
            for (var i = 0; i < Database.MaximumStreamsPerPlayer - 2; i++)
                Check(database.Apply(1, Guid.NewGuid().ToString("N"), new Counters()), "Stream within limit accepted");
            Check(!database.Apply(1, Guid.NewGuid().ToString("N"), new Counters()), "Stream limit enforced");
            var secondWorld = new Database { World = 456 };
            secondWorld.Touch(1, "Viking");
            Check(secondWorld.Players[1].Totals.Kills == 0, "World isolation");
            Check(database.IsValid(123) && !database.IsValid(456), "World identity validation");
            Check(!database.Players[1].Name.Contains("<") && !database.Players[1].Name.Contains("\n"), "Name sanitization");

            var path = Path.Combine(directory, "world.json");
            Storage.Save(path, database);
            var restored = Storage.Load<Database>(path, () => throw new Exception("Missing file"));
            Check(restored.IsValid(123), "Persisted data validation");
            Check(restored.Apply(1, stream, totals) && restored.Players[1].Totals.Kills == 12, "Restart and retry do not duplicate");
            restored.Players[1].Seconds = 60;
            Storage.Save(path, restored);
            Check(File.Exists(path + ".bak"), "Atomic replacement keeps backup");
            Check(Storage.Load<Database>(path, () => null).Players[1].Seconds == 60, "Updated data persisted");
            var journalPath = Path.Combine(directory, "client.json");
            var journal = new ClientJournal { Totals = totals };
            Storage.Save(journalPath, journal);
            var recovered = Storage.Load<ClientJournal>(journalPath, () => null);
            Check(recovered.Stream == journal.Stream && recovered.Totals.Kills == 9, "Client outbox survives restart");
            File.WriteAllText(path, "{broken");
            var threw = false;
            try { Storage.Load<Database>(path, () => new Database()); } catch { threw = true; }
            Check(threw && File.ReadAllText(path) == "{broken", "Corruption fails without overwriting");
            Check(!new Database { World = 1, Players = { [1] = new PlayerRecord { Id = 1, Seconds = double.PositiveInfinity } } }.IsValid(1),
                "Non-finite connected time rejected");
            Console.WriteLine($"PASS: {checks} checks (aggregation, retries, identity, worlds, limits, storage, and corrupt data).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
