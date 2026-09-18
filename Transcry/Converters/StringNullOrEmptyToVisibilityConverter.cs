using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Transcry.Converters;

public sealed class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
  public bool Invert { get; set; }

  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    var isEmpty = string.IsNullOrWhiteSpace(value as string);
    if (Invert)
    {
      isEmpty = !isEmpty;
    }

    return isEmpty ? Visibility.Collapsed : Visibility.Visible;
  }

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    throw new NotSupportedException();
  }
}
