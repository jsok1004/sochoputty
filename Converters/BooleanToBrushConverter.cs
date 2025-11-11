using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SochoPutty.Converters
{
    public class BooleanToBrushConverter : IValueConverter
    {
        public Brush OnlineBrush { get; set; } = Brushes.LimeGreen;

        public Brush OfflineBrush { get; set; } = Brushes.Gray;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var isOnline = value is bool b && b;
            return isOnline ? OnlineBrush : OfflineBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}


