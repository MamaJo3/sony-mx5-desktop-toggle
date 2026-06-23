using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SonyXm5.Core;

/// <summary>User settings, persisted as config.json next to the executables.</summary>
public sealed class AppConfig
{
    public string hotkey { get; set; } = "CTRL+ALT+A";
    public string behavior { get; set; } = "toggle";   // "toggle" | "hold"
    public string modeA { get; set; } = "amb";          // amb | nc | off | wind
    public string modeB { get; set; } = "nc";
    public int ambientLevel { get; set; } = 20;          // 0..20

    private static readonly JsonSerializerOptions Opt =
        new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Load from path, creating it with defaults if missing.</summary>
    public static AppConfig Load(string path)
    {
        try { if (File.Exists(path)) return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path)) ?? new AppConfig(); }
        catch { }
        var c = new AppConfig();
        try { File.WriteAllText(path, JsonSerializer.Serialize(c, Opt)); } catch { }
        return c;
    }

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Opt));
}
