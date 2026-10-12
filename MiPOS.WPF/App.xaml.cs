using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiPOS.Datos;
using MiPOS.Negocio.Servicios;
using MiPOS.WPF.Servicios;
using MiPOS.WPF.ViewModels;
using MiPOS.WPF.Vistas;

namespace MiPOS.WPF;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Formato argentino para números y precios ($ 1.500,00)
        var cultura = new CultureInfo("es-AR");
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(cultura.IetfLanguageTag)));

        // 2. Leer appsettings.json
        var configuracion = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        // 3. Registrar qué objetos existen y cómo crearlos
        var servicios = new ServiceCollection();

        servicios.AddDbContextFactory<MiPOSDbContext>(opciones =>
            opciones.UseSqlServer(configuracion.GetConnectionString("MiPOS")));

        servicios.AddTransient<ProductoServicio>();
        servicios.AddTransient<VentaServicio>();
        servicios.AddTransient<DialogoServicio>();
        servicios.AddTransient<CajaViewModel>();
        servicios.AddTransient<CajaVentana>();
        servicios.AddTransient<BuscarProductoViewModel>();
        servicios.AddTransient<CantidadViewModel>();
        servicios.AddTransient<CobroViewModel>();

        var proveedor = servicios.BuildServiceProvider();

        // 4. Abrir la ventana de caja (se crea con todo lo que necesita)
        proveedor.GetRequiredService<CajaVentana>().Show();
    }
}