using System.Configuration;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MarkdownPad;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
}

/// <summary>
/// Converts boolean to Visibility
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}

/// <summary>
/// Converts boolean to TextWrapping
/// </summary>
public class BoolToWrapConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? TextWrapping.Wrap : TextWrapping.NoWrap;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is TextWrapping.Wrap;
    }
}

