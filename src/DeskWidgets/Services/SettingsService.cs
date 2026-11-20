using DeskWidgets.Models;

namespace DeskWidgets.Services;

/// <summary>
/// Loads and saves settings.json in %APPDATA%\DeskWidgets. Saving is debounced (one write after
/// changes settle) and atomic (temp file then replace), so a crash cannot leave a half-written file.
/// </summary>
internal sealed class SettingsService : IDisposable
{
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 600 };
    private AppSettings? _pending;

    public string Folder { get; }
    public string FilePath { get; }
    public string? LastError { get; private set; }

    public SettingsService()
    {
        Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskWidgets");
        FilePath = Path.Combine(Folder, "settings.json");
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            if (_pending != null) SaveNow(_pending);
        };
    }

    /// <summary>Loads settings. A missing file gives defaults; a damaged file is kept as settings.corrupt.json.</summary>
    public AppSettings Load(out string? warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(FilePath)) return NewDefaults();

            var loaded = SettingsSerializer.TryDeserialize(File.ReadAllText(FilePath));
            if (loaded != null) return loaded;

            try { File.Copy(FilePath, Path.Combine(Folder, "settings.corrupt.json"), true); } catch { /* best effort */ }
            warning = "Your settings file was damaged, so defaults were restored. The old file was kept as settings.corrupt.json.";
            return NewDefaults();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            warning = "Settings could not be read, so defaults are being used.";
            return NewDefaults();
        }
    }

    private static AppSettings NewDefaults()
    {
        var s = new AppSettings();
        s.Sanitize();
        return s;
    }

    /// <summary>Queue a save; the write happens shortly after the last change.</summary>
    public void ScheduleSave(AppSettings settings)
    {
        _pending = settings;
        _debounce.Stop();
        _debounce.Start();
    }

    public void SaveNow(AppSettings settings)
    {
        _debounce.Stop();
        _pending = null;
        try
        {
            Directory.CreateDirectory(Folder);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, SettingsSerializer.Serialize(settings));
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message; // Saving is best effort; the app keeps running.
        }
    }

    public void Dispose()
    {
        _debounce.Stop();
        _debounce.Dispose();
    }
}
