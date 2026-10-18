using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MiPOS.Datos;
using MiPOS.Negocio.Servicios;
using MiPOS.Dominio.Entidades;
using MiPOS.Seguridad;
using MiPOS.Negocio.Comprobantes;
using MiPOS.Hardware.Impresion;

Console.OutputEncoding = Encoding.UTF8;

var opciones = new DbContextOptionsBuilder<MiPOSDbContext>()
    .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=MiPOS;Trusted_Connection=True;TrustServerCertificate=True")
    .Options;

var servicio = new ProductoServicio(new PooledDbContextFactory<MiPOSDbContext>(opciones));
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
