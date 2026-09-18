using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Transcry.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
  public bool Invert { get; set; }

  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    var boolValue = value is true;
    if (Invert)
    {
      boolValue = !boolValue;
    }

    return boolValue ? Visibility.Visible : Visibility.Collapsed;
  }

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    throw new NotSupportedException();
  }
}
