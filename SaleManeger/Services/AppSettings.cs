using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SaleManeger.Services
{
    public enum AppTheme { Dark, Light }

    /// <summary>Look of the program, kept per computer in a small json file next to sale.db.</summary>
    public class AppSettings : ReactiveObject
    {
        // Text and buttons are scaled together; these are the sizes offered on the settings screen.
        public static readonly double[] Scales = { 1.0, 1.15, 1.3 };

        private AppTheme _theme = AppTheme.Dark;
        private double _uiScale = 1.0;

        public AppTheme Theme
        {
            get => _theme;
            set => this.RaiseAndSetIfChanged(ref _theme, Enum.IsDefined(typeof(AppTheme), value) ? value : AppTheme.Dark);
        }

        // Always one of Scales, anything else (an edited file) falls back to the nearest one.
        public double UiScale
        {
            get => _uiScale;
            set => this.RaiseAndSetIfChanged(ref _uiScale, Nearest(value));
        }

        public static double Nearest(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) ? 1.0 : Scales.OrderBy(x => Math.Abs(x - value)).First();
    }

    public static class AppSettingsStore
    {
        public static string DefaultPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaleManagerSettings.json");

        private record Stored(string Theme, double UiScale);

        /// <summary>Reads the settings; a missing or damaged file just gives the defaults, it must never stop the program.</summary>
        public static AppSettings Load(string path)
        {
            var settings = new AppSettings();
            try
            {
                if (!File.Exists(path))
                    return settings;
                var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(path));
                if (stored == null)
                    return settings;
                if (Enum.TryParse<AppTheme>(stored.Theme, true, out var theme) && Enum.IsDefined(typeof(AppTheme), theme))
                    settings.Theme = theme;
                settings.UiScale = stored.UiScale <= 0 ? 1.0 : stored.UiScale;
            }
            catch
            {
                return new AppSettings();
            }
            return settings;
        }

        /// <summary>Returns false when the file could not be written (read-only folder), the settings then last until the program closes.</summary>
        public static bool TrySave(AppSettings settings, string path)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(path, JsonSerializer.Serialize(new Stored(settings.Theme.ToString(), settings.UiScale),
                    new JsonSerializerOptions { WriteIndented = true }));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
