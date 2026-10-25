using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiPOS.Datos;
using MiPOS.Dominio.Entidades;
using MiPOS.Negocio.SinConexion;
using MiPOS.Seguridad;
using MiPOS.WPF;
using MiPOS.WPF.Idiomas;
using MiPOS.WPF.ViewModels;
using MiPOS.WPF.Vistas;

/// <summary>
/// Prueba de punta a punta: abre las ventanas reales de MiPOS y las usa como un cajero o un administrador
/// (ejecuta los mismos comandos que las teclas F1, F3, F11...), contesta cada diálogo y cada MessageBox,
/// y comprueba el resultado. Trabaja contra la base de desarrollo: deja ventas, productos y usuarios de prueba.
/// Cada modo corre en su propio proceso (WPF permite una sola aplicación por proceso): ver Program.cs.
/// </summary>
public static class Robot
{
    static int _errores;
    static readonly List<string> _mensajesWindows = new();
    static readonly Dictionary<string, string> _valores = new();
    static readonly Dictionary<Type, Func<Window, Task>> _respuestas = new();

    static void Ok(string que, bool cumple, string detalle = "")
    {
        if (!cumple) _errores++;
        Console.WriteLine($"{(cumple ? "OK   " : "ERROR")} {que}{(detalle.Length > 0 ? " — " + detalle : "")}");
    }

    static string _modo = "completo";

    public static int Ejecutar(string idioma, string modo = "completo")
    {
        _modo = modo;
        var hilo = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.DispatcherUnhandledException += (_, e) => { Ok("Excepción no controlada en la pantalla", false, e.Exception.ToString()); e.Handled = true; };
            PresentationTraceSourcesListener.Activar();

            // El mismo appsettings.json de MiPOS.WPF (se copia junto a este programa al compilar)
            var constructor = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json");
            // Sin conexión: la base central es un servidor que no existe
            if (modo == "sinconexion")
                constructor.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MiPOS"] = "Server=tcp:127.0.0.1,1;Database=MiPOS;Connect Timeout=3;TrustServerCertificate=True"
                });
            // Facturación simulada como Responsable Inscripto (para probar la factura A)
            if (modo is "cajero")
                constructor.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Facturacion:Modo"] = "Simulada",
                    ["Facturacion:CondicionIva"] = "ResponsableInscripto",
                    ["Facturacion:CarpetaDatos"] = Path.Combine(Path.GetTempPath(), "mipos-robot-arca"),
                    ["Comercio:Cuit"] = "33-69345023-9"
                });
            var configuracion = constructor.Build();
            var servicios = new ServiceCollection();
            App.RegistrarServicios(servicios, configuracion);
            var proveedor = servicios.BuildServiceProvider();
            proveedor.GetRequiredService<BaseLocal>().Preparar();

            Traductor.Usar(idioma);
            TraduccionVentanas.Activar();
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
            {
                var ventana = (Window)s;
                if (ventana is CajaVentana) return;
                var tipo = ventana.DataContext?.GetType();
                if (tipo is not null && _respuestas.TryGetValue(tipo, out var respuesta))
                    ventana.Dispatcher.BeginInvoke(DispatcherPriority.Background, async () =>
                    {
                        try { await respuesta(ventana); }
                        catch (Exception ex) { Ok($"Respuesta a {tipo.Name}", false, ex.ToString()); if (ventana.IsVisible) ventana.Close(); }
                    });
                else
                {
                    Ok($"Diálogo inesperado: {ventana.Title} ({tipo?.Name})", false);
                    ventana.Dispatcher.BeginInvoke(DispatcherPriority.Background, ventana.Close);
                }
            }));

            if (modo is not ("sinconexion" or "cajero" or "admin"))
                using (var contexto = proveedor.GetRequiredService<IDbContextFactory<MiPOSDbContext>>().CreateDbContext())
                    proveedor.GetRequiredService<Sesion>().Iniciar(contexto.Usuarios.AsNoTracking().First(u => u.Activo && u.Rol == Rol.Administrador));

            var botonera = new Thread(ContestarMensajesDeWindows) { IsBackground = true };
            botonera.Start();

            Dispatcher.CurrentDispatcher.BeginInvoke(async () =>
            {
                try { await Guion(proveedor); }
                catch (Exception ex) { Ok("Guion", false, ex.ToString()); }
                Console.WriteLine($"Errores de Binding: {PresentationTraceSourcesListener.Errores.Count}");
                foreach (var e in PresentationTraceSourcesListener.Errores.Distinct().Take(15)) Console.WriteLine("   " + e);
                Console.WriteLine($"Mensajes de Windows contestados: {string.Join(" | ", _mensajesWindows)}");
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            });
            Dispatcher.Run();
        });
        hilo.SetApartmentState(ApartmentState.STA);
        hilo.Start();
        hilo.Join();
        Console.WriteLine($"\nTOTAL: {_errores} errores");
        return _errores;
    }

    static async Task Esperar(Func<bool> condicion, int ms = 10000)
    {
        var reloj = Stopwatch.StartNew();
        while (!condicion() && reloj.ElapsedMilliseconds < ms)
            await Task.Delay(50);
    }

    static async Task Guion(IServiceProvider proveedor)
    {
        // ----- respuestas a los diálogos -----
        _valores["Abrir caja"] = "1000";
        _valores["Cambiar cantidad"] = "3";
        _valores["Descuento"] = "10";
        _respuestas[typeof(IngresarValorViewModel)] = async v =>
        {
            var vm = (IngresarValorViewModel)v.DataContext;
            vm.Texto = _valores.TryGetValue(vm.Titulo, out var t) ? t : "";
            Ok($"Diálogo \"{vm.Titulo}\" se abre con su título traducido", Traductor.EsEspanol || v.Title != vm.Titulo, v.Title);
            vm.AceptarCommand.Execute(null);
            await Task.CompletedTask;
        };
        string? pagaCon = "10000";
        _respuestas[typeof(CobroViewModel)] = async v =>
        {
            var vm = (CobroViewModel)v.DataContext;
            await Esperar(() => vm.MediosPago.Count > 0);
            Ok("Cobro: se cargan los medios de pago", vm.MediosPago.Count >= 6, $"{vm.MediosPago.Count}");
            vm.PagaCon = pagaCon!;
            await vm.ConfirmarCommand.ExecuteAsync(null);
            if (v.IsVisible) { Ok("Cobro: la venta se registró", false, vm.Mensaje); v.Close(); }
        };
        _respuestas[typeof(MovimientoCajaViewModel)] = async v =>
        {
            var vm = (MovimientoCajaViewModel)v.DataContext;
            vm.TipoSeleccionado = vm.Tipos[_valores["tipoMovimiento"] == "ingreso" ? 1 : 0];
            vm.Monto = _valores["montoMovimiento"];
            vm.Motivo = "Prueba del robot";
            await vm.ConfirmarCommand.ExecuteAsync(null);
            if (v.IsVisible) { Ok("Movimiento de caja registrado", false, vm.Mensaje); v.Close(); }
        };
        _respuestas[typeof(CierreCajaViewModel)] = async v =>
        {
            var vm = (CierreCajaViewModel)v.DataContext;
            vm.EfectivoContado = "1000";
            await vm.ConfirmarCommand.ExecuteAsync(null);
            Ok("Cierre: muestra el resumen con la diferencia", vm.Cerrada && vm.TextoDiferencia.Length > 0, vm.Cerrada ? vm.TextoDiferencia : vm.Mensaje);
            await Task.Delay(300);
            vm.ConfirmarCommand.Execute(null);
            if (v.IsVisible) v.Close();
        };
        Producto? creado = null;
        _respuestas[typeof(ProductoEdicionViewModel)] = async v =>
        {
            var vm = (ProductoEdicionViewModel)v.DataContext;
            await Esperar(() => vm.Categorias.Count > 0);
            await vm.GenerarCodigoInternoCommand.ExecuteAsync(null);
            Ok("Producto nuevo: genera el código interno", vm.CodigoBarras.StartsWith("INT"), vm.CodigoBarras);
            vm.Nombre = $"Producto del robot {DateTime.Now:HHmmss}";
            vm.CategoriaSeleccionada = vm.Categorias[0];
            vm.Precio = "1234,50";
            vm.StockInicial = "5";
            vm.StockMinimo = "2";
            await vm.GuardarCommand.ExecuteAsync(null);
            creado = vm.ProductoGuardado;
            Ok("Producto nuevo: se guarda", creado is not null, vm.Mensaje);
            if (v.IsVisible) v.Close();
        };
        _respuestas[typeof(ProductosViewModel)] = async v =>
        {
            var vm = (ProductosViewModel)v.DataContext;
            await Esperar(() => vm.Productos.Count > 0);
            Ok("Ctrl+P: lista los productos", vm.Productos.Count > 0, $"{vm.Productos.Count}");
            await vm.NuevoCommand.ExecuteAsync(null);
            await Task.Delay(300);
            v.Close();
        };
        _respuestas[typeof(ComprasViewModel)] = async v => { await Task.Delay(1500); Ok("Ctrl+M: abre compras", string.IsNullOrEmpty(((ComprasViewModel)v.DataContext).Mensaje) || ((ComprasViewModel)v.DataContext).Mensaje.StartsWith("Todavía"), ((ComprasViewModel)v.DataContext).Mensaje); v.Close(); };
        _respuestas[typeof(UsuariosViewModel)] = async v => { await Task.Delay(1500); Ok("F9: abre usuarios", ((UsuariosViewModel)v.DataContext).Usuarios.Count > 0); v.Close(); };
        _respuestas[typeof(HardwareViewModel)] = async v => { var vm = (HardwareViewModel)v.DataContext; await vm.ImprimirPruebaCommand.ExecuteAsync(null); Ok("Ctrl+H: ticket de prueba", vm.MensajeExito.Length > 0, vm.Mensaje); v.Close(); };
        _respuestas[typeof(LicenciaViewModel)] = async v => { await Task.Delay(300); Ok("Ctrl+L: muestra el código de instalación", ((LicenciaViewModel)v.DataContext).CodigoInstalacion.Length == 19); v.Close(); };

        if (_modo == "abrir")
        {
            try { await proveedor.GetRequiredService<MiPOS.Negocio.Servicios.UsuarioServicio>().CrearAsync("robot", "Cajero de prueba", "Robot123", Rol.Cajero); }
            catch (MiPOS.Negocio.Usuarios.UsuarioInvalidoException) { } // ya existía
            try { await proveedor.GetRequiredService<MiPOS.Negocio.Servicios.UsuarioServicio>().CrearAsync("robotadmin", "Administrador de prueba", "Robot123", Rol.Administrador); }
            catch (MiPOS.Negocio.Usuarios.UsuarioInvalidoException) { }
        }

        // ----- inicio de sesión real (sin conexión: con la copia local de los usuarios) -----
        if (_modo is "sinconexion" or "cajero" or "admin")
        {
            var login = proveedor.GetRequiredService<LoginViewModel>();
            login.NombreUsuario = _modo == "admin" ? "robotadmin" : "robot";
            login.Contrasena = "Robot123";
            var reloj = Stopwatch.StartNew();
            login.Iniciar();
            await Esperar(() => login.Mensaje.Length == 0 || login.Mensaje.StartsWith("No se pudo"), 20000);
            Ok("Inicio de sesión: encuentra los usuarios", login.Mensaje.Length == 0, $"{login.Mensaje} ({reloj.ElapsedMilliseconds} ms)");
            await login.IngresarCommand.ExecuteAsync(null);
            Ok("Inicio de sesión con usuario y contraseña", proveedor.GetRequiredService<Sesion>().HaySesion, login.Mensaje);
            if (!proveedor.GetRequiredService<Sesion>().HaySesion) return;
        }

        // ----- la caja -----
        var caja = proveedor.GetRequiredService<CajaVentana>();
        var vm = (CajaViewModel)caja.DataContext;
        caja.WindowState = WindowState.Normal;
        caja.Show();
        await Esperar(() => vm.CajaAbierta && !vm.CargarCajaCommand.IsRunning, 20000);
        Ok("La caja queda abierta", vm.CajaAbierta, vm.Mensaje);

        async Task Escanear(string codigo) { vm.CodigoIngresado = codigo; await vm.AgregarPorCodigoCommand.ExecuteAsync(null); }

        if (_modo == "abrir")
        {
            await Task.Delay(3000); // que termine la primera sincronización (descarga la copia local)
            caja.Close();
            return;
        }

        if (_modo == "sinconexion")
        {
            await Esperar(() => vm.HayAvisoConexion, 15000);
            Ok("Se ve el aviso SIN CONEXIÓN", vm.AvisoConexion.StartsWith("SIN CONEXIÓN"), vm.AvisoConexion);
            var reloj = Stopwatch.StartNew();
            await Escanear("7791234");
            Ok("Sin conexión: escanear es inmediato (no espera la base central)", vm.Items.Count == 1 && reloj.ElapsedMilliseconds < 1000, $"{reloj.ElapsedMilliseconds} ms / {vm.Mensaje}");
            await Escanear("7795678");
            pagaCon = "5000";
            var desde = DateTime.Now;
            vm.CobrarCommand.Execute(null);
            Ok("Sin conexión: la venta se registra en la caja", vm.MensajeExito.StartsWith("Venta S/C N°") && vm.MensajeExito.Contains("Vuelto: $ 1.000,00"), vm.MensajeExito + " / " + vm.Mensaje);
            await Task.Delay(1500);
            var tk = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Tickets")).Select(f => new FileInfo(f)).Where(f => f.LastWriteTime >= desde.AddSeconds(-1)).OrderBy(f => f.LastWriteTime).LastOrDefault();
            var txt = tk is null ? "" : File.ReadAllText(tk.FullName);
            Ok("Sin conexión: el ticket dice Venta S/C", txt.Contains("Venta S/C N°") && txt.Contains("Registrada sin conexión"), tk?.Name ?? "no hay ticket");
            await vm.ReimprimirUltimoTicketCommand.ExecuteAsync(null);
            Ok("Sin conexión: F8 reimprime la venta local", vm.MensajeExito.Contains("reimpreso"), vm.MensajeExito + " / " + vm.Mensaje);
            Ok("El aviso cuenta la venta sin subir", vm.AvisoConexion.Contains("1 venta sin subir"), vm.AvisoConexion);
            await vm.AbrirOCerrarCajaCommand.ExecuteAsync(null);
            Ok("Sin conexión no se cierra la caja", vm.CajaAbierta && vm.Mensaje.Contains("conexión"), vm.Mensaje);
            caja.Close();
            return;
        }

        if (_modo == "cajero")
        {
            _respuestas[typeof(BuscarProductoViewModel)] = async v =>
            {
                var b = (BuscarProductoViewModel)v.DataContext;
                b.TextoBusqueda = "ARR";
                await Esperar(() => b.Resultados.Count > 0, 5000);
                Ok("F1 busca por nombre", b.Resultados.Count > 0, b.Mensaje);
                b.ProductoSeleccionado = b.Resultados.FirstOrDefault();
                b.ElegirCommand.Execute(null);
            };
            _respuestas[typeof(AutorizacionViewModel)] = async v =>
            {
                var a = (AutorizacionViewModel)v.DataContext;
                a.NombreUsuario = "robotadmin"; a.Contrasena = "Robot123";
                await a.AutorizarCommand.ExecuteAsync(null);
                Ok($"Autorización de administrador: {a.Motivo}", a.Autorizante is not null, a.Mensaje);
                if (v.IsVisible) v.Close();
            };
            _respuestas[typeof(ClienteFacturaViewModel)] = async v =>
            {
                var c = (ClienteFacturaViewModel)v.DataContext;
                c.Documento = "33-69345023-9"; c.Nombre = "Cliente de prueba SA";
                c.CondicionSeleccionada = c.Condiciones[1];
                Ok("F2: el cliente inscripto recibe factura A", c.TextoComprobante.EndsWith("Factura A"), c.TextoComprobante);
                c.AceptarCommand.Execute(null);
                await Task.CompletedTask;
                if (v.IsVisible) { Ok("F2: se acepta el cliente", false, c.Mensaje); v.Close(); }
            };
            _respuestas[typeof(CobroViewModel)] = async v =>
            {
                var c = (CobroViewModel)v.DataContext;
                await Esperar(() => c.MediosPago.Count > 0);
                c.IdentificarClienteCommand.Execute(null);
                Ok("El cobro muestra la factura que se va a emitir", c.TextoComprobante.Contains("Factura A") && c.TextoComprobante.Contains("Cliente de prueba"), c.TextoComprobante);
                c.PagaCon = "5000";
                await c.ConfirmarCommand.ExecuteAsync(null);
                if (v.IsVisible) { Ok("Cobro con factura", false, c.Mensaje); v.Close(); }
            };
            _respuestas[typeof(FacturasPendientesViewModel)] = async v => { await Task.Delay(1500); var f = (FacturasPendientesViewModel)v.DataContext; Ok("Ctrl+F abre las facturas sin autorizar", f.Mensaje.Length == 0 || f.Mensaje.StartsWith("No hay"), f.Mensaje); v.Close(); };
            _respuestas[typeof(CambiarContrasenaViewModel)] = async v =>
            {
                var c = (CambiarContrasenaViewModel)v.DataContext;
                var (actual, nueva) = _valores["contrasena"] == "ida" ? ("Robot123", "Robot456") : ("Robot456", "Robot123");
                c.ContrasenaActual = actual; c.ContrasenaNueva = nueva; c.ConfirmarContrasena = nueva;
                await c.GuardarCommand.ExecuteAsync(null);
                if (v.IsVisible) { Ok("F10 cambia la contraseña", false, c.Mensaje); v.Close(); }
            };

            vm.BuscarProductoCommand.Execute(null);
            Ok("F1 agrega el producto elegido", vm.Items.Count == 1, vm.Mensaje);
            _valores["Descuento"] = "5";
            vm.AplicarDescuentoCommand.Execute(null);
            Ok("F6 de un cajero pide autorización y aplica el descuento", vm.TieneDescuento, vm.Mensaje);
            var desde = DateTime.Now;
            vm.CobrarCommand.Execute(null);
            Ok("Cobro con factura registrado", vm.MensajeExito.Contains("registrada"), vm.MensajeExito + " / " + vm.Mensaje);
            await Task.Delay(2500);
            var tk = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Tickets")).Select(f => new FileInfo(f)).Where(f => f.LastWriteTime >= desde.AddSeconds(-1)).OrderBy(f => f.LastWriteTime).LastOrDefault();
            var txt = tk is null ? "" : File.ReadAllText(tk.FullName);
            Ok("El ticket es una factura A con CAE", txt.Contains("FACTURA A") && txt.Contains("CAE") && txt.Contains("Neto gravado"), tk?.Name ?? "no hay ticket");
            Ok("Sin facturas pendientes", vm.AvisoFacturas.Length == 0, vm.AvisoFacturas + " / " + vm.Mensaje);
            await vm.VerFacturasPendientesCommand.ExecuteAsync(null);
            Ok("Un cajero no puede entrar a productos ni compras", !vm.GestionarProductosCommand.CanExecute(null) && !vm.GestionarComprasCommand.CanExecute(null) && !vm.GestionarUsuariosCommand.CanExecute(null));
            _valores["tipoMovimiento"] = "retiro"; _valores["montoMovimiento"] = "100";
            vm.RegistrarMovimientoCajaCommand.Execute(null);
            Ok("F7 retiro de un cajero con autorización", vm.MensajeExito.Contains("Movimiento"), vm.MensajeExito + " / " + vm.Mensaje);
            _valores["contrasena"] = "ida";
            vm.CambiarMiContrasenaCommand.Execute(null);
            Ok("F10 cambia la contraseña", vm.MensajeExito.Contains("Contraseña cambiada"), vm.MensajeExito + " / " + vm.Mensaje);
            _valores["contrasena"] = "vuelta";
            vm.CambiarMiContrasenaCommand.Execute(null);
            caja.Close();
            return;
        }

        if (_modo == "admin")
        {
            Proveedor? proveedorNuevo = null;
            _respuestas[typeof(ProveedorEdicionViewModel)] = async v =>
            {
                var p = (ProveedorEdicionViewModel)v.DataContext;
                p.Nombre = $"Proveedor del robot {DateTime.Now:HHmmss}"; p.Cuit = "33-69345023-9"; p.Telefono = "1144445555";
                await p.GuardarCommand.ExecuteAsync(null);
                proveedorNuevo = p.ProveedorGuardado;
                Ok("Proveedor nuevo con CUIT guardado sin guiones", proveedorNuevo?.Cuit == "33693450239", p.Mensaje);
                if (v.IsVisible) v.Close();
            };
            _respuestas[typeof(ProveedoresViewModel)] = async v => { var p = (ProveedoresViewModel)v.DataContext; await Task.Delay(1000); await p.NuevoCommand.ExecuteAsync(null); await Task.Delay(500); v.Close(); };
            _respuestas[typeof(CompraItemViewModel)] = async v => { var c = (CompraItemViewModel)v.DataContext; c.Cantidad = "10"; c.Costo = "1500"; c.AceptarCommand.Execute(null); await Task.CompletedTask; if (v.IsVisible) { Ok("Ítem de compra", false, c.Mensaje); v.Close(); } };
            _respuestas[typeof(CompraViewModel)] = async v =>
            {
                var c = (CompraViewModel)v.DataContext;
                await Esperar(() => c.Proveedores.Count > 0);
                c.ProveedorSeleccionado = c.Proveedores.FirstOrDefault(x => x.Id == proveedorNuevo?.Id) ?? c.Proveedores[0];
                c.NumeroComprobante = "0001-00001234";
                c.CodigoIngresado = "7791234";
                await c.AgregarPorCodigoCommand.ExecuteAsync(null);
                Ok("La compra agrega el producto escaneado", c.Items.Count == 1, c.Mensaje);
                await c.RegistrarCommand.ExecuteAsync(null);
                Ok("Se registra la compra", c.CompraRegistrada is not null, c.Mensaje);
                if (v.IsVisible) v.Close();
            };
            _respuestas[typeof(ComprasViewModel)] = async v =>
            {
                var c = (ComprasViewModel)v.DataContext;
                c.GestionarProveedoresCommand.Execute(null);
                await c.NuevaCompraCommand.ExecuteAsync(null);
                Ok("La lista de compras muestra la compra nueva", c.MensajeExito.Contains("registrada"), c.MensajeExito + " / " + c.Mensaje);
                v.Close();
            };
            _respuestas[typeof(AjusteStockViewModel)] = async v => { var a = (AjusteStockViewModel)v.DataContext; a.TipoSeleccionado = a.Tipos[1]; a.Cantidad = "1"; a.Motivo = "Rotura (robot)"; await a.ConfirmarCommand.ExecuteAsync(null); if (v.IsVisible) { Ok("Ajuste de stock", false, a.Mensaje); v.Close(); } };
            _respuestas[typeof(MovimientosStockViewModel)] = async v => { var m = (MovimientosStockViewModel)v.DataContext; await Esperar(() => m.Movimientos.Count > 0, 5000); Ok("Movimientos de stock: muestra compra y ajuste", m.Movimientos.Any(x => x.Tipo == TipoMovimientoStock.Compra) && m.Movimientos.Any(x => x.Tipo == TipoMovimientoStock.Ajuste), m.Mensaje); v.Close(); };
            _respuestas[typeof(ProductosViewModel)] = async v =>
            {
                var p = (ProductosViewModel)v.DataContext;
                await Esperar(() => p.Productos.Count > 0);
                p.ProductoSeleccionado = p.Productos.First(x => x.CodigoBarras == "7791234");
                var antes = p.ProductoSeleccionado.Stock;
                await p.AjustarStockCommand.ExecuteAsync(null);
                await Esperar(() => p.Productos.Any(x => x.CodigoBarras == "7791234" && x.Stock == antes - 1), 3000);
                Ok("El ajuste resta 1 unidad y la lista se actualiza", p.Productos.Any(x => x.CodigoBarras == "7791234" && x.Stock == antes - 1), p.Mensaje);
                p.ProductoSeleccionado = p.Productos.First(x => x.CodigoBarras == "7791234");
                p.VerMovimientosCommand.Execute(null);
                v.Close();
            };
            _respuestas[typeof(UsuarioEdicionViewModel)] = async v => { var u = (UsuarioEdicionViewModel)v.DataContext; u.IdiomaSeleccionado = "zh"; await u.GuardarCommand.ExecuteAsync(null); if (v.IsVisible) { Ok("Editar usuario (idioma)", false, u.Mensaje); v.Close(); } };
            _respuestas[typeof(UsuariosViewModel)] = async v =>
            {
                var u = (UsuariosViewModel)v.DataContext;
                await Esperar(() => u.Usuarios.Count > 0);
                u.UsuarioSeleccionado = u.Usuarios.First(x => x.NombreUsuario == "robot");
                await u.EditarCommand.ExecuteAsync(null);
                Ok("F9: se guarda el idioma del usuario", u.Usuarios.First(x => x.NombreUsuario == "robot").Idioma == "zh", u.Mensaje);
                v.Close();
            };

            await vm.GestionarComprasCommand.ExecuteAsync(null);
            await vm.GestionarProductosCommand.ExecuteAsync(null);
            vm.GestionarUsuariosCommand.Execute(null);
            caja.Close();
            return;
        }

        if (_modo == "cerrar")
        {
            await Esperar(() => vm.MensajeExito.Contains("Volvió la conexión"), 15000);
            Ok("Al volver la conexión sube la venta y avisa", vm.MensajeExito.Contains("se subió 1 venta"), vm.MensajeExito + " / " + vm.Mensaje + " / " + vm.AvisoConexion);
            Ok("Sin ventas pendientes no hay aviso", !vm.HayAvisoConexion, vm.AvisoConexion);
            await vm.ReimprimirUltimoTicketCommand.ExecuteAsync(null);
            Ok("F8 después de subir reimprime con el número real", vm.MensajeExito.Contains("reimpreso"), vm.MensajeExito + " / " + vm.Mensaje);
            await vm.AbrirOCerrarCajaCommand.ExecuteAsync(null);
            Ok("Con todo subido, F11 cierra la caja", !vm.CajaAbierta, vm.MensajeExito + " / " + vm.Mensaje);
            caja.Close();
            return;
        }

        await Escanear("7791234");
        await Escanear(" 7791234 ");
        Ok("Escanear dos veces suma cantidad", vm.Items.Count == 1 && vm.Items[0].Cantidad == 2 && vm.Total == 5000m, $"{vm.Items.Count} ítems, total {vm.Total}");
        await Escanear("0000000");
        Ok("Código inexistente avisa", vm.Mensaje.Contains("no encontrado"), vm.Mensaje);
        await Escanear("7795678");
        vm.CambiarCantidadCommand.Execute(null);
        Ok("F5 cambia la cantidad", vm.Items.Count == 2 && vm.Items[1].Cantidad == 3 && vm.Total == 9500m, $"total {vm.Total}");
        vm.AplicarDescuentoCommand.Execute(null);
        Ok("F6 aplica 10% de descuento", vm.Total == 8550m && vm.TieneDescuento, $"total {vm.Total}");
        vm.SeleccionarAnteriorCommand.Execute(null);
        Ok("Flecha arriba mueve la selección", vm.ItemSeleccionado == vm.Items[0]);

        var antes = DateTime.Now;
        vm.CobrarCommand.Execute(null);
        Ok("F3 cobra y muestra el vuelto", vm.MensajeExito.Contains("Vuelto: $ 1.450,00") && vm.Items.Count == 0, vm.MensajeExito + " / " + vm.Mensaje);
        await Task.Delay(1500);
        var ticket = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Tickets")).Select(f => new FileInfo(f)).Where(f => f.LastWriteTime >= antes.AddSeconds(-1)).OrderBy(f => f.LastWriteTime).LastOrDefault();
        var textoTicket = ticket is null ? "" : File.ReadAllText(ticket.FullName);
        Ok("Se imprime el ticket con el descuento y el vuelto", textoTicket.Contains("Descuento 10%") && textoTicket.Contains("Vuelto"), ticket?.Name ?? "no hay ticket");
        Ok("El ticket sale en español aunque la pantalla esté en otro idioma", textoTicket.Contains("TOTAL"));

        await vm.ReimprimirUltimoTicketCommand.ExecuteAsync(null);
        Ok("F8 reimprime", vm.MensajeExito.Contains("reimpreso"), vm.MensajeExito + " / " + vm.Mensaje);

        await Escanear("7791234");
        vm.CancelarVentaCommand.Execute(null);
        Ok("ESC cancela la venta (con confirmación)", vm.Items.Count == 0);

        _valores["tipoMovimiento"] = "ingreso"; _valores["montoMovimiento"] = "500";
        vm.RegistrarMovimientoCajaCommand.Execute(null);
        Ok("F7 ingreso de efectivo", vm.MensajeExito.Contains("Movimiento"), vm.MensajeExito + " / " + vm.Mensaje);
        _valores["tipoMovimiento"] = "retiro"; _valores["montoMovimiento"] = "200";
        vm.RegistrarMovimientoCajaCommand.Execute(null);
        Ok("F7 retiro de efectivo (el administrador autoriza solo)", vm.MensajeExito.Contains("Movimiento"), vm.MensajeExito + " / " + vm.Mensaje);

        await vm.GestionarProductosCommand.ExecuteAsync(null);
        if (creado is not null)
        {
            await Escanear(creado.CodigoBarras.ToLowerInvariant());
            Ok("El producto nuevo se vende (código en minúsculas)", vm.Items.Count == 1 && vm.Total == 1234.50m, $"{vm.Items.Count} / {vm.Total} / {vm.Mensaje}");
            pagaCon = "1234,50";
            vm.CobrarCommand.Execute(null);
            Ok("Cobro exacto sin vuelto", vm.MensajeExito.Contains("Vuelto: $ 0,00") || vm.MensajeExito.Contains("registrada"), vm.MensajeExito + " / " + vm.Mensaje);
        }
        await vm.GestionarComprasCommand.ExecuteAsync(null);
        vm.GestionarUsuariosCommand.Execute(null);
        vm.ProbarHardwareCommand.Execute(null);
        vm.GestionarLicenciaCommand.Execute(null);

        await vm.AbrirOCerrarCajaCommand.ExecuteAsync(null);
        Ok("F11 cierra la caja", !vm.CajaAbierta && vm.MensajeExito.Contains("Caja cerrada"), vm.MensajeExito + " / " + vm.Mensaje);
        await Escanear("7791234");
        Ok("Con la caja cerrada no se puede escanear", vm.Items.Count == 0 && vm.Mensaje.Contains("cerrada"), vm.Mensaje);

        var cerrada = false;
        vm.SesionCerrada += () => cerrada = true;
        vm.CerrarSesionCommand.Execute(null);
        Ok("F12 cierra la sesión", cerrada);
        caja.Close();
        await Task.Delay(300);
    }

    // ----- MessageBox de Windows: se contesta "Sí" (o "Aceptar") -----
    delegate bool Enumerador(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] static extern bool EnumWindows(Enumerador f, IntPtr p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr h, int id);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);

    static void ContestarMensajesDeWindows()
    {
        var pid = (uint)Environment.ProcessId;
        while (true)
        {
            Thread.Sleep(250);
            EnumWindows((h, _) =>
            {
                GetWindowThreadProcessId(h, out var p);
                var clase = new StringBuilder(64); GetClassName(h, clase, 64);
                if (p != pid || clase.ToString() != "#32770" || !IsWindowVisible(h)) return true;
                var texto = new StringBuilder(512); GetWindowText(GetDlgItem(h, 0xFFFF), texto, 512);
                var boton = GetDlgItem(h, 6); if (boton == IntPtr.Zero) boton = GetDlgItem(h, 1);
                lock (_mensajesWindows) { if (!_mensajesWindows.Contains(texto.ToString())) _mensajesWindows.Add(texto.ToString().Replace("\n", " ")); }
                PostMessage(boton, 0x00F5, IntPtr.Zero, IntPtr.Zero); // BM_CLICK
                return true;
            }, IntPtr.Zero);
        }
    }
}

// Junta los errores de Binding de WPF (propiedades mal escritas, tipos que no coinciden)
public sealed class PresentationTraceSourcesListener : System.Diagnostics.TraceListener
{
    public static readonly List<string> Errores = new();
    public static void Activar()
    {
        System.Diagnostics.PresentationTraceSources.Refresh();
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(new PresentationTraceSourcesListener());
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Error;
    }
    public override void Write(string? message) { }
    public override void WriteLine(string? message) { if (message is not null) lock (Errores) Errores.Add(message); }
}
