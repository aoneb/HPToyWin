using System.Text.RegularExpressions;

namespace HPToy.Core.Device;

public sealed class HiFiToyPresetManager
{
    /// <summary>Single slot for device import — overwritten on each import, no timestamp spam.</summary>
    public const string LegacyImportedPresetName = "从设备导入";

    private static HiFiToyPresetManager? _instance;
    private string? _officialPresetsPath;

    public static HiFiToyPresetManager Instance => _instance ??= new HiFiToyPresetManager();

    private HiFiToyPresetManager()
    {
        RestoreOldPreset();
        PrintUserPresets();
    }

    public void Initialize(string officialPresetsPath)
    {
        _officialPresetsPath = officialPresetsPath;
        RemoveLegacyImportPresetFiles();
        CleanupLegacyTimestampImportPresets();
        RestoreOldPreset();
        PrintUserPresets();
    }

    /// <summary>Remove obsolete import slots from older builds.</summary>
    private static void RemoveLegacyImportPresetFiles()
    {
        var dir = GetUserDir();
        if (!Directory.Exists(dir))
            return;

        foreach (var name in new[] { LegacyImportedPresetName, "Device import" })
        {
            var path = Path.Combine(dir, name + ".tpr");
            if (!File.Exists(path))
                continue;

            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }

    /// <summary>Remove auto-import files named yyyyMMdd_HHmmss.tpr from older builds.</summary>
    private static void CleanupLegacyTimestampImportPresets()
    {
        var dir = GetUserDir();
        if (!Directory.Exists(dir))
            return;

        foreach (var file in Directory.GetFiles(dir, "*.tpr"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!Regex.IsMatch(name, @"^\d{8}_\d{6}$"))
                continue;

            try
            {
                File.Delete(file);
            }
            catch
            {
            }
        }
    }

    private void RestoreOldPreset()
    {
        // No-op: legacy HiFiToyPreset binary migration is not supported on desktop.
    }

    public static string GetUserDir()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HPToy",
            "PresetList");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string? GetUserPresetFilePath(string presetName)
    {
        if (presetName == null) return null;
        var path = Path.Combine(GetUserDir(), presetName + ".tpr");
        return File.Exists(path) ? path : null;
    }

    private static bool CheckFormat(string filename) =>
        filename.Contains(".tpr", StringComparison.OrdinalIgnoreCase);

    private static string FilenameToPresetName(string name)
    {
        var end = name.IndexOf(".tpr", StringComparison.OrdinalIgnoreCase);
        return end != -1 ? name[..end] : name;
    }

    public List<string> GetOfficialPresetNameList()
    {
        var presetNameList = new List<string> { "No processing" };
        if (string.IsNullOrEmpty(_officialPresetsPath) || !Directory.Exists(_officialPresetsPath))
        {
            return presetNameList;
        }

        foreach (var filename in Directory.GetFiles(_officialPresetsPath, "*.tpr"))
        {
            presetNameList.Add(FilenameToPresetName(Path.GetFileName(filename)));
        }

        return presetNameList;
    }

    public List<string> GetUserPresetNameList()
    {
        var presetNameList = new List<string>();
        var dir = GetUserDir();
        if (!Directory.Exists(dir)) return presetNameList;

        foreach (var file in Directory.GetFiles(dir, "*.tpr"))
        {
            presetNameList.Add(FilenameToPresetName(Path.GetFileName(file)));
        }

        return presetNameList;
    }

    /// <summary>Latest user-imported preset by file write time (for ack recovery).</summary>
    public string? GetMostRecentUserPresetName()
    {
        var dir = GetUserDir();
        if (!Directory.Exists(dir))
            return null;

        return Directory.GetFiles(dir, "*.tpr")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => FilenameToPresetName(f.Name))
            .FirstOrDefault();
    }

    public List<string> GetPresetNameList()
    {
        var presetNameList = new List<string>();
        presetNameList.AddRange(GetOfficialPresetNameList());
        presetNameList.AddRange(GetUserPresetNameList());
        return presetNameList;
    }

    private ToyPreset GetOfficialPreset(string presetName)
    {
        if (presetName.Equals("No processing", StringComparison.Ordinal))
        {
            return new ToyPreset();
        }

        if (string.IsNullOrEmpty(_officialPresetsPath))
        {
            throw new IOException("Official preset not found.");
        }

        foreach (var name in GetOfficialPresetNameList())
        {
            if (presetName.Equals(name, StringComparison.Ordinal))
            {
                var filename = presetName + ".tpr";
                var path = Path.Combine(_officialPresetsPath, filename);
                return ToyPreset.FromFile(path);
            }
        }

        throw new IOException("Official preset not found.");
    }

    private ToyPreset GetUserPreset(string presetName)
    {
        var dir = GetUserDir();
        foreach (var file in Directory.GetFiles(dir, "*.tpr"))
        {
            var n = FilenameToPresetName(Path.GetFileName(file));
            if (presetName.Equals(n, StringComparison.Ordinal))
            {
                return ToyPreset.FromFile(file);
            }
        }

        throw new IOException("User preset not found");
    }

    public ToyPreset GetPreset(string presetName)
    {
        try
        {
            return GetOfficialPreset(presetName);
        }
        catch (IOException e) when (e.Message == "Official preset not found.")
        {
            return GetUserPreset(presetName);
        }
    }

    public int GetPresetIndex(string name)
    {
        var presetNameList = GetPresetNameList();
        for (var i = 0; i < presetNameList.Count; i++)
        {
            if (presetNameList[i].Equals(name, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    public ToyPreset GetPreset(int position)
    {
        var presetName = GetPresetNameList()[position];
        return GetPreset(presetName);
    }

    public bool IsUserPresetExist(string name) =>
        GetUserPresetNameList().Any(presetName => presetName.Equals(name, StringComparison.Ordinal));

    public bool IsOfficialPresetExist(string name) =>
        GetOfficialPresetNameList().Any(presetName => presetName.Equals(name, StringComparison.Ordinal));

    public bool IsPresetExist(string name) =>
        GetPresetNameList().Any(presetName => presetName.Equals(name, StringComparison.Ordinal));

    public int GetOfficialPresetSize() => GetOfficialPresetNameList().Count;
    public int GetUserPresetSize() => GetUserPresetNameList().Count;
    public int Size() => GetOfficialPresetSize() + GetUserPresetSize();

    public bool DeletePreset(string presetName)
    {
        var path = GetUserPresetFilePath(presetName);
        if (path != null && File.Exists(path))
        {
            File.Delete(path);
            return true;
        }
        return false;
    }

    public void RenamePreset(string oldName, string newName)
    {
        if (IsPresetExist(newName))
        {
            throw new IOException("Rename error because preset with this name is exist.");
        }

        var preset = GetUserPreset(oldName);
        preset.SetName(newName);
        preset.Save(true);
        DeletePreset(oldName);
    }

    public void ImportPreset(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new IOException("File path is null.");
        }

        var importPreset = ToyPreset.FromFile(filePath);
        importPreset.Save(false);
    }

    private static void PrintPresets(List<string> presetNameList)
    {
        _ = presetNameList;
    }

    public void PrintOfficialPresets() => PrintPresets(GetOfficialPresetNameList());
    public void PrintUserPresets() => PrintPresets(GetUserPresetNameList());
}
