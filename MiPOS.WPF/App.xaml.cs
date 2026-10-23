using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiPOS.Datos;
using MiPOS.DatosLocal;
using MiPOS.Facturacion;
using MiPOS.Facturacion.Arca;
using MiPOS.Hardware.Cajon;
using MiPOS.Hardware.Impresion;
using MiPOS.Hardware.Lector;
using MiPOS.Hardware.Terminal;
using MiPOS.Negocio.Cajas;
using MiPOS.Negocio.Comprobantes;
using MiPOS.Negocio.Facturacion;
using MiPOS.Negocio.Servicios;
using MiPOS.Negocio.SinConexion;
using MiPOS.Seguridad;
using MiPOS.WPF.Servicios;
using MiPOS.WPF.ViewModels;
using MiPOS.WPF.Vistas;

namespace MiPOS.WPF;

public partial class App : Application
{
    private ServiceProvider _proveedor = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // La aplicación termina solo cuando se lo pedimos (no al cerrar una ventana),
        // porque al cerrar sesión se cierra la caja y se vuelve a abrir el inicio de sesión
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

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
        RegistrarServicios(servicios, configuracion);
        _proveedor = servicios.BuildServiceProvider();

        // 4. Crear la base local de la caja (para vender sin conexión), si todavía no existe
        _proveedor.GetRequiredService<BaseLocal>().Preparar();

        // 5. Empezar por el inicio de sesión
        MostrarInicioSesion();
    }

    // Separado de OnStartup para poder comprobar desde una prueba que se pueden crear todos los objetos
    public static void RegistrarServicios(IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddDbContextFactory<MiPOSDbContext>(opciones =>
            opciones.UseSqlServer(configuracion.GetConnectionString("MiPOS")));

        // Base local SQLite de la caja (Fase 8): se usa cuando no hay conexión con la base central
        var opcionesSinConexion = configuracion.GetSection("SinConexion").Get<OpcionesSinConexion>() ?? new OpcionesSinConexion();
        servicios.AddSingleton(opcionesSinConexion);
        servicios.AddDbContextFactory<LocalDbContext>(opciones =>
            opciones.UseSqlite($"Data Source={opcionesSinConexion.RutaCompleta}"));

        // Únicas excepciones a "todo Transient": la sesión y el estado de la conexión tienen que ser
        // un solo objeto compartido por toda la aplicación
        servicios.AddSingleton<Sesion>();
        servicios.AddSingleton<BaseLocal>();

        // Qué caja es esta PC (cada PC tiene su número)
        servicios.AddTransient(_ => configuracion.GetSection("Caja").Get<OpcionesCaja>() ?? new OpcionesCaja());

        // Datos del comercio y de la impresora: cambian en cada instalación (appsettings.json)
        servicios.AddTransient(_ => configuracion.GetSection("Comercio").Get<DatosComercio>() ?? new DatosComercio());
        servicios.AddTransient(_ => configuracion.GetSection("Impresion").Get<OpcionesImpresion>() ?? new OpcionesImpresion());

        // Qué impresora se usa depende de Impresion:Modo
        servicios.AddTransient<IImpresoraTicket>(proveedor =>
        {
            var opciones = proveedor.GetRequiredService<OpcionesImpresion>();
            return opciones.Modo == ModoImpresion.Archivo
                ? new ImpresoraArchivo(opciones)
                : new ImpresoraWindows(opciones);
        });

        // Cajón de dinero, lector de códigos y terminal de pago: cada caja tiene los suyos
        servicios.AddTransient(_ => configuracion.GetSection("Cajon").Get<OpcionesCajon>() ?? new OpcionesCajon());
        servicios.AddTransient(_ => configuracion.GetSection("Lector").Get<OpcionesLector>() ?? new OpcionesLector());
        servicios.AddTransient(_ => configuracion.GetSection("Terminal").Get<OpcionesTerminal>() ?? new OpcionesTerminal());
        servicios.AddTransient<CajonDinero>();
        servicios.AddTransient<LectorCodigos>();

        // Con qué terminal se cobra depende de Terminal:Modo (en Manual no se usa: el cajero anota el cupón)
        servicios.AddTransient<ITerminalPago>(proveedor =>
        {
            var opciones = proveedor.GetRequiredService<OpcionesTerminal>();
            return opciones.Modo == ModoTerminal.MercadoPagoPoint
                ? new TerminalMercadoPagoPoint(opciones)
                : new TerminalSimulada();
        });

        // Factura electrónica (sección "Facturacion"): la parte técnica y los datos fiscales del comercio
        servicios.AddTransient(_ => configuracion.GetSection("Facturacion").Get<OpcionesFacturacion>() ?? new OpcionesFacturacion());
        servicios.AddTransient(_ => configuracion.GetSection("Facturacion").Get<DatosFiscales>() ?? new DatosFiscales());
        servicios.AddTransient<AutenticacionWsaa>();

        // Con quién se habla para facturar depende de Facturacion:Modo
        servicios.AddTransient<IServicioArca>(proveedor =>
        {
            var opciones = proveedor.GetRequiredService<OpcionesFacturacion>();
            return opciones.Modo == ModoFacturacion.Simulada
                ? new ServicioArcaSimulado(opciones)
                : new ServicioArcaWs(opciones, proveedor.GetRequiredService<AutenticacionWsaa>());
        });

        servicios.AddTransient<FacturacionServicio>();
        servicios.AddTransient<ProductoServicio>();
        servicios.AddTransient<VentaServicio>();
        servicios.AddTransient<UsuarioServicio>();
        servicios.AddTransient<TicketServicio>();
        servicios.AddTransient<CajaServicio>();
        servicios.AddTransient<StockServicio>();
        servicios.AddTransient<CompraServicio>();
        servicios.AddTransient<SincronizacionServicio>();
        servicios.AddTransient<DialogoServicio>();
        servicios.AddTransient<LoginViewModel>();
        servicios.AddTransient<CajaViewModel>();
        servicios.AddTransient<CajaVentana>();
        servicios.AddTransient<BuscarProductoViewModel>();
        servicios.AddTransient<IngresarValorViewModel>();
        servicios.AddTransient<CobroViewModel>();
        servicios.AddTransient<AutorizacionViewModel>();
        servicios.AddTransient<UsuariosViewModel>();
        servicios.AddTransient<UsuarioEdicionViewModel>();
        servicios.AddTransient<CambiarContrasenaViewModel>();
        servicios.AddTransient<MovimientoCajaViewModel>();
        servicios.AddTransient<CierreCajaViewModel>();
        servicios.AddTransient<ProductosViewModel>();
        servicios.AddTransient<ProductoEdicionViewModel>();
        servicios.AddTransient<AjusteStockViewModel>();
        servicios.AddTransient<MovimientosStockViewModel>();
        servicios.AddTransient<ComprasViewModel>();
        servicios.AddTransient<CompraViewModel>();
        servicios.AddTransient<CompraItemViewModel>();
        servicios.AddTransient<ProveedoresViewModel>();
        servicios.AddTransient<ProveedorEdicionViewModel>();
        servicios.AddTransient<ClienteFacturaViewModel>();
        servicios.AddTransient<FacturasPendientesViewModel>();
        servicios.AddTransient<HardwareViewModel>();

    }

    private void MostrarInicioSesion()
    {
        var viewModel = _proveedor.GetRequiredService<LoginViewModel>();
        var login = new LoginVentana(viewModel);
        MainWindow = login;
        viewModel.Iniciar();

        // Si se cierra el inicio de sesión sin ingresar, termina la aplicación
        if (login.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        // Cada sesión usa una caja nueva (con su propio carrito vacío)
        var caja = _proveedor.GetRequiredService<CajaVentana>();
        MainWindow = caja;

        caja.Closed += (_, _) =>
        {
            // Si se cerró con "Cerrar sesión" se vuelve al inicio de sesión;
            // si se cerró la ventana con la X, termina la aplicación
            if (_proveedor.GetRequiredService<Sesion>().HaySesion)
                Shutdown();
            else
                Dispatcher.BeginInvoke(MostrarInicioSesion); // cuando termine de cerrarse la caja
        };

        caja.Show();
    }
}