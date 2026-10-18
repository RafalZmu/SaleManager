using Avalonia;
using Avalonia.Media;
using Avalonia.Themes.Fluent;

namespace SaleManeger.Services
{
    /// <summary>
    /// The colours of the dark and the light look. Views use them as {DynamicResource AppXxx}, code that
    /// needs a colour (list rows, order highlighting, charts) reads the same palette from here.
    /// </summary>
    public static class ThemeService
    {
        public static bool IsLight { get; private set; }

        public static IBrush GoodBrush => Brush(IsLight ? "#1E7B34" : "#90EE90");
        public static IBrush BadBrush => Brush(IsLight ? "#B3261E" : "#F08080");
        public static IBrush WarnBrush => Brush(IsLight ? "#B35C00" : "#FFA500");
        public static IBrush TextBrush => Brush(IsLight ? "#1A1A1A" : "#FFFFFF");
        public static IBrush MutedBrush => Brush("#808080");

        // Name of the colour of a client row that has no order and no purchase.
        public static string DefaultTextColor => IsLight ? "Black" : "White";

        // Plot colours (OxyPlot has its own colour type).
        public static OxyPlot.OxyColor PlotText => IsLight ? OxyPlot.OxyColors.Black : OxyPlot.OxyColors.White;
        public static OxyPlot.OxyColor PlotGood => IsLight ? OxyPlot.OxyColor.Parse("#1E7B34") : OxyPlot.OxyColors.LightGreen;
        public static OxyPlot.OxyColor PlotAccent => IsLight ? OxyPlot.OxyColor.Parse("#1F5FBF") : OxyPlot.OxyColors.CornflowerBlue;

        /// <summary>Switches the whole program to the chosen theme. Safe to call without a running application (tests).</summary>
        public static void Apply(AppSettings settings)
        {
            IsLight = settings.Theme == AppTheme.Light;
            var app = Application.Current;
            if (app == null)
                return;

            foreach (var (key, dark, light) in Palette)
                app.Resources[key] = Brush(IsLight ? light : dark);

            foreach (var style in app.Styles)
            {
                if (style is FluentTheme fluent)
                    fluent.Mode = IsLight ? FluentThemeMode.Light : FluentThemeMode.Dark;
            }
        }

        private static readonly (string Key, string Dark, string Light)[] Palette =
        {
            ("AppBackground", "#1F1F1F", "#F3F3F3"),
            ("AppCard", "#1E1E1E", "#FFFFFF"),
            ("AppSurface", "#2D2D30", "#E4E4E6"),
            ("AppBorder", "#3E3E42", "#C6C6CB"),
            ("AppText", "#FFFFFF", "#1A1A1A"),
            ("AppTextSoft", "#BBBBBB", "#555555"),
            ("AppGood", "#90EE90", "#1E7B34"),
            ("AppBad", "#CD5C5C", "#B3261E"),
            ("AppWarn", "#FFA500", "#B35C00"),
            ("AppEditorBackground", "#000000", "#FFFFFF"),
            ("AppEditorForeground", "#FFFFFF", "#000000"),
            ("AppOverlay", "#90000000", "#80FFFFFF"),
        };

        private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
    }
}
