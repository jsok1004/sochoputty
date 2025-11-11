using System;
using System.Globalization;
using System.Windows.Data;

namespace SochoPutty.Converters
{
    public class BooleanToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var isOnline = value is bool b && b;
            return isOnline ? "온라인" : "오프라인";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}


