using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace MiPOS.WPF.Idiomas;

/// <summary>
/// Traduce cada ventana cuando se abre, sin tocar los XAML ni los ViewModels:
/// - los textos fijos del XAML (Text, Content, Header, Title, ToolTip) se reemplazan por su traducción;
/// - a los textos que vienen del ViewModel (Binding) se les agrega el convertidor Traducir,
///   así los mensajes también salen traducidos cada vez que cambian.
/// No toca lo que escribe el usuario (TextBox) ni los datos de las listas (productos, ventas).
/// Se activa una sola vez en App.xaml.cs. En español no hace nada.
/// </summary>
public static class TraduccionVentanas
{
    private static readonly System.Windows.Media.FontFamily FuenteChino = new("Segoe UI, Microsoft YaHei UI");

    public static void Activar()
        => EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((ventana, _) => Traducir((Window)ventana)));

    public static void Traducir(Window ventana)
    {
        if (Traductor.EsEspanol)
            return;

        // Segoe UI para letras y números; Microsoft YaHei UI (incluida en Windows) para los caracteres chinos
        if (Traductor.Idioma == "zh")
            ventana.FontFamily = FuenteChino;

        TraducirPropiedad(ventana, Window.TitleProperty);
        Recorrer(ventana);
    }

    private static void Recorrer(DependencyObject elemento)
    {
        switch (elemento)
        {
            case TextBox:
                return; // lo que escribe el usuario no se traduce
            case TextBlock:
                TraducirPropiedad(elemento, TextBlock.TextProperty);
                break;
            case Run:
                TraducirPropiedad(elemento, Run.TextProperty);
                break;
            case HeaderedContentControl:
                TraducirPropiedad(elemento, HeaderedContentControl.HeaderProperty);
                TraducirPropiedad(elemento, ContentControl.ContentProperty);
                break;
            case ContentControl and not Window:
                TraducirPropiedad(elemento, ContentControl.ContentProperty);
                break;
            case DataGrid grilla:
                foreach (var columna in grilla.Columns)
                {
                    if (columna.Header is string titulo)
                        columna.Header = Traductor.Traducir(titulo);

                    // Las celdas son datos: solo cambian los valores que están en el catálogo
                    // (por ejemplo, el rol "Cajero" o el medio de pago "Efectivo"); un nombre de producto queda igual
                    if (columna is DataGridBoundColumn { Binding: Binding { Converter: null } enlace } conEnlace)
                        conEnlace.Binding = ConTraduccion(enlace);
                }
                return;
            case ItemsControl lista and (ComboBox or ListBox) when lista.ItemTemplate is null && lista.ItemTemplateSelector is null:
                // Las opciones de una lista (por ejemplo, los roles): se muestran con el convertidor
                var celda = new FrameworkElementFactory(typeof(TextBlock));
                celda.SetBinding(TextBlock.TextProperty, new Binding(lista.DisplayMemberPath ?? string.Empty)
                {
                    Converter = ConvertidorTraduccion.Instancia
                });
                lista.DisplayMemberPath = null;
                lista.ItemTemplate = new DataTemplate { VisualTree = celda };
                break;
        }

        if (elemento is FrameworkElement { ToolTip: string ayuda } conAyuda)
            conAyuda.ToolTip = Traductor.Traducir(ayuda);

        // Los ítems de una lista con ItemsSource son datos (no DependencyObject): se saltean solos
        foreach (var hijo in LogicalTreeHelper.GetChildren(elemento).OfType<DependencyObject>().ToList())
            Recorrer(hijo);
    }

    private static void TraducirPropiedad(DependencyObject elemento, DependencyProperty propiedad)
    {
        if (BindingOperations.GetBindingExpression(elemento, propiedad)?.ParentBinding is { } enlace)
        {
            if (enlace.Converter is null)
                BindingOperations.SetBinding(elemento, propiedad, ConTraduccion(enlace));
            return;
        }

        // MultiBinding u otros enlaces: se dejan como están
        if (BindingOperations.IsDataBound(elemento, propiedad))
            return;

        if (elemento.ReadLocalValue(propiedad) is string texto)
            elemento.SetValue(propiedad, Traductor.Traducir(texto));
    }

    // Una copia del Binding con el convertidor (un Binding ya en uso no se puede modificar)
    private static Binding ConTraduccion(Binding original)
    {
        var copia = new Binding
        {
            Path = original.Path,
            Mode = original.Mode,
            UpdateSourceTrigger = original.UpdateSourceTrigger,
            StringFormat = original.StringFormat is null ? null : Traductor.Traducir(original.StringFormat),
            FallbackValue = original.FallbackValue,
            TargetNullValue = original.TargetNullValue,
            Converter = ConvertidorTraduccion.Instancia
        };

        if (original.Source is not null)
            copia.Source = original.Source;
        else if (original.RelativeSource is not null)
            copia.RelativeSource = original.RelativeSource;
        else if (!string.IsNullOrEmpty(original.ElementName))
            copia.ElementName = original.ElementName;

        return copia;
    }
}

// Traduce los textos (y los valores de un enum, como Rol) que llegan por Binding.
// Lo demás (números, fechas, objetos) pasa igual.
public sealed class ConvertidorTraduccion : IValueConverter
{
    public static readonly ConvertidorTraduccion Instancia = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        string texto => Traductor.Traducir(texto),
        Enum valor => Traductor.Traducir(valor.ToString()),
        _ => value
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value;
}
