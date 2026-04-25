using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace ItimHebrewCalendar.Windows
{
    // Application.Current.Resources[key] doesn't refresh when RequestedTheme changes
    // only at the window level, so we hand-pick brushes by isDark instead.
    internal static class CellTheme
    {
        public static SolidColorBrush TextPrimary(bool isDark) =>
            new(isDark ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(232, 0, 0, 0));

        public static SolidColorBrush TextSecondary(bool isDark) =>
            new(isDark ? Color.FromArgb(200, 255, 255, 255) : Color.FromArgb(155, 0, 0, 0));

        public static SolidColorBrush TextOnAccent() =>
            new(Colors.White);

        public static SolidColorBrush AccentText(bool isDark) =>
            new(isDark ? Color.FromArgb(255, 96, 205, 255) : Color.FromArgb(255, 0, 95, 184));

        public static SolidColorBrush AccentBackground(bool isDark) =>
            new(isDark ? Color.FromArgb(255, 76, 194, 255) : Color.FromArgb(255, 0, 120, 212));

        public static SolidColorBrush HolidayBackground(bool isDark) =>
            new(isDark ? Color.FromArgb(90, 255, 200, 80) : Color.FromArgb(130, 255, 220, 100));

        public static SolidColorBrush ShabbatBackground(bool isDark) =>
            new(isDark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(24, 0, 0, 0));

        public static SolidColorBrush HoverBackground(bool isDark) =>
            new(isDark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(18, 0, 0, 0));

        public static SolidColorBrush Border(bool isDark) =>
            new(isDark ? Color.FromArgb(80, 255, 255, 255) : Color.FromArgb(40, 0, 0, 0));

        public static SolidColorBrush NormalBackground() =>
            new(Colors.Transparent);
    }
}
