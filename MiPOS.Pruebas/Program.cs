using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MiPOS.Datos;
using MiPOS.Negocio.Servicios;
using MiPOS.Dominio.Entidades;
using MiPOS.Seguridad;
using MiPOS.Negocio.Comprobantes;
using MiPOS.Hardware.Impresion;
using MiPOS.Negocio.Cajas;
using MiPOS.Negocio.Ventas;
using MiPOS.Negocio.Productos;
using MiPOS.Negocio.Compras;
using MiPOS.Negocio.Comun;

Console.OutputEncoding = Encoding.UTF8;

var opciones = new DbContextOptionsBuilder<MiPOSDbContext>()
    .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=MiPOS;Trusted_Connection=True;TrustServerCertificate=True")
    .Options;

var servicio = new ProductoServicio(new PooledDbContextFactory<MiPOSDbContext>(opciones), new Sesion());
// --- Prueba 1: código que existe ---
Console.WriteLine("=== Buscar código 7795678 ===");
var producto = await servicio.BuscarPorCodigoAsync("7795678");
Console.WriteLine(producto is null
    ? "No encontrado"
    : $"{producto.Nombre} - ${producto.Precio:N2} ({producto.Categoria?.Nombre})");

// --- Prueba 2: código que NO existe ---
Console.WriteLine("\n=== Buscar código 0000000 ===");
var inexistente = await servicio.BuscarPorCodigoAsync("0000000");
Console.WriteLine(inexistente is null ? "No encontrado (correcto)" : "ERROR: no debería existir");

// --- Prueba 3: código con espacios (como algunos escáneres) ---
Console.WriteLine("\n=== Buscar código ' 7791234 ' con espacios ===");
var conEspacios = await servicio.BuscarPorCodigoAsync(" 7791234 ");
Console.WriteLine(conEspacios?.Nombre ?? "No encontrado");

// --- Prueba 4: búsqueda por nombre ---
foreach (var texto in new[] { "ARROZ", "co", "x" })
{
    Console.WriteLine($"\n=== Buscar por nombre '{texto}' ===");
    var resultados = await servicio.BuscarPorNombreAsync(texto);

    if (resultados.Count == 0)
        Console.WriteLine("Sin resultados");

    foreach (var p in resultados)
        Console.WriteLine($"{p.CodigoBarras}  {p.Nombre}  ${p.Precio:N2}");
}

// --- Prueba 5: hash de contraseñas ---
Console.WriteLine("\n=== Hash de contraseñas ===");
var reloj = System.Diagnostics.Stopwatch.StartNew();
var hash1 = HashContrasena.Generar("Secreta123");
Console.WriteLine($"Tiempo para generar un hash: {reloj.ElapsedMilliseconds} ms");
var hash2 = HashContrasena.Generar("Secreta123");

Console.WriteLine($"Hash: {hash1}");
Console.WriteLine($"Misma contraseña, hashes distintos (por la sal): {(hash1 != hash2 ? "OK" : "ERROR")}");
Console.WriteLine($"Contraseña correcta se acepta: {(HashContrasena.Verificar("Secreta123", hash1) ? "OK" : "ERROR")}");
Console.WriteLine($"Contraseña incorrecta se rechaza: {(!HashContrasena.Verificar("secreta123", hash1) ? "OK" : "ERROR")}");
Console.WriteLine($"Contraseña vacía se rechaza: {(!HashContrasena.Verificar("", hash1) ? "OK" : "ERROR")}");
Console.WriteLine($"Hash dañado se rechaza: {(!HashContrasena.Verificar("Secreta123", "PBKDF2$SHA256$600000$$") ? "OK" : "ERROR")}");
Console.WriteLine($"Hash con otro formato se rechaza: {(!HashContrasena.Verificar("Secreta123", "Secreta123") ? "OK" : "ERROR")}");

// --- Prueba 6: permisos por rol ---
Console.WriteLine("\n=== Permisos ===");
Console.WriteLine($"Administrador puede gestionar usuarios: {(Permisos.Tiene(Rol.Administrador, Permiso.GestionarUsuarios) ? "OK" : "ERROR")}");
Console.WriteLine($"Administrador puede aplicar descuento: {(Permisos.Tiene(Rol.Administrador, Permiso.AplicarDescuento) ? "OK" : "ERROR")}");
Console.WriteLine($"Cajero NO puede gestionar usuarios: {(!Permisos.Tiene(Rol.Cajero, Permiso.GestionarUsuarios) ? "OK" : "ERROR")}");
Console.WriteLine($"Cajero NO puede aplicar descuento: {(!Permisos.Tiene(Rol.Cajero, Permiso.AplicarDescuento) ? "OK" : "ERROR")}");

// --- Prueba 7: ticket (sin base de datos ni impresora) ---
var ventaEjemplo = new Venta
{
    Id = 15,
    Fecha = new DateTime(2026, 10, 12, 14, 32, 0),
    Usuario = new Usuario { NombreCompleto = "Juan Pérez" },
    MedioPago = new MedioPago { Nombre = "Efectivo", EsEfectivo = true },
    Subtotal = 6500m, DescuentoPorcentaje = 12.5m, Descuento = 812.50m, Total = 5687.50m,
    MontoRecibido = 6000m, Vuelto = 312.50m,
    Detalles =
    {
        new DetalleVenta { NombreProducto = "Coca Cola 2.25L", Cantidad = 2, PrecioUnitario = 2500m, Subtotal = 5000m },
        new DetalleVenta { NombreProducto = "Arroz largo fino doble carolina paquete familiar 1Kg", Cantidad = 1, PrecioUnitario = 1500m, Subtotal = 1500m }
    }
};
var comercioEjemplo = new DatosComercio
{
    Nombre = "Almacén Don Pepe",
    Direccion = "Av. Siempreviva 742 - CABA",
    Cuit = "20-12345678-9",
    MensajePie = "¡Gracias por su compra!"
};

foreach (var ancho in new[] { 42, 32 })
{
    Console.WriteLine($"\n=== Ticket de {ancho} caracteres ===");
    var lineasTicket = TicketVenta.Armar(ventaEjemplo, comercioEjemplo, ancho, esReimpresion: ancho == 32);
    foreach (var linea in lineasTicket)
        Console.WriteLine($"|{linea.PadRight(ancho)}|");

    Console.WriteLine($"Ninguna línea supera el ancho: {(lineasTicket.All(l => l.Length <= ancho) ? "OK" : "ERROR")}");
    Console.WriteLine($"Dice NO VÁLIDO COMO FACTURA: {(lineasTicket.Any(l => l.Contains("NO VÁLIDO COMO FACTURA")) ? "OK" : "ERROR")}");
}

// --- Prueba 8: impresoras ---
Console.WriteLine("\n=== Impresoras ===");
var impresoraArchivo = new ImpresoraArchivo(new OpcionesImpresion { Modo = ModoImpresion.Archivo, CaracteresPorLinea = 42 });
await impresoraArchivo.ImprimirAsync(TicketVenta.Armar(ventaEjemplo, comercioEjemplo, 42, false), "Venta 15");
Console.WriteLine($"Ticket guardado en archivo: {(File.Exists(impresoraArchivo.UltimoArchivo) ? "OK" : "ERROR")} ({impresoraArchivo.UltimoArchivo})");

try
{
    var impresoraInexistente = new ImpresoraWindows(new OpcionesImpresion { Impresora = "Impresora que no existe" });
    await impresoraInexistente.ImprimirAsync(new[] { "prueba" }, "Prueba");
    Console.WriteLine("Impresora inexistente da error: ERROR (no falló)");
}
catch (ImpresionException ex)
{
    Console.WriteLine($"Impresora inexistente da error: OK ({ex.Message})");
}

try
{
    new OpcionesImpresion { CaracteresPorLinea = 5 }.Validar();
    Console.WriteLine("Configuración inválida da error: ERROR (no falló)");
}
catch (ImpresionException ex)
{
    Console.WriteLine($"Configuración inválida da error: OK ({ex.Message})");
}

// --- Prueba 9: control de caja (contra la base real) ---
// Se usa una caja de pruebas (N° 99) para no tocar el turno de la Caja 1.
// Las ventas de esta prueba quedan registradas en la base de desarrollo, en la caja 99.
Console.WriteLine("\n=== Control de caja ===");
var fabrica = new PooledDbContextFactory<MiPOSDbContext>(opciones);
await using (var contexto = await fabrica.CreateDbContextAsync())
{
    var cajaPruebas = await contexto.Cajas.FirstOrDefaultAsync(c => c.Numero == 99);
    if (cajaPruebas is null)
        contexto.Cajas.Add(new Caja { Numero = 99, Nombre = "Caja de pruebas" });
    else
        cajaPruebas.Activa = true;
    await contexto.SaveChangesAsync();
}

await using (var contexto = await fabrica.CreateDbContextAsync())
{
    var administrador = await contexto.Usuarios.AsNoTracking()
        .FirstOrDefaultAsync(u => u.Activo && u.Rol == Rol.Administrador);

    if (administrador is null)
        Console.WriteLine("No hay un administrador activo: se saltean las pruebas de caja.");
    else
    {
        await ProbarCajaAsync(fabrica, administrador);
        await ProbarProductosYStockAsync(fabrica, administrador);
    }

    // La caja de pruebas queda desactivada para que no aparezca en el sistema
    await contexto.Cajas.Where(c => c.Numero == 99).ExecuteUpdateAsync(s => s.SetProperty(c => c.Activa, false));
}

static async Task ProbarCajaAsync(PooledDbContextFactory<MiPOSDbContext> fabrica, Usuario administrador)
{
    var sesion = new Sesion();
    sesion.Iniciar(administrador);
    var opcionesCaja = new OpcionesCaja { Numero = 99 };
    var cajaServicio = new CajaServicio(fabrica, sesion, opcionesCaja);
    var ventaServicio = new VentaServicio(fabrica, sesion, opcionesCaja);
    var productoServicio = new ProductoServicio(fabrica, sesion);

    // Si quedó abierta de una prueba anterior que falló, se cierra
    if (await cajaServicio.ObtenerTurnoAbiertoAsync() is not null)
        await cajaServicio.CerrarTurnoAsync(0);

    var carrito = new Carrito();
    carrito.Agregar((await productoServicio.BuscarPorCodigoAsync("7791234"))!); // $ 2.500

    await Esperar<VentaInvalidaException>("Vender con la caja cerrada da error",
        () => ventaServicio.RegistrarAsync(carrito, 1, 3000m));

    var turno = await cajaServicio.AbrirTurnoAsync(1000m);
    Console.WriteLine($"Abrir con $ 1.000,00: OK (turno N° {turno.Id})");

    await Esperar<CajaInvalidaException>("Abrir dos veces da error", () => cajaServicio.AbrirTurnoAsync(0));

    var stockAntes = await LeerStockAsync(fabrica, carrito.Items[0].ProductoId);

    var venta1 = await ventaServicio.RegistrarAsync(carrito, 1, 3000m);   // efectivo
    var venta2 = await ventaServicio.RegistrarAsync(carrito, 2, null);    // débito
    Console.WriteLine($"Las ventas quedan en el turno: {(venta1.TurnoCajaId == turno.Id && venta2.TurnoCajaId == turno.Id ? "OK" : "ERROR")}");

    // Cada venta descuenta 1 unidad y deja su movimiento de stock
    var stockDespues = await LeerStockAsync(fabrica, carrito.Items[0].ProductoId);
    Console.WriteLine($"Las ventas descuentan stock ({stockAntes} -> {stockDespues}): {(stockDespues == stockAntes - 2 ? "OK" : "ERROR")}");
    await using (var contexto = await fabrica.CreateDbContextAsync())
    {
        var movimientos = await contexto.MovimientosStock
            .CountAsync(m => (m.VentaId == venta1.Id || m.VentaId == venta2.Id) && m.Tipo == TipoMovimientoStock.Venta && m.Cantidad == -1);
        Console.WriteLine($"Cada venta deja su movimiento de stock: {(movimientos == 2 ? "OK" : "ERROR")}");
    }

    await cajaServicio.RegistrarMovimientoAsync(TipoMovimientoCaja.Ingreso, 500m, "Cambio del banco", null);
    Console.WriteLine("Ingreso de $ 500,00 sin autorización: OK");

    await Esperar<PermisoDenegadoException>("Retiro sin autorización da error",
        () => cajaServicio.RegistrarMovimientoAsync(TipoMovimientoCaja.Retiro, 100m, "Prueba", null));
    await Esperar<CajaInvalidaException>("Movimiento sin motivo da error",
        () => cajaServicio.RegistrarMovimientoAsync(TipoMovimientoCaja.Ingreso, 100m, "  ", null));
    await Esperar<CajaInvalidaException>("Retiro mayor al efectivo da error",
        () => cajaServicio.RegistrarMovimientoAsync(TipoMovimientoCaja.Retiro, 999999m, "Prueba", administrador.Id));

    await cajaServicio.RegistrarMovimientoAsync(TipoMovimientoCaja.Retiro, 1000m, "Retiro parcial", administrador.Id);
    Console.WriteLine("Retiro de $ 1.000,00 autorizado: OK");

    // Esperado: 1.000 inicial + 2.500 ventas en efectivo + 500 ingreso - 1.000 retiro = 3.000
    var resumen = await cajaServicio.CerrarTurnoAsync(2900m);
    Console.WriteLine($"Efectivo esperado $ 3.000,00: {(resumen.EfectivoEsperado == 3000m ? "OK" : $"ERROR ({resumen.EfectivoEsperado:N2})")}");
    Console.WriteLine($"Diferencia -$ 100,00: {(resumen.Turno.Diferencia == -100m ? "OK" : $"ERROR ({resumen.Turno.Diferencia:N2})")}");
    Console.WriteLine($"Ventas del turno: 2 por $ 5.000,00: {(resumen.CantidadVentas == 2 && resumen.TotalVentas == 5000m ? "OK" : "ERROR")}");
    Console.WriteLine($"Turno cerrado: {(await cajaServicio.ObtenerTurnoAbiertoAsync() is null ? "OK" : "ERROR")}");

    await Esperar<CajaInvalidaException>("Cerrar con la caja cerrada da error", () => cajaServicio.CerrarTurnoAsync(0));

    Console.WriteLine("\n=== Ticket de cierre de caja ===");
    var lineasCierre = TicketCierreCaja.Armar(resumen, new DatosComercio { Nombre = "Almacén Don Pepe" }, 42);
    foreach (var linea in lineasCierre)
        Console.WriteLine($"|{linea.PadRight(42)}|");
    Console.WriteLine($"Ninguna línea supera el ancho: {(lineasCierre.All(l => l.Length <= 42) ? "OK" : "ERROR")}");
}

static async Task<int> LeerStockAsync(PooledDbContextFactory<MiPOSDbContext> fabrica, int productoId)
{
    await using var contexto = await fabrica.CreateDbContextAsync();
    return await contexto.Productos.Where(p => p.Id == productoId).Select(p => p.Stock).FirstAsync();
}

// --- Prueba 10: productos y stock (contra la base real) ---
// Usa un producto de pruebas (código PRUEBA-STOCK) que queda desactivado al terminar
static async Task ProbarProductosYStockAsync(PooledDbContextFactory<MiPOSDbContext> fabrica, Usuario administrador)
{
    Console.WriteLine("\n=== Productos y stock ===");
    var sesion = new Sesion();
    sesion.Iniciar(administrador);
    var productoServicio = new ProductoServicio(fabrica, sesion);
    var stockServicio = new StockServicio(fabrica, sesion);
    const string codigo = "PRUEBA-STOCK";

    await using (var contexto = await fabrica.CreateDbContextAsync())
    {
        // Si quedó de una prueba anterior, se le cambia el código para poder crearlo de nuevo
        var anteriores = await contexto.Productos.Where(p => p.CodigoBarras == codigo).ToListAsync();
        foreach (var anterior in anteriores)
            anterior.CodigoBarras = $"PRUEBA-{anterior.Id}";
        await contexto.SaveChangesAsync();
    }

    var categoria = (await productoServicio.ObtenerCategoriasAsync()).First();
    var datos = new DatosProducto(codigo.ToLower(), "  Producto de prueba  ", categoria.Id, 1000m, StockMinimo: 5);

    var producto = await productoServicio.CrearAsync(datos, stockInicial: 10);
    Console.WriteLine($"Alta con stock inicial 10: {(producto.Stock == 10 && await LeerStockAsync(fabrica, producto.Id) == 10 ? "OK" : "ERROR")}");
    Console.WriteLine($"El código se guarda en mayúsculas y el nombre sin espacios: {(producto.CodigoBarras == codigo && producto.Nombre == "Producto de prueba" ? "OK" : "ERROR")}");

    await Esperar<ProductoInvalidoException>("Código repetido da error", () => productoServicio.CrearAsync(datos, 0));
    await Esperar<ProductoInvalidoException>("Precio cero da error",
        () => productoServicio.CrearAsync(datos with { CodigoBarras = "OTRO-CODIGO", Precio = 0 }, 0));
    await Esperar<ProductoInvalidoException>("Precio con 3 decimales da error",
        () => productoServicio.CrearAsync(datos with { CodigoBarras = "OTRO-CODIGO", Precio = 1.555m }, 0));
    await Esperar<ProductoInvalidoException>("Código con espacios da error",
        () => productoServicio.CrearAsync(datos with { CodigoBarras = "CON ESPACIO" }, 0));

    var codigoInterno = await productoServicio.GenerarCodigoInternoAsync();
    Console.WriteLine($"Código interno sugerido: {(codigoInterno.StartsWith("INT") && codigoInterno.Length >= 7 ? "OK" : "ERROR")} ({codigoInterno})");

    await stockServicio.AjustarAsync(producto.Id, -3, "Rotura");
    Console.WriteLine($"Ajuste -3: {(await LeerStockAsync(fabrica, producto.Id) == 7 ? "OK" : "ERROR")}");
    await Esperar<ProductoInvalidoException>("Ajuste sin motivo da error", () => stockServicio.AjustarAsync(producto.Id, 1, " "));
    await Esperar<ProductoInvalidoException>("Ajuste de cero da error", () => stockServicio.AjustarAsync(producto.Id, 0, "Nada"));

    await Esperar<ProductoInvalidoException>("Stock contado con dato viejo da error",
        () => stockServicio.EstablecerStockAsync(producto.Id, stockLeido: 10, stockContado: 4, "Inventario"));
    await stockServicio.EstablecerStockAsync(producto.Id, stockLeido: 7, stockContado: 4, "Inventario");
    Console.WriteLine($"Stock contado 4: {(await LeerStockAsync(fabrica, producto.Id) == 4 ? "OK" : "ERROR")}");

    var alertas = await stockServicio.ObtenerBajoMinimoAsync();
    Console.WriteLine($"Con stock 4 y mínimo 5 aparece en alertas: {(alertas.Any(p => p.Id == producto.Id) ? "OK" : "ERROR")}");

    var movimientos = await stockServicio.ObtenerMovimientosAsync(producto.Id);
    Console.WriteLine($"Historia: inicial +10, rotura -3, inventario -3: {(movimientos.Select(m => m.Cantidad).SequenceEqual(new[] { -3, -3, 10 }) ? "OK" : "ERROR")}");
    Console.WriteLine($"La suma de los movimientos es igual al stock: {(movimientos.Sum(m => m.Cantidad) == 4 ? "OK" : "ERROR")}");

    await productoServicio.ModificarAsync(producto.Id, datos with { CodigoBarras = codigo, Precio = 1200m, StockMinimo = 2 });
    var modificado = await productoServicio.ObtenerAsync(producto.Id);
    Console.WriteLine($"Modificar precio y mínimo: {(modificado.Precio == 1200m && modificado.StockMinimo == 2 && modificado.Stock == 4 ? "OK" : "ERROR")}");
    Console.WriteLine($"Con stock 4 y mínimo 2 sale de las alertas: {(!(await stockServicio.ObtenerBajoMinimoAsync()).Any(p => p.Id == producto.Id) ? "OK" : "ERROR")}");

    var cajero = new Sesion();
    cajero.Iniciar(new Usuario { Id = administrador.Id, Rol = Rol.Cajero });
    await Esperar<PermisoDenegadoException>("Un cajero no puede ajustar stock",
        () => new StockServicio(fabrica, cajero).AjustarAsync(producto.Id, 1, "Prueba"));
    await Esperar<PermisoDenegadoException>("Un cajero no puede modificar productos",
        () => new ProductoServicio(fabrica, cajero).ModificarAsync(producto.Id, datos with { CodigoBarras = codigo }));

    await ProbarComprasAsync(fabrica, sesion, producto.Id);

    await productoServicio.CambiarActivoAsync(producto.Id, false);
    Console.WriteLine($"Desactivado no aparece en la caja: {(await productoServicio.BuscarPorCodigoAsync(codigo) is null ? "OK" : "ERROR")}");
}

// --- Prueba 11: compras (contra la base real) ---
// Usa un proveedor de pruebas y el producto de pruebas de la prueba 10
static async Task ProbarComprasAsync(PooledDbContextFactory<MiPOSDbContext> fabrica, Sesion sesion, int productoId)
{
    Console.WriteLine("\n=== CUIT ===");
    Console.WriteLine($"20-12345678-6 es válido: {(Cuit.IntentarNormalizar("20-12345678-6", out var cuit) && cuit == "20123456786" ? "OK" : "ERROR")}");
    Console.WriteLine($"30-71234567-0 es inválido (verificador mal): {(!Cuit.IntentarNormalizar("30-71234567-0", out _) ? "OK" : "ERROR")}");
    Console.WriteLine($"Con letras es inválido: {(!Cuit.IntentarNormalizar("20-1234567A-6", out _) ? "OK" : "ERROR")}");
    Console.WriteLine($"Formatear: {(Cuit.Formatear("20123456786") == "20-12345678-6" ? "OK" : "ERROR")}");

    Console.WriteLine("\n=== Compras ===");
    var compraServicio = new CompraServicio(fabrica, sesion);
    const string nombreProveedor = "Proveedor de pruebas";

    var proveedor = (await compraServicio.ObtenerProveedoresAsync(incluirInactivos: true)).FirstOrDefault(p => p.Nombre == nombreProveedor);
    if (proveedor is null)
        proveedor = await compraServicio.CrearProveedorAsync(nombreProveedor, "20-12345678-6", "11 5555-5555");
    else
        await compraServicio.CambiarActivoProveedorAsync(proveedor.Id, true);
    Console.WriteLine($"Proveedor con CUIT guardado sin guiones: {(proveedor.Cuit == "20123456786" ? "OK" : "ERROR")}");

    await Esperar<CompraInvalidaException>("Proveedor repetido da error",
        () => compraServicio.CrearProveedorAsync(nombreProveedor, null, null));
    await Esperar<CompraInvalidaException>("CUIT inválido da error",
        () => compraServicio.CrearProveedorAsync("Otro proveedor de pruebas", "20-12345678-0", null));

    var stockAntes = await LeerStockAsync(fabrica, productoId);
    var items = new List<ItemCompra> { new(productoId, "PRUEBA-STOCK", "Producto de prueba", 24, 650.50m) };

    await Esperar<CompraInvalidaException>("Compra sin productos da error",
        () => compraServicio.RegistrarAsync(proveedor.Id, null, new List<ItemCompra>()));
    await Esperar<CompraInvalidaException>("Cantidad cero da error",
        () => compraServicio.RegistrarAsync(proveedor.Id, null, new List<ItemCompra> { items[0] with { Cantidad = 0 } }));
    await Esperar<CompraInvalidaException>("Producto repetido da error",
        () => compraServicio.RegistrarAsync(proveedor.Id, null, new List<ItemCompra> { items[0], items[0] }));

    var compra = await compraServicio.RegistrarAsync(proveedor.Id, " A-0001-00001234 ", items);
    Console.WriteLine($"Compra registrada, total $ 15.612,00: {(compra.Total == 15612m && compra.NumeroComprobante == "A-0001-00001234" ? "OK" : "ERROR")}");
    Console.WriteLine($"La compra suma stock ({stockAntes} -> {await LeerStockAsync(fabrica, productoId)}): {(await LeerStockAsync(fabrica, productoId) == stockAntes + 24 ? "OK" : "ERROR")}");

    await using (var contexto = await fabrica.CreateDbContextAsync())
    {
        var movimiento = await contexto.MovimientosStock.FirstOrDefaultAsync(m => m.CompraId == compra.Id);
        Console.WriteLine($"La compra deja su movimiento de stock: {(movimiento is { Tipo: TipoMovimientoStock.Compra, Cantidad: 24 } ? "OK" : "ERROR")}");
    }

    Console.WriteLine($"Último costo $ 650,50: {(await compraServicio.ObtenerUltimoCostoAsync(productoId) == 650.50m ? "OK" : "ERROR")}");
    var compras = await compraServicio.ObtenerComprasAsync();
    Console.WriteLine($"La compra aparece en la lista con su detalle: {(compras.FirstOrDefault(c => c.Id == compra.Id)?.Detalles.Count == 1 ? "OK" : "ERROR")}");

    await compraServicio.CambiarActivoProveedorAsync(proveedor.Id, false);
    await Esperar<CompraInvalidaException>("Comprar a un proveedor desactivado da error",
        () => compraServicio.RegistrarAsync(proveedor.Id, null, items));
}

// Ejecuta la acción y comprueba que lance el error esperado
static async Task Esperar<TError>(string descripcion, Func<Task> accion) where TError : Exception
{
    try
    {
        await accion();
        Console.WriteLine($"{descripcion}: ERROR (no falló)");
    }
    catch (TError ex)
    {
        Console.WriteLine($"{descripcion}: OK ({ex.Message})");
    }
}
