using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ItimHebrewCalendar.Windows
{
    // Shared UI building blocks for reminder offset editing, reused by the event
    // editor and the standalone-reminder cards in settings.
    internal static class ReminderUiHelpers
    {
        // A "value + unit" offset editor. The offset is stored everywhere as minutes;
        // this lets the user enter it in minutes or whole hours (e.g. 2 hours = 120 min).
        // Negative values mean "before" the anchor.
        public static FrameworkElement BuildOffsetInput(int initialMinutes, Action<int> onChanged,
            string header = "היסט (שלילי = לפני)")
        {
            int abs = Math.Abs(initialMinutes);
            bool hours = abs != 0 && abs % 60 == 0;
            double initialValue = hours ? initialMinutes / 60.0 : initialMinutes;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var valueBox = new NumberBox
            {
                Header = header,
                Value = initialValue,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Margin = new Thickness(0, 0, 4, 0)
            };

            var unitCombo = new ComboBox
            {
                Header = "יחידה",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(4, 0, 0, 0)
            };
            unitCombo.Items.Add(new ComboBoxItem { Content = "דקות", Tag = "min" });
            unitCombo.Items.Add(new ComboBoxItem { Content = "שעות", Tag = "hr" });
            unitCombo.SelectedIndex = hours ? 1 : 0;

            void Recompute()
            {
                double v = double.IsNaN(valueBox.Value) ? 0 : valueBox.Value;
                bool isHours = (unitCombo.SelectedItem as ComboBoxItem)?.Tag as string == "hr";
                onChanged((int)Math.Round(v * (isHours ? 60 : 1)));
            }
            valueBox.ValueChanged += (_, _) => Recompute();
            unitCombo.SelectionChanged += (_, _) => Recompute();

            Grid.SetColumn(valueBox, 0);
            grid.Children.Add(valueBox);
            Grid.SetColumn(unitCombo, 1);
            grid.Children.Add(unitCombo);
            return grid;
        }
    }
}
