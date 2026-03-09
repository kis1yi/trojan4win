using System;
using System.IO;
using System.Text.Json;

namespace trojan4win.Services;

public static class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "trojan4win");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    // Internal test hook: set to a temp directory to avoid touching real user data in tests
    internal static string? _testSettingsDir;

    private static string ActiveSettingsDir =>
        _testSettingsDir ?? SettingsDir;

    private static string ActiveSettingsFile =>
        _testSettingsDir != null
            ? Path.Combine(_testSettingsDir, "settings.json")
            : SettingsFile;

    public static Models.AppSettings Load()
    {
        var settingsFile = ActiveSettingsFile;
        try
        {
            if (File.Exists(settingsFile))
            {
                var json = File.ReadAllText(settingsFile);
                return JsonSerializer.Deserialize<Models.AppSettings>(json, JsonOpts) ?? new Models.AppSettings();
            }
        }
        catch
        {
            // CR-15: rename the corrupt file so the user can inspect/recover it;
            // returning defaults avoids a crash-on-launch loop
            try { File.Move(settingsFile, settingsFile + ".bak", overwrite: true); }
            catch { /* backup attempt failed; still return defaults */ }
        }

        return new Models.AppSettings();
    }

    public static void Save(Models.AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(ActiveSettingsDir);
            var json = JsonSerializer.Serialize(settings, JsonOpts);
            // CR-14: write to a temp file then rename atomically; prevents a corrupt
            // settings.json if the process is killed mid-write
            var settingsFile = ActiveSettingsFile;
            var tmpPath = settingsFile + ".tmp";
            File.WriteAllText(tmpPath, json);
            if (File.Exists(settingsFile))
                File.Replace(tmpPath, settingsFile, null);
            else
                File.Move(tmpPath, settingsFile);
        }
        catch
        {
            // ignored
        }
    }
}
