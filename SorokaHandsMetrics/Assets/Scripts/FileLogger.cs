using System.IO;
using UnityEngine;

public static class FileLogger {
    private static string LogPath =>
        Path.Combine(Application.persistentDataPath, "placement_log.txt");

    public static void Log(string message) {
        try {
            var line = $"{System.DateTime.Now:HH:mm:ss.fff} | {message}\n";
            File.AppendAllText(LogPath, line);
        }
        catch {
            // never crash because of logging
        }
    }

    public static void Clear() {
        try {
            File.WriteAllText(LogPath, "=== LOG START ===\n");
        }
        catch { }
    }

    public static string GetPath() => LogPath;
}
