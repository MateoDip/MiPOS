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
| `MiPOS.Negocio` | Servicios (`Servicios/`: `ProductoServicio`, `VentaServicio`, `UsuarioServicio`, `TicketServicio`, `CajaServicio`), lógica de venta (`Ventas/`: `Carrito`, `ItemCarrito`, `Cobro`), de usuarios (`Usuarios/`), de caja (`Cajas/`: `ResumenTurno`, `OpcionesCaja`) y comprobantes (`Comprobantes/`: `TicketVenta`, `TicketCierreCaja`, `FormatoTicket`, `DatosComercio`) | Dominio, Datos, Seguridad |
| `MiPOS.WPF` | `Vistas/`, `ViewModels/`, `Servicios/` (`DialogoServicio`, `LectorNumeros`, `VisibleSiFalse`), `App.xaml.cs` (inyección de dependencias e inicio de sesión), `appsettings.json` | Negocio, Dominio, Seguridad, Hardware |
| `MiPOS.Seguridad` | Hash de contraseñas (`HashContrasena`), permisos por rol (`Permiso`, `Permisos`), usuario conectado (`Sesion`) | Dominio |
| `MiPOS.Hardware` | Impresión de tickets (`Impresion/`: `IImpresoraTicket`, `ImpresoraWindows`, `ImpresoraArchivo`, `OpcionesImpresion`). Solo Windows (`net10.0-windows`, usa WPF) | Nada |
| `MiPOS.Pruebas` | Consola para pruebas manuales contra la base real (`net10.0-windows`) | Negocio, Datos, Seguridad, Hardware |

Más adelante se agregará `MiPOS.Facturacion` siguiendo el mismo criterio.

## Decisiones ya tomadas

- **Dinero siempre en `decimal`**, nunca `double` ni `float`. En la base: `HasPrecision(18, 2)`.
- **No se borran productos ni categorías**: se desactivan con la propiedad `Activo`/`Activa`. Las búsquedas solo devuelven activos.
- **`CodigoBarras` es `string`, obligatorio y único.** Los artículos sin código de barras usarán un código interno (por ejemplo `INT0001`).
- **El precio se congela en `ItemCarrito` al agregar el producto.** Un cambio de precio no afecta una venta en curso.
- **La cantidad es `int`.** La venta por peso no está contemplada en el diseño.
- **Acceso a datos:** los servicios reciben `IDbContextFactory<MiPOSDbContext>` y crean un contexto por operación (`await using var contexto = await _fabrica.CreateDbContextAsync();`). Lecturas con `AsNoTracking()`.
- **Configuración de tablas con Fluent API** en `MiPOS.Datos/Configuraciones/`, una clase `IEntityTypeConfiguration<T>` por entidad. Nada de data annotations en las entidades.
- **`MiPOS.Negocio` usa directamente el `DbContext` de `MiPOS.Datos`** (simplificación aceptada; no introducir repositorios ni interfaces extra sin consultar).
- **MVVM estricto:** los ViewModels heredan de `ObservableObject`, son `partial` y usan `[ObservableProperty]` (sobre campos privados) y `[RelayCommand]`. El code-behind de las ventanas solo asigna el `DataContext` y maneja el foco. **Nunca lógica de negocio en las ventanas.** Excepciones aceptadas: pasar la contraseña de un `PasswordBox` al ViewModel en `PasswordChanged` (WPF no permite Binding en `PasswordBox`), y detectar F10 en `PreviewKeyDown` (Windows la usa para el menú y no llega a un `KeyBinding`).
- **Reglas de negocio en `MiPOS.Negocio`**, no en los ViewModels. El ViewModel solo coordina entre la pantalla y el negocio.
- **Inyección de dependencias** registrada en `App.xaml.cs`. Ventanas, ViewModels y servicios como `Transient`. Única excepción: `Sesion` es `Singleton` (una sola sesión para toda la aplicación).
- **Cadena de conexión** en `MiPOS.WPF/appsettings.json` (`ConnectionStrings:MiPOS`), copiada a la salida con `CopyToOutputDirectory`.
- **Cultura `es-AR`** configurada en `App.xaml.cs`. Precios con formato `'$ {0:N2}'`.
- **Textos de la interfaz:** por ahora están escritos en el XAML en español. En la Fase 10 (multidioma) se migrarán a archivos de recursos. No migrarlos antes sin consultar.
- **Migraciones:** nunca modificar una migración ya aplicada; crear una nueva con un nombre descriptivo en español.
- **Diálogos:** los ViewModels nunca abren ventanas ni usan `MessageBox`; lo hacen a través de `MiPOS.WPF/Servicios/DialogoServicio`. Cada diálogo tiene su ViewModel con un evento `Cerrar(bool)` que la ventana convierte en `DialogResult`. El dueño (`Owner`) de cada diálogo es la ventana activa.
- **Ventas:** `DetalleVenta` guarda una copia del nombre y del precio del producto al momento de vender. El medio que calcula vuelto se identifica con `MedioPago.EsEfectivo`, nunca por el nombre. Los errores de reglas de venta se lanzan como `VentaInvalidaException` (su mensaje se muestra al cajero). Toda venta guarda el `UsuarioId` de quien la hizo. El descuento es un porcentaje sobre el total (`Carrito.AplicarDescuento`); la venta guarda `Subtotal`, `DescuentoPorcentaje`, `Descuento` y `DescuentoAutorizadoPorId`, y `VentaServicio` vuelve a verificar en la base que quien autorizó tenga el permiso.
- **Errores de usuarios y permisos:** `UsuarioInvalidoException` (reglas de usuarios) y `PermisoDenegadoException` (falta de permiso). Sus mensajes se muestran tal cual en pantalla.
- **Autorización:** si el usuario conectado tiene el permiso, autoriza él mismo; si no, se abre `AutorizacionVentana` y se registra quién autorizó. Quitar un descuento no requiere autorización.
- **Contraseñas:** nunca se guardan ni se registran en texto; solo el hash PBKDF2-SHA256 (600.000 iteraciones, sal aleatoria de 16 bytes por usuario), con formato `PBKDF2$SHA256$<iteraciones>$<sal>$<hash>`. La comparación se hace con `CryptographicOperations.FixedTimeEquals`. El cálculo se ejecuta fuera del hilo de la pantalla.
- **Roles fijos en código:** `Rol` (Cajero, Administrador) es un enum en `MiPOS.Dominio`; qué permisos tiene cada rol se define en `MiPOS.Seguridad/Permisos`. No hay tabla de permisos configurables.
- **Los permisos se verifican en `MiPOS.Negocio`**, no solo ocultando botones en la pantalla.
- **Usuarios:** no se borran, se desactivan. Nunca puede quedar el sistema sin un administrador activo. No hay usuario ni contraseña por defecto: si no existen usuarios, el primer arranque pide crear el administrador. 5 intentos fallidos seguidos bloquean el inicio de sesión de ese usuario por 30 segundos.
- **Impresión:** se usa la impresión común de Windows (cualquier impresora con driver), no comandos ESC/POS. Todo se configura por instalación en la sección `Impresion` de `appsettings.json` (`Modo` = `Windows` o `Archivo`, `Impresora` vacío = predeterminada, `AnchoPapelMm`, `CaracteresPorLinea`, `MargenMm`, `PapelContinuo`, `CarpetaArchivos`). En el repositorio queda `Modo = Archivo` para desarrollar sin impresora. `IImpresoraTicket` es una interfaz aprobada (dos implementaciones reales). `MiPOS.Hardware` no depende de ningún otro proyecto: recibe el ticket como líneas de texto.
- **Ticket:** lo arma `TicketVenta` (en `MiPOS.Negocio/Comprobantes`) siempre desde la venta guardada en la base, con letra de ancho fijo y montos en formato es-AR. Mientras no haya facturación electrónica dice "NO VÁLIDO COMO FACTURA". Se imprime automáticamente al cobrar; si falla, la venta queda registrada igual y se avisa para reimprimir con F8 (que marca "REIMPRESIÓN").
- **Datos del comercio:** el software se vende a distintos negocios, así que nombre, dirección, CUIT, teléfono y mensaje de pie van en la sección `Comercio` de `appsettings.json` (los vacíos no se imprimen). Más adelante pasarán a una pantalla de configuración.
- **Control de caja:** por ahora siempre activo (el interruptor del módulo llegará con la pantalla de configuración). Las cajas están en la tabla `Cajas` y cada PC indica la suya en `Caja:Numero` de `appsettings.json`. El turno (`TurnoCaja`) es de la caja, no del cajero: si cambia el usuario se sigue en el mismo turno, y cada venta guarda igual quién la hizo. La base impide dos turnos abiertos en la misma caja (índice único filtrado). Las ventas anteriores al control de caja quedan con `TurnoCajaId` en null.
- **Arqueo ciego:** al cerrar, el cajero informa el efectivo contado sin ver el esperado; después se muestra la diferencia (contado − esperado). Retiros e ingresos de efectivo llevan motivo y entran en el arqueo; el retiro requiere el permiso `RetirarEfectivo` (autorización de administrador, verificada de nuevo en la base) y no puede superar el efectivo esperado. El efectivo esperado se calcula solo en `ResumenTurno` (inicial + ventas en efectivo + ingresos − retiros). No se vende con la caja cerrada: `VentaServicio` lo exige y guarda el turno. Al entrar a la caja, si está cerrada se ofrece abrirla. El cierre imprime un comprobante (`TicketCierreCaja`) y requiere el carrito vacío. Errores de caja: `CajaInvalidaException` (mensaje mostrado tal cual).
- **Montos ingresados por el cajero:** solo números con coma decimal y hasta 2 decimales; no se aceptan puntos (en `es-AR` "1500.50" se leería como 150050).

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

**Atajos de teclado de la caja:** F1 buscar, F2 producto, F3 cobrar, F4 eliminar, F5 cambiar cantidad, F6 descuento, F7 retiro/ingreso de efectivo, F8 reimprimir último ticket, F9 usuarios (solo administrador), F10 cambiar mi contraseña, F11 abrir/cerrar caja, F12 cerrar sesión, ESC cancelar.

**Medios de pago:** efectivo, débito, crédito, Mercado Pago, QR, transferencia. Un único medio por venta. En efectivo se calcula el vuelto.

**Módulos opcionales** (se activan/desactivan desde configuración): stock (con mínimos y alertas), compras (requiere stock), control de caja (apertura, cierre y arqueo), cajón de dinero.

**Otros requisitos:** varias cajas simultáneas contra una base central; ventas básicas sin Internet; facturación con ARCA; impresora térmica; contraseñas con hashing seguro; licenciamiento por instalación; idiomas español y chino.

## Plan de fases

1. POS básico — **terminada**
2. Usuarios (Cajero, Administrador, permisos, autorización por contraseña) — **terminada**
3. Impresión (ticket, impresora térmica) — **terminada** (pendiente la prueba en pantalla junto con el testeo general)
4. Control de caja — **terminada** (pendiente la prueba en pantalla junto con el testeo general)
5. Stock y compras
6. Facturación (ARCA)
7. Hardware (scanner, impresora, cajón, terminal POS)
8. Offline y sincronización
9. Licenciamiento
10. Multidioma (español y chino)

## Estado de la Fase 4 (terminada)

- [x] 4.1 Entidades `Caja`, `TurnoCaja`, `MovimientoCaja` y turno en cada `Venta` (migración `AgregarControlDeCaja`, Caja 1 inicial, `Caja:Numero` en `appsettings.json`)
- [x] 4.2 `CajaServicio`: abrir turno, retiros e ingresos, cierre con arqueo; toda venta exige turno abierto
- [x] 4.3 Pantallas: apertura al entrar, retiro/ingreso (F7), cierre con arqueo ciego (F11), resumen y ticket de cierre

## Estado de la Fase 3 (terminada)

- [x] 3.1 Proyecto `MiPOS.Hardware`: impresión por Windows configurable e impresora de archivo para desarrollo
- [x] 3.2 Ticket: `TicketVenta`, `TicketServicio`, datos del comercio y configuración en `appsettings.json`
- [x] 3.3 Impresión automática al cobrar y reimpresión del último ticket (F8)

## Estado de la Fase 2 (terminada)

- [x] 2.1 Proyecto `MiPOS.Seguridad`: hash de contraseñas (PBKDF2) y permisos por rol
- [x] 2.2 Entidad `Usuario` y `UsuarioServicio` (inicio de sesión con bloqueo, alta, edición, contraseñas, activar/desactivar)
- [x] 2.3 Inicio de sesión: creación del primer administrador, `Sesion`, usuario en la caja, cerrar sesión (F12), usuario obligatorio en cada venta
- [x] 2.4 Gestión de usuarios (F9, solo administrador) y cambiar mi contraseña (F10)
- [x] 2.5 Autorización por contraseña de administrador (diálogo reutilizable)
- [x] 2.6 Descuento (F6) con autorización de administrador

## Estado de la Fase 1 (terminada)

- [x] 1.1 Entidades `Categoria` y `Producto`
- [x] 1.2 Base de datos con EF Core, configuraciones y datos iniciales (3 categorías, 3 productos)
- [x] 1.3 `ProductoServicio`: búsqueda por código y por nombre
- [x] 1.4 Pantalla de caja MVVM: escaneo con Enter, carrito (suma cantidades si se repite el producto), total y cantidad de artículos
- [x] 1.5 Operación del carrito: buscar por nombre (F1), eliminar (F4), cambiar cantidad (F5), cancelar venta (ESC), navegación del carrito con flechas
- [x] 1.6 Cobro: medio de pago, vuelto en efectivo y registro de la venta (entidades `Venta`, `DetalleVenta`, `MedioPago`)

Actualizar esta lista al completar cada paso.
