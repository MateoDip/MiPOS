using System.Windows;
using MiPOS.WPF.ViewModels;

namespace MiPOS.WPF.Vistas;

public partial class CajaVentana : Window
{
    public CajaVentana(CajaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Al abrir, el cursor queda listo en el campo de código
        Loaded += (_, _) => TxtCodigo.Focus();
    }
}