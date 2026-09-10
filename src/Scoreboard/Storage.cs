using System;
using System.IO;
using Newtonsoft.Json;

namespace Scoreboard
{
    internal static class Storage
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 32
        };

        internal static T Load<T>(string path, Func<T> create)
        {
            if (!File.Exists(path)) return create();
            // Fail closed on corrupt or newer data; never replace it with an empty database.
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), Settings)
                ?? throw new InvalidDataException("Empty statistics file: " + path);
        }

        internal static void Save<T>(string path, T value)
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) throw new InvalidDataException("The statistics path has no directory.");
            Directory.CreateDirectory(directory);
            var temp = path + ".tmp";
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file))
            {
                writer.Write(JsonConvert.SerializeObject(value, Formatting.Indented, Settings));
                writer.Flush();
                file.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
    }
}
