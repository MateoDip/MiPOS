using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MiPOS.WPF.Servicios;

/// <summary>
/// Lo contrario de BooleanToVisibilityConverter: muestra el elemento cuando el valor es false
/// (por ejemplo, el aviso "CAJA CERRADA" cuando CajaAbierta es false).
/// </summary>
public class VisibleSiFalse : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is false ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
