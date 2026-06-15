using System.Globalization;
using System.IO;
using System.Text.Json;

namespace HPToy.Win.Helpers;

public enum AppLanguage
{
    English,
    Chinese
}

public static class UiLanguageService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HPToy",
        "settings.json");

    public static AppLanguage Current { get; private set; }

    public static event Action? LanguageChanged;

    public static void Initialize()
    {
        Current = Load();
        UiText.Reload();
    }

    public static void SetLanguage(AppLanguage language)
    {
        if (Current == language) return;
        Current = language;
        Save();
        UiText.Reload();
        LanguageChanged?.Invoke();
    }

    public static bool IsChinese => Current == AppLanguage.Chinese;

    private static AppLanguage Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return DefaultFromOs();
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            return settings?.Language == "zh" ? AppLanguage.Chinese : AppLanguage.English;
        }
        catch
        {
            return DefaultFromOs();
        }
    }

    private static AppLanguage DefaultFromOs() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
            ? AppLanguage.Chinese
            : AppLanguage.English;

    private static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var settings = new AppSettings { Language = Current == AppLanguage.Chinese ? "zh" : "en" };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
        }
        catch
        {
        }
    }

    private sealed class AppSettings
    {
        public string Language { get; set; } = "en";
    }
}
