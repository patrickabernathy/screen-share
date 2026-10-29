using System.Text.Json;

namespace ScreenShare;

internal sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenShare", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string ShareHotkey { get; set; } = "Ctrl+Alt+S";
    public string PauseHotkey { get; set; } = "Ctrl+Alt+P";
    public bool CaptureCursor { get; set; } = true;
    public bool CloseToTray { get; set; }
    /// <summary>When true, the projector moves off screen while sharing, so the pointer can't land on it.</summary>
    public bool ParkWhileSharing { get; set; } = true;
    public int ProjectorWidth { get; set; } = 1920;
    public int ProjectorHeight { get; set; } = 1080;
    /// <summary>When true, the projector resizes to match the shared window instead of keeping a fixed size.</summary>
    public bool ProjectorAutoSize { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Unreadable settings fall back to defaults rather than blocking class.
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
