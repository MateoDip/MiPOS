using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiPOS.Dominio.Entidades;
using MiPOS.Facturacion;
using MiPOS.Hardware;
using MiPOS.Hardware.Cajon;
using MiPOS.Hardware.Impresion;
using MiPOS.Hardware.Lector;
using MiPOS.Negocio.Cajas;
using MiPOS.Negocio.Facturacion;
using MiPOS.Negocio.Servicios;
using MiPOS.Negocio.Ventas;
using MiPOS.Seguridad;
using MiPOS.WPF.Servicios;

namespace MiPOS.WPF.ViewModels;

// "partial" es obligatorio: el Toolkit completa la clase con código generado
public partial class CajaViewModel : ObservableObject
{
    private const string MensajeCantidadInvalida = "Cantidad inválida: ingrese un número entero mayor a cero.";

    private const string MensajeCajaCerrada = "La caja está cerrada. Presione F11 para abrirla.";

    private const string MensajeDescuentoInvalido = "Descuento inválido: ingrese un porcentaje mayor a 0 y hasta 100, con coma para los decimales (ej: 10 o 12,5).";

    private readonly ProductoServicio _productoServicio;
    private readonly UsuarioServicio _usuarioServicio;
    private readonly VentaServicio _ventaServicio;
    private readonly TicketServicio _ticketServicio;
    private readonly CajaServicio _cajaServicio;
    private readonly StockServicio _stockServicio;
    private readonly FacturacionServicio _facturacionServicio;
    private readonly IImpresoraTicket _impresora;
    private readonly CajonDinero _cajon;
    private readonly LectorCodigos _lector;
    private readonly DialogoServicio _dialogos;
    private readonly Sesion _sesion;
    private readonly Carrito _carrito = new();

    public CajaViewModel(ProductoServicio productoServicio, UsuarioServicio usuarioServicio,
                         VentaServicio ventaServicio, TicketServicio ticketServicio, CajaServicio cajaServicio,
                         StockServicio stockServicio, FacturacionServicio facturacionServicio,
                         IImpresoraTicket impresora, CajonDinero cajon, LectorCodigos lector,
                         DialogoServicio dialogos, Sesion sesion)
    {
        _productoServicio = productoServicio;
        _usuarioServicio = usuarioServicio;
        _ventaServicio = ventaServicio;
        _ticketServicio = ticketServicio;
        _cajaServicio = cajaServicio;
        _stockServicio = stockServicio;
        _facturacionServicio = facturacionServicio;
        _impresora = impresora;
        _cajon = cajon;
        _lector = lector;
        _dialogos = dialogos;
        _sesion = sesion;
    }

    // Avisa a la ventana que terminó una acción, para que devuelva el cursor al campo de código
    public event Action? AccionTerminada;

    // Avisa a la ventana que se cerró la sesión, para que se cierre y vuelva el inicio de sesión
    public event Action? SesionCerrada;

    // Ej: "Administrador: Juan Pérez"
    public string UsuarioTexto => _sesion.UsuarioActual is { } usuario
        ? $"{usuario.Rol}: {usuario.NombreCompleto}"
        : string.Empty;

    public ReadOnlyObservableCollection<ItemCarrito> Items => _carrito.Items;
    public decimal Subtotal => _carrito.Subtotal;
    public decimal Descuento => _carrito.Descuento;
    public bool TieneDescuento => _carrito.TieneDescuento;
    public string TextoDescuento => $"Descuento {_carrito.DescuentoPorcentaje:0.##}%: - $ {_carrito.Descuento:N2}";
    public decimal Total => _carrito.Total;
    public int CantidadArticulos => _carrito.CantidadArticulos;

    // Turno abierto de la caja de esta PC (null = caja cerrada)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CajaAbierta))]
    [NotifyPropertyChangedFor(nameof(CajaTexto))]
    [NotifyPropertyChangedFor(nameof(TextoBotonCaja))]
    [NotifyCanExecuteChangedFor(nameof(RegistrarMovimientoCajaCommand))]
    private TurnoCaja? turno;

    // Nombre de la caja de esta PC (se completa al cargar)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CajaTexto))]
    private string nombreCaja = string.Empty;

    public bool CajaAbierta => Turno is not null;

    // Ej: "Caja 1 · Abierta desde 08:15" o "Caja 1 · CERRADA"
    public string CajaTexto => Turno is { } turnoAbierto
        ? $"{NombreCaja} · Abierta desde {turnoAbierto.FechaApertura:HH:mm}"
        : $"{NombreCaja} · CERRADA";

    public string TextoBotonCaja => CajaAbierta ? "F11  Cerrar caja" : "F11  Abrir caja";

    // [ObservableProperty] genera una propiedad pública "CodigoIngresado"
    // que avisa a la pantalla cada vez que cambia
    [ObservableProperty]
    private string codigoIngresado = string.Empty;

    [ObservableProperty]
    private string mensaje = string.Empty;

    // Mensaje en verde (por ejemplo, "Venta registrada"). Se borra al agregar el próximo producto.
    [ObservableProperty]
    private string mensajeExito = string.Empty;

    // Solo para quien gestiona el stock: "3 productos con stock bajo · Ctrl+P" (vacío si no hay)
    [ObservableProperty]
    private string avisoStock = string.Empty;

    // "2 facturas sin autorizar · Ctrl+F" (vacío si no hay o si la facturación está desactivada)
    [ObservableProperty]
    private string avisoFacturas = string.Empty;

    // Cuando cambia el producto seleccionado, se re-evalúa si se puede eliminar o cambiar cantidad
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EliminarCommand))]
    [NotifyCanExecuteChangedFor(nameof(CambiarCantidadCommand))]
    private ItemCarrito? itemSeleccionado;

    // [RelayCommand] genera "AgregarPorCodigoCommand", que la pantalla ejecuta con Enter
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AgregarPorCodigoAsync()
    {
        // Se toma el código y se limpia el campo enseguida,
        // así el cajero puede escanear el siguiente sin esperar
        var codigo = CodigoIngresado;
        CodigoIngresado = string.Empty;

        if (string.IsNullOrWhiteSpace(codigo))
            return;

        if (!CajaAbierta)
        {
            Mensaje = MensajeCajaCerrada;
            return;
        }

        try
        {
            var producto = await _productoServicio.BuscarPorCodigoAsync(codigo);

            if (producto is null)
            {
                Mensaje = $"Producto no encontrado: {codigo.Trim()}";
                return;
            }

            AgregarAlCarrito(producto);
        }
        catch (Exception)
        {
            Mensaje = "Error al buscar el producto. Verifique la conexión con la base de datos.";
        }
    }

    private void AgregarAlCarrito(Producto producto)
    {
        var item = _carrito.Agregar(producto);
        ItemSeleccionado = item;
        MensajeExito = string.Empty;

        // Se vende igual (el producto existe si el cliente lo tiene en la mano),
        // pero se avisa para que alguien revise el stock cargado
        Mensaje = producto.Stock < item.Cantidad
            ? $"Atención: según el sistema hay {producto.Stock} unidades de {producto.Nombre}. Se vende igual; avise al encargado para revisar el stock."
            : string.Empty;

        ActualizarTotales();
    }

    // F1: buscar un producto por nombre y agregarlo
    [RelayCommand]
    private void BuscarProducto()
    {
        if (!CajaAbierta)
        {
            Mensaje = MensajeCajaCerrada;
            AccionTerminada?.Invoke();
            return;
        }

        var producto = _dialogos.BuscarProducto();

        if (producto is not null)
            AgregarAlCarrito(producto);

        AccionTerminada?.Invoke();
    }

    // F3: cobrar la venta
    [RelayCommand(CanExecute = nameof(HayProductos))]
    private void Cobrar()
    {
        // Si todavía se está buscando un producto escaneado, el total podría cambiar
        // con la ventana de cobro abierta. Se pide esperar.
        if (AgregarPorCodigoCommand.IsRunning)
        {
            Mensaje = "Espere a que termine de cargar el último producto.";
            AccionTerminada?.Invoke();
            return;
        }

        // La ventana de cobro registra la venta. Si se cierra sin cobrar, el carrito queda igual.
        var venta = _dialogos.Cobrar(_carrito);

        if (venta is not null)
        {
            _carrito.Vaciar();
            ItemSeleccionado = null;
            CodigoIngresado = string.Empty;
            Mensaje = string.Empty;
            MensajeExito = venta.Vuelto is decimal vuelto
                ? $"Venta N° {venta.Id} registrada. Vuelto: $ {vuelto:N2}"
                : $"Venta N° {venta.Id} registrada.";
            ActualizarTotales();

            // En efectivo se abre el cajón para guardar la plata y dar el vuelto
            if (venta.MontoRecibido is not null)
                _ = AbrirCajonAsync();

            // Se factura e imprime sin esperar: el cajero puede empezar la próxima venta mientras sale el ticket.
            // Si falla, la venta ya está registrada igual y se avisa (factura pendiente, o reimprimir con F8).
            _ = FacturarEImprimirAsync(venta.Id);
            _ = ActualizarAvisoStockAsync();
        }

        AccionTerminada?.Invoke();
    }

    // F8: reimprimir el ticket de la última venta del usuario conectado
    [RelayCommand]
    private async Task ReimprimirUltimoTicketAsync()
    {
        MensajeExito = string.Empty;
        try
        {
            var ventaId = await _ventaServicio.ObtenerUltimaVentaIdAsync();

            if (ventaId is null)
                Mensaje = "No tiene ventas para reimprimir.";
            else
            {
                Mensaje = string.Empty;
                await ImprimirTicketAsync(ventaId.Value, esReimpresion: true);
            }
        }
        catch (Exception)
        {
            Mensaje = "Error al buscar la última venta. Verifique la conexión con la base de datos.";
        }

        AccionTerminada?.Invoke();
    }

    // Pide el CAE de la factura (si la facturación está activa) y después imprime el ticket.
    // Sin conexión con ARCA, la factura queda pendiente y el ticket sale como "pendiente de autorización".
    private async Task FacturarEImprimirAsync(int ventaId)
    {
        if (_facturacionServicio.EstaActiva)
        {
            try
            {
                await _facturacionServicio.AutorizarVentaAsync(ventaId);

                // Hay conexión: se aprovecha para autorizar las que hayan quedado pendientes antes
                _ = AutorizarPendientesEnSegundoPlanoAsync();
            }
            catch (FacturacionException ex)
            {
                Mensaje = $"Venta N° {ventaId} registrada, pero la factura quedó PENDIENTE: {ex.Message} Se puede reintentar con Ctrl+F.";
            }
            catch (FacturaInvalidaException ex)
            {
                Mensaje = $"Venta N° {ventaId} registrada, pero la factura no se emitió: {ex.Message} Avise a un administrador (Ctrl+F).";
            }
            catch (Exception)
            {
                Mensaje = $"Venta N° {ventaId} registrada, pero no se pudo pedir la factura. Reintente con Ctrl+F.";
            }

            await ActualizarAvisoFacturasAsync();
        }

        await ImprimirTicketAsync(ventaId, esReimpresion: false);
    }

    private async Task AutorizarPendientesEnSegundoPlanoAsync()
    {
        try
        {
            var resultado = await _facturacionServicio.AutorizarPendientesAsync();
            if (resultado.Autorizadas > 0)
                await ActualizarAvisoFacturasAsync();
        }
        catch (Exception)
        {
            // Se vuelve a intentar con la próxima venta o desde Ctrl+F
        }
    }

    // Arma el ticket desde la venta guardada y lo manda a la impresora configurada
    private async Task ImprimirTicketAsync(int ventaId, bool esReimpresion)
    {
        try
        {
            var ticket = await _ticketServicio.ArmarAsync(ventaId, _impresora.CaracteresPorLinea, esReimpresion);
            await _impresora.ImprimirAsync(ticket.Lineas, $"Venta {ventaId}", ticket.CodigoQr);

            if (esReimpresion)
                MensajeExito = $"Ticket de la venta N° {ventaId} reimpreso.";
        }
        catch (ImpresionException ex)
        {
            Mensaje = $"No se pudo imprimir el ticket de la venta N° {ventaId}. {ex.Message} Use F8 para reimprimir.";
        }
        catch (Exception)
        {
            Mensaje = $"No se pudo imprimir el ticket de la venta N° {ventaId}. Use F8 para reimprimir.";
        }
    }

    // Al abrir la pantalla: se averigua si la caja está abierta. Si está cerrada, se ofrece abrirla.
    [RelayCommand]
    private async Task CargarCajaAsync()
    {
        try
        {
            NombreCaja = (await _cajaServicio.ObtenerCajaAsync()).Nombre;
            Turno = await _cajaServicio.ObtenerTurnoAbiertoAsync();

            await ActualizarAvisoStockAsync();
            await ActualizarAvisoFacturasAsync();
            IniciarLector();

            if (!CajaAbierta)
                await AbrirCajaAsync();
        }
        catch (CajaInvalidaException ex)
        {
            Mensaje = ex.Message;
        }
        catch (Exception)
        {
            Mensaje = "Error al consultar el estado de la caja. Verifique la conexión con la base de datos.";
        }

        AccionTerminada?.Invoke();
    }

    // F11: abrir la caja si está cerrada, o cerrarla (con arqueo) si está abierta
    [RelayCommand]
    private async Task AbrirOCerrarCajaAsync()
    {
        if (CajaAbierta)
            CerrarCaja();
        else
            await AbrirCajaAsync();

        AccionTerminada?.Invoke();
    }

    private async Task AbrirCajaAsync()
    {
        var texto = _dialogos.PedirMontoInicial();

        // null = se canceló: la caja queda cerrada
        if (texto is null)
        {
            Mensaje = MensajeCajaCerrada;
            return;
        }

        if (!LectorNumeros.IntentarLeerDecimal(texto, out var montoInicial))
        {
            Mensaje = "Monto inválido: ingrese solo números, con coma para los centavos (ej: 10000 o 10000,50). Presione F11 para intentar de nuevo.";
            return;
        }

        try
        {
            Turno = await _cajaServicio.AbrirTurnoAsync(montoInicial);
            _ = AbrirCajonAsync(); // para guardar el cambio inicial
            NombreCaja = Turno.Caja?.Nombre ?? NombreCaja;
            Mensaje = string.Empty;
            MensajeExito = $"Caja abierta con $ {montoInicial:N2}.";
        }
        catch (CajaInvalidaException ex)
        {
            Mensaje = ex.Message;

            // Si otra PC la abrió recién, se sigue con ese turno
            try { Turno = await _cajaServicio.ObtenerTurnoAbiertoAsync(); }
            catch (Exception) { }
        }
        catch (Exception)
        {
            Mensaje = "Error al abrir la caja. Verifique la conexión con la base de datos.";
        }
    }

    private void CerrarCaja()
    {
        if (!_carrito.EstaVacio)
        {
            Mensaje = "Termine o cancele la venta antes de cerrar la caja.";
            return;
        }

        // Se abre el cajón para contar el efectivo
        _ = AbrirCajonAsync();
        var resumen = _dialogos.CerrarCaja();

        // null = volvió sin cerrar
        if (resumen is null)
            return;

        Turno = null;
        Mensaje = string.Empty;
        MensajeExito = "Caja cerrada. Presione F11 para abrir un nuevo turno.";

        // El comprobante del arqueo se imprime sin esperar (igual que el ticket de venta)
        _ = ImprimirCierreAsync(resumen.Turno.Id);
    }

    private async Task ImprimirCierreAsync(int turnoId)
    {
        try
        {
            var lineas = await _ticketServicio.ArmarCierreAsync(turnoId, _impresora.CaracteresPorLinea);
            await _impresora.ImprimirAsync(lineas, $"Cierre de caja {turnoId}");
        }
        catch (ImpresionException ex)
        {
            Mensaje = $"No se pudo imprimir el cierre de caja (turno N° {turnoId}). {ex.Message}";
        }
        catch (Exception)
        {
            Mensaje = $"No se pudo imprimir el cierre de caja (turno N° {turnoId}).";
        }
    }

    // F7: retiro o ingreso de efectivo (solo con la caja abierta)
    [RelayCommand(CanExecute = nameof(CajaAbierta))]
    private void RegistrarMovimientoCaja()
    {
        if (_dialogos.RegistrarMovimientoCaja())
        {
            Mensaje = string.Empty;
            MensajeExito = "Movimiento de efectivo registrado.";
            _ = AbrirCajonAsync();
        }

        AccionTerminada?.Invoke();
    }

    // F4: eliminar el producto seleccionado
    [RelayCommand(CanExecute = nameof(HayItemSeleccionado))]
    private void Eliminar()
    {
        if (ItemSeleccionado is null)
            return;

        var posicion = Items.IndexOf(ItemSeleccionado);
        _carrito.Eliminar(ItemSeleccionado);

        // Se selecciona el siguiente (o el anterior si era el último),
        // así se puede seguir eliminando con F4
        ItemSeleccionado = Items.Count > 0
            ? Items[Math.Min(posicion, Items.Count - 1)]
            : null;

        Mensaje = string.Empty;
        ActualizarTotales();
        AccionTerminada?.Invoke();
    }

    // F5: cambiar la cantidad del producto seleccionado
    [RelayCommand(CanExecute = nameof(HayItemSeleccionado))]
    private void CambiarCantidad()
    {
        var item = ItemSeleccionado;
        if (item is null)
            return;

        var texto = _dialogos.PedirCantidad(item.Cantidad);

        // null significa que el cajero canceló con Esc: no se cambia nada
        if (texto is not null)
        {
            if (!int.TryParse(texto.Trim(), out var cantidad))
            {
                Mensaje = MensajeCantidadInvalida;
            }
            else
            {
                try
                {
                    // La regla "mayor a cero" la controla el Carrito
                    _carrito.CambiarCantidad(item, cantidad);
                    Mensaje = string.Empty;
                    ActualizarTotales();
                }
                catch (ArgumentOutOfRangeException)
                {
                    Mensaje = MensajeCantidadInvalida;
                }
            }
        }

        AccionTerminada?.Invoke();
    }

    // ESC: cancelar la venta completa (pide confirmación)
    [RelayCommand(CanExecute = nameof(HayProductos))]
    private void CancelarVenta()
    {
        if (_carrito.EstaVacio)
            return;

        if (_dialogos.Confirmar("¿Desea cancelar la venta? Se quitarán todos los productos.", "Cancelar venta"))
        {
            _carrito.Vaciar();
            ItemSeleccionado = null;
            CodigoIngresado = string.Empty;
            Mensaje = string.Empty;
            ActualizarTotales();
        }

        AccionTerminada?.Invoke();
    }

    // F6: aplicar, cambiar o quitar un descuento sobre el total
    [RelayCommand(CanExecute = nameof(HayProductos))]
    private void AplicarDescuento()
    {
        var texto = _dialogos.PedirDescuento(_carrito.DescuentoPorcentaje);

        // null significa que se canceló con Esc: no se cambia nada
        if (texto is not null)
        {
            if (!LectorNumeros.IntentarLeerDecimal(texto, out var porcentaje))
            {
                Mensaje = MensajeDescuentoInvalido;
            }
            else if (porcentaje == 0)
            {
                // Quitar el descuento no necesita autorización (el cliente paga más, no menos)
                _carrito.QuitarDescuento();
                Mensaje = string.Empty;
                ActualizarTotales();
            }
            else if (!Carrito.EsPorcentajeDescuentoValido(porcentaje))
            {
                // Se valida antes de pedir la contraseña, para no hacer escribirla en vano
                Mensaje = MensajeDescuentoInvalido;
            }
            else
            {
                var motivo = $"Descuento del {porcentaje:0.##}% sobre $ {_carrito.Subtotal:N2}";
                var autorizante = ObtenerAutorizacion(Permiso.AplicarDescuento, motivo);

                if (autorizante is not null)
                {
                    _carrito.AplicarDescuento(porcentaje, autorizante.Id);
                    Mensaje = string.Empty;
                    ActualizarTotales();
                }
            }
        }

        AccionTerminada?.Invoke();
    }

    // Si el usuario conectado tiene el permiso, autoriza él mismo;
    // si no, se pide usuario y contraseña de alguien que lo tenga. null = no se autorizó.
    private Usuario? ObtenerAutorizacion(Permiso permiso, string motivo)
        => _sesion.Tiene(permiso)
            ? _sesion.UsuarioActual
            : _dialogos.PedirAutorizacion(permiso, motivo);

    // F9: gestionar usuarios (solo quien tiene el permiso)
    [RelayCommand(CanExecute = nameof(PuedeGestionarUsuarios))]
    private void GestionarUsuarios()
    {
        _dialogos.GestionarUsuarios();

        // Si el administrador cambió su propio nombre, se actualiza el encabezado
        OnPropertyChanged(nameof(UsuarioTexto));
        AccionTerminada?.Invoke();
    }

    private bool PuedeGestionarUsuarios() => _sesion.Tiene(Permiso.GestionarUsuarios);

    // Ctrl+P: productos y stock (solo quien tiene el permiso)
    [RelayCommand(CanExecute = nameof(PuedeGestionarProductos))]
    private async Task GestionarProductosAsync()
    {
        _dialogos.GestionarProductos(soloStockBajo: false);
        await ActualizarAvisoStockAsync();
        AccionTerminada?.Invoke();
    }

    private bool PuedeGestionarProductos() => _sesion.Tiene(Permiso.GestionarProductos);

    // Ctrl+M: compras de mercadería (solo quien tiene el permiso)
    [RelayCommand(CanExecute = nameof(PuedeGestionarCompras))]
    private async Task GestionarComprasAsync()
    {
        _dialogos.GestionarCompras();

        // Una compra puede sacar productos de las alertas
        await ActualizarAvisoStockAsync();
        AccionTerminada?.Invoke();
    }

    private bool PuedeGestionarCompras() => _sesion.Tiene(Permiso.GestionarCompras);

    // ----- Hardware -----

    // Abre el cajón (si hay uno configurado). Si falla, se avisa pero no se interrumpe nada.
    private async Task AbrirCajonAsync()
    {
        try
        {
            await _cajon.AbrirAsync();
        }
        catch (HardwareException ex)
        {
            Mensaje = $"No se pudo abrir el cajón: {ex.Message}";
        }
    }

    // Ctrl+G: abrir el cajón sin una venta (por ejemplo, para dar cambio). Necesita autorización.
    [RelayCommand]
    private async Task AbrirCajonManualAsync()
    {
        if (!_cajon.EstaActivo)
        {
            Mensaje = "No hay un cajón de dinero configurado en esta caja (Cajon:Modo en appsettings.json).";
            AccionTerminada?.Invoke();
            return;
        }

        var autorizante = ObtenerAutorizacion(Permiso.AbrirCajon, "Abrir el cajón de dinero sin una venta");
        if (autorizante is not null)
        {
            Mensaje = string.Empty;
            await AbrirCajonAsync();
            if (Mensaje.Length == 0)
                MensajeExito = $"Cajón abierto (autorizó {autorizante.NombreCompleto}).";
        }

        AccionTerminada?.Invoke();
    }

    // Ctrl+H: probar impresora, cajón y lector (solo quien configura el hardware)
    [RelayCommand(CanExecute = nameof(PuedeConfigurarHardware))]
    private void ProbarHardware()
    {
        _dialogos.ProbarHardware();
        AccionTerminada?.Invoke();
    }

    private bool PuedeConfigurarHardware() => _sesion.Tiene(Permiso.ConfigurarHardware);

    // Lector por puerto serie: los códigos llegan desde otro hilo y se pasan al hilo de la pantalla
    private void IniciarLector()
    {
        if (!_lector.UsaPuertoSerie)
            return;

        var pantalla = SynchronizationContext.Current;
        try
        {
            _lector.Iniciar(codigo => pantalla?.Post(_ => CodigoLeidoPorPuerto(codigo), null));
        }
        catch (HardwareException ex)
        {
            Mensaje = ex.Message;
        }
    }

    private void CodigoLeidoPorPuerto(string codigo)
    {
        // Con otra ventana abierta (cobro, búsqueda...) no se toca el carrito
        if (_dialogos.HayDialogoAbierto)
        {
            Mensaje = $"Se leyó el código {codigo} con otra ventana abierta: no se agregó. Ciérrela y vuelva a escanear.";
            return;
        }

        CodigoIngresado = codigo;
        AgregarPorCodigoCommand.Execute(null);
    }

    // Lo llama la ventana al cerrarse, para liberar el puerto del lector
    public void LiberarDispositivos() => _lector.Detener();

    // El aviso de stock bajo solo lo ve quien puede corregirlo
    private async Task ActualizarAvisoStockAsync()
    {
        if (!_sesion.Tiene(Permiso.GestionarStock))
        {
            AvisoStock = string.Empty;
            return;
        }

        try
        {
            var cantidad = await _stockServicio.ContarBajoMinimoAsync();
            AvisoStock = cantidad switch
            {
                0 => string.Empty,
                1 => "1 producto con stock bajo · Ctrl+P",
                _ => $"{cantidad} productos con stock bajo · Ctrl+P"
            };
        }
        catch (Exception)
        {
            // El aviso no es imprescindible: si falla la consulta, no se muestra
            AvisoStock = string.Empty;
        }
    }

    // Ctrl+F: facturas pendientes de autorización (cualquier usuario puede reintentar)
    [RelayCommand(CanExecute = nameof(FacturacionActiva))]
    private async Task VerFacturasPendientesAsync()
    {
        _dialogos.VerFacturasPendientes();
        await ActualizarAvisoFacturasAsync();
        AccionTerminada?.Invoke();
    }

    private bool FacturacionActiva() => _facturacionServicio.EstaActiva;

    private async Task ActualizarAvisoFacturasAsync()
    {
        if (!_facturacionServicio.EstaActiva)
            return;

        try
        {
            var cantidad = await _facturacionServicio.ContarSinAutorizarAsync();
            AvisoFacturas = cantidad switch
            {
                0 => string.Empty,
                1 => "1 factura sin autorizar · Ctrl+F",
                _ => $"{cantidad} facturas sin autorizar · Ctrl+F"
            };
        }
        catch (Exception)
        {
            // El aviso no es imprescindible
        }
    }

    // F10: cambiar mi contraseña
    [RelayCommand]
    private void CambiarMiContrasena()
    {
        if (_dialogos.CambiarMiContrasena())
        {
            Mensaje = string.Empty;
            MensajeExito = "Contraseña cambiada.";
        }

        AccionTerminada?.Invoke();
    }

    // F12: cerrar sesión (solo sin una venta en curso)
    [RelayCommand]
    private void CerrarSesion()
    {
        if (!_carrito.EstaVacio)
        {
            Mensaje = "Termine o cancele la venta antes de cerrar sesión.";
            AccionTerminada?.Invoke();
            return;
        }

        _usuarioServicio.CerrarSesion();
        SesionCerrada?.Invoke();
    }

    // Flecha arriba en el campo de código: seleccionar el producto anterior del carrito
    [RelayCommand]
    private void SeleccionarAnterior() => MoverSeleccion(-1);

    // Flecha abajo en el campo de código: seleccionar el producto siguiente del carrito
    [RelayCommand]
    private void SeleccionarSiguiente() => MoverSeleccion(+1);

    private void MoverSeleccion(int paso)
    {
        if (Items.Count == 0)
            return;

        // Si no hay nada seleccionado, la flecha arriba elige el último y la flecha abajo el primero
        var posicion = ItemSeleccionado is null
            ? (paso < 0 ? Items.Count : -1)
            : Items.IndexOf(ItemSeleccionado);

        posicion = Math.Clamp(posicion + paso, 0, Items.Count - 1);
        ItemSeleccionado = Items[posicion];
    }

    private bool HayItemSeleccionado() => ItemSeleccionado is not null;

    private bool HayProductos() => !_carrito.EstaVacio;

    private void ActualizarTotales()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(Descuento));
        OnPropertyChanged(nameof(TieneDescuento));
        OnPropertyChanged(nameof(TextoDescuento));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(CantidadArticulos));
        CancelarVentaCommand.NotifyCanExecuteChanged();
        CobrarCommand.NotifyCanExecuteChanged();
        AplicarDescuentoCommand.NotifyCanExecuteChanged();
    }
}
