using System;
using System.Globalization;
using System.Windows.Data;

namespace BloodysManager.App.ViewModels;


    public sealed class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b)
               ? System.Windows.Media.Brushes.LimeGreen
               : System.Windows.Media.Brushes.IndianRed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

