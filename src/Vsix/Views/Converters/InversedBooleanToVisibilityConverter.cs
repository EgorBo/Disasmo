using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Disasmo.Utils
{
    public class InversedBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => 
            value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => 
            throw new NotImplementedException();
    }
}
