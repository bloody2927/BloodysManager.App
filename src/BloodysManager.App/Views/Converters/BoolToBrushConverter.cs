using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BloodysManager.App.Views.Converters;

public sealed class BoolToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b
            ? Brushes.LimeGreen
            : Brushes.IndianRed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
