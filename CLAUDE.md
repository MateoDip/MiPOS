# MiPOS — Guía del proyecto para Claude Code

## Contexto

MiPOS es un sistema POS (punto de venta) para comercios minoristas, principalmente supermercados y bazares, que funciona en computadoras y notebooks con Windows. Lo desarrolla un equipo con poca o ninguna experiencia profesional en desarrollo de software, avanzando paso a paso.

## Reglas de comunicación

- Toda la comunicación, explicaciones, comentarios en el código, mensajes de commit y documentación deben estar en **español**.
- Explicar de manera clara, ordenada y progresiva. No asumir conocimientos avanzados.
- Antes de escribir código: explicar la solución en palabras simples y qué archivos se van a crear o modificar.
- Después de escribir código: explicar cómo probarlo y cuáles son los errores comunes.
- Trabajar en **pasos chicos**. Al terminar un paso, compilar, explicar cómo probarlo y **esperar confirmación** antes de seguir con el siguiente.
- No agregar funcionalidades no solicitadas sin avisarlo y pedir permiso.
- No cambiar decisiones de arquitectura (ver "Decisiones ya tomadas") sin explicar por qué y pedir aprobación.
- No hacer `git commit` ni `git push` sin pedir permiso. Al final de cada paso, proponer el mensaje de commit.

## Tecnologías

- C# / .NET 10 (LTS), Visual Studio 2026
- WPF para la interfaz, con patrón MVVM usando **CommunityToolkit.Mvvm**
- Entity Framework Core con **SQL Server** (en desarrollo: LocalDB `(localdb)\MSSQLLocalDB`, base `MiPOS`)
- `Microsoft.Extensions.DependencyInjection` y `Microsoft.Extensions.Configuration.Json`
- SQLite queda reservado para funciones offline futuras (Fase 8). No usarlo todavía.

## Estructura de la solución

| Proyecto | Contenido | Depende de |
|---|---|---|
| `MiPOS.Dominio` | Entidades (`Entidades/`) | Nada |
| `MiPOS.Datos` | `MiPOSDbContext`, configuraciones Fluent API (`Configuraciones/`), migraciones, `MiPOSDbContextFactory` (solo para `dotnet ef`) | Dominio |
| `MiPOS.Negocio` | Servicios (`Servicios/`) y lógica de venta (`Ventas/`: `Carrito`, `ItemCarrito`) | Dominio, Datos |
| `MiPOS.WPF` | `Vistas/`, `ViewModels/`, `App.xaml.cs` (inyección de dependencias), `appsettings.json` | Negocio, Dominio |
| `MiPOS.Pruebas` | Consola para pruebas manuales contra la base real | Negocio, Datos |

Más adelante se agregarán `MiPOS.Hardware`, `MiPOS.Facturacion` y `MiPOS.Seguridad` siguiendo el mismo criterio.

## Decisiones ya tomadas

- **Dinero siempre en `decimal`**, nunca `double` ni `float`. En la base: `HasPrecision(18, 2)`.
- **No se borran productos ni categorías**: se desactivan con la propiedad `Activo`/`Activa`. Las búsquedas solo devuelven activos.
- **`CodigoBarras` es `string`, obligatorio y único.** Los artículos sin código de barras usarán un código interno (por ejemplo `INT0001`).
- **El precio se congela en `ItemCarrito` al agregar el producto.** Un cambio de precio no afecta una venta en curso.
- **La cantidad es `int`.** La venta por peso no está contemplada en el diseño.
- **Acceso a datos:** los servicios reciben `IDbContextFactory<MiPOSDbContext>` y crean un contexto por operación (`await using var contexto = await _fabrica.CreateDbContextAsync();`). Lecturas con `AsNoTracking()`.
- **Configuración de tablas con Fluent API** en `MiPOS.Datos/Configuraciones/`, una clase `IEntityTypeConfiguration<T>` por entidad. Nada de data annotations en las entidades.
- **`MiPOS.Negocio` usa directamente el `DbContext` de `MiPOS.Datos`** (simplificación aceptada; no introducir repositorios ni interfaces extra sin consultar).
- **MVVM estricto:** los ViewModels heredan de `ObservableObject`, son `partial` y usan `[ObservableProperty]` (sobre campos privados) y `[RelayCommand]`. El code-behind de las ventanas solo asigna el `DataContext` y maneja el foco. **Nunca lógica de negocio en las ventanas.**
- **Reglas de negocio en `MiPOS.Negocio`**, no en los ViewModels. El ViewModel solo coordina entre la pantalla y el negocio.
- **Inyección de dependencias** registrada en `App.xaml.cs`. Ventanas, ViewModels y servicios como `Transient`.
- **Cadena de conexión** en `MiPOS.WPF/appsettings.json` (`ConnectionStrings:MiPOS`), copiada a la salida con `CopyToOutputDirectory`.
- **Cultura `es-AR`** configurada en `App.xaml.cs`. Precios con formato `'$ {0:N2}'`.
- **Textos de la interfaz:** por ahora están escritos en el XAML en español. En la Fase 10 (multidioma) se migrarán a archivos de recursos. No migrarlos antes sin consultar.
- **Migraciones:** nunca modificar una migración ya aplicada; crear una nueva con un nombre descriptivo en español.

## Codificación de archivos

Todos los archivos deben guardarse en **UTF-8**. Ya hubo errores de compilación por archivos con tildes guardados en ANSI.

## Comandos útiles

```powershell
dotnet build
dotnet run --project MiPOS.WPF
dotnet run --project MiPOS.Pruebas
dotnet ef migrations add NombreMigracion --project MiPOS.Datos
dotnet ef database update --project MiPOS.Datos
```

Después de cada cambio, ejecutar `dotnet build` y corregir los errores antes de dar el paso por terminado.

## Especificación funcional resumida

**Roles**
- Cajero: vender, escanear, buscar por nombre, cambiar cantidades, eliminar productos de la venta, cancelar venta, cobrar, elegir medio de pago, imprimir comprobante. No puede modificar precios, productos, stock ni configuración, ni aplicar descuentos sin contraseña de administrador.
- Administrador: todo lo del cajero más productos, precios, stock, compras, cajas, hardware, usuarios, descuentos, facturación, configuración, idioma y licencia.

**Flujo de venta:** caja → escanear/buscar → agregar al carrito → cambiar cantidad/eliminar → total → descuento opcional (requiere contraseña de administrador) → cobrar → medio de pago → comprobante (ticket fiscal o interno) → registrar venta → descontar stock si el módulo está activo → nueva venta.

**Atajos de teclado de la caja:** F1 buscar, F2 producto, F3 cobrar, F4 eliminar, F5 cambiar cantidad, F6 descuento, ESC cancelar.

**Medios de pago:** efectivo, débito, crédito, Mercado Pago, QR, transferencia. Un único medio por venta. En efectivo se calcula el vuelto.

**Módulos opcionales** (se activan/desactivan desde configuración): stock (con mínimos y alertas), compras (requiere stock), control de caja (apertura, cierre y arqueo), cajón de dinero.

**Otros requisitos:** varias cajas simultáneas contra una base central; ventas básicas sin Internet; facturación con ARCA; impresora térmica; contraseñas con hashing seguro; licenciamiento por instalación; idiomas español y chino.

## Plan de fases

1. POS básico — **en curso**
2. Usuarios (Cajero, Administrador, permisos, autorización por contraseña)
3. Impresión (ticket, impresora térmica)
4. Control de caja
5. Stock y compras
6. Facturación (ARCA)
7. Hardware (scanner, impresora, cajón, terminal POS)
8. Offline y sincronización
9. Licenciamiento
10. Multidioma (español y chino)

## Estado actual de la Fase 1

- [x] 1.1 Entidades `Categoria` y `Producto`
- [x] 1.2 Base de datos con EF Core, configuraciones y datos iniciales (3 categorías, 3 productos)
- [x] 1.3 `ProductoServicio`: búsqueda por código y por nombre
- [x] 1.4 Pantalla de caja MVVM: escaneo con Enter, carrito (suma cantidades si se repite el producto), total y cantidad de artículos
- [x] 1.5 Operación del carrito: buscar por nombre (F1), eliminar (F4), cambiar cantidad (F5), cancelar venta (ESC), navegación del carrito con flechas
- [ ] 1.6 Cobro: medio de pago, vuelto en efectivo y registro de la venta (entidades `Venta`, `DetalleVenta`, `MedioPago`)

Actualizar esta lista al completar cada paso.
