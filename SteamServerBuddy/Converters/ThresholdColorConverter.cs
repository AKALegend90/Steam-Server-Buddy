using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SteamServerBuddy.Converters
{
    /// <summary>
    /// Converts a percentage value to a color based on thresholds:
    /// Green < 70%, Yellow 70-85%, Red > 85%
    /// </summary>
    public class ThresholdColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percent)
            {
                if (percent < 70)
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#48BB78")); // Green
                if (percent < 85)
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECC94B")); // Yellow
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53E3E")); // Red
            }
            
            if (value is float floatPercent)
            {
                if (floatPercent < 70)
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#48BB78"));
                if (floatPercent < 85)
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECC94B"));
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53E3E"));
            }

            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a percentage value to a string color code
    /// </summary>
    public class ThresholdColorStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = 0;
            if (value is double d) percent = d;
            else if (value is float f) percent = f;
            else if (value is int i) percent = i;

            if (percent < 70) return "#48BB78"; // Green
            if (percent < 85) return "#ECC94B"; // Yellow
            return "#E53E3E"; // Red
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
