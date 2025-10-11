using System;
using System.Globalization;
using System.Windows.Data;

namespace BloodysManager.App.Views.Converters;

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) => v is bool b ? !b : v;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
