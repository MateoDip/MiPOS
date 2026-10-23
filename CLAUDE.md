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
- SQLite (`Microsoft.EntityFrameworkCore.Sqlite`) solo para la base local de cada caja (`MiPOS.DatosLocal`, venta sin conexión). La base del sistema sigue siendo SQL Server.

## Estructura de la solución

| Proyecto | Contenido | Depende de |
|---|---|---|
| `MiPOS.Dominio` | Entidades (`Entidades/`) | Nada |
| `MiPOS.Datos` | `MiPOSDbContext`, configuraciones Fluent API (`Configuraciones/`), migraciones, `MiPOSDbContextFactory` (solo para `dotnet ef`), `ErroresConexion` | Dominio |
| `MiPOS.Negocio` | Servicios (`Servicios/`: `ProductoServicio`, `VentaServicio`, `UsuarioServicio`, `TicketServicio`, `CajaServicio`, `StockServicio`, `CompraServicio`, `SincronizacionServicio`), venta sin conexión (`SinConexion/`: `BaseLocal`, `OpcionesSinConexion`), lógica de venta (`Ventas/`: `Carrito`, `ItemCarrito`, `Cobro`), de usuarios (`Usuarios/`), de caja (`Cajas/`: `ResumenTurno`, `OpcionesCaja`), de productos (`Productos/`: `DatosProducto`), de compras (`Compras/`: `ItemCompra`), de facturación (`Facturacion/`: `CalculadoraFactura`, `DatosReceptor`, `DatosFiscales`), utilidades comunes (`Comun/`: `Cuit`) y comprobantes (`Comprobantes/`: `TicketVenta`, `TicketCierreCaja`, `TicketParaImprimir`, `FormatoTicket`, `DatosComercio`). Servicio de facturación: `FacturacionServicio`. Licencia: `LicenciaServicio`, `Licencias/` (`OpcionesLicencia`, `LicenciaInvalidaException`) | Dominio, Datos, DatosLocal, Seguridad, Facturacion, Licencias |
| `MiPOS.DatosLocal` | Base local SQLite de cada caja: `LocalDbContext` (copia de productos, medios de pago y usuarios; `VentaPendiente`; `ValorLocal`), `Configuraciones/`. Sin migraciones (`VersionEsquema`) | Dominio |
| `MiPOS.WPF` | `Vistas/`, `ViewModels/`, `Servicios/` (`DialogoServicio`, `LectorNumeros`, `VisibleSiFalse`), `Idiomas/` (`Traductor`, `TraduccionVentanas`, `ConvertidorTraduccion`, `OpcionesIdioma`, catálogo `zh.json`), `App.xaml.cs` (inyección de dependencias en `RegistrarServicios` e inicio de sesión), `appsettings.json` | Negocio, Dominio, Seguridad, Hardware, Facturacion |
| `MiPOS.Seguridad` | Hash de contraseñas (`HashContrasena`), permisos por rol (`Permiso`, `Permisos`), usuario conectado (`Sesion`) | Dominio |
| `MiPOS.Hardware` | Impresión de tickets (`Impresion/`: `IImpresoraTicket`, `ImpresoraWindows`, `ImpresoraArchivo`, `OpcionesImpresion`) con código QR (paquete QRCoder), cajón de dinero (`Cajon/`: `CajonDinero`, `OpcionesCajon`), lector por puerto serie (`Lector/`: `LectorCodigos`, `OpcionesLector`), terminal de pago (`Terminal/`: `ITerminalPago`, `TerminalMercadoPagoPoint`, `TerminalSimulada`, `OpcionesTerminal`), `HardwareException`. Guía en `MiPOS.Hardware/LEEME.md`. Solo Windows (`net10.0-windows`, usa WPF; paquete System.IO.Ports) | Nada |
| `MiPOS.Facturacion` | Comunicación con ARCA: `OpcionesFacturacion`, `IServicioArca`, `Arca/` (`AutenticacionWsaa`, `ServicioArcaWs`, `ServicioArcaSimulado`, `ClienteSoap`), `CodigoQrArca`. Guía de configuración en `MiPOS.Facturacion/LEEME.md` | Nada |
| `MiPOS.Licencias` | `Licencia`, `ResultadoLicencia`, `CodigoInstalacion`, `ArchivoLicencia` (firma y verificación ECDSA). Guía en `MiPOS.Licencias/LEEME.md` | Nada |
| `MiPOS.GeneradorLicencias` | Consola de quien vende el sistema: `claves`, `emitir`, `codigo`. No se instala en los comercios | Licencias |
| `MiPOS.Pruebas` | Consola para pruebas manuales contra la base real (`net10.0-windows`) | Negocio, Datos, DatosLocal, Seguridad, Hardware, Facturacion |

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
- **Inyección de dependencias** registrada en `App.xaml.cs`. Ventanas, ViewModels y servicios como `Transient`. Excepciones: `Sesion` (una sola sesión para toda la aplicación) y `BaseLocal` (estado de la conexión, compartido por todos los servicios) son `Singleton`.
- **Cadena de conexión** en `MiPOS.WPF/appsettings.json` (`ConnectionStrings:MiPOS`), copiada a la salida con `CopyToOutputDirectory`.
- **Cultura `es-AR`** configurada en `App.xaml.cs`. Precios con formato `'$ {0:N2}'`.
- **Textos de la interfaz e idiomas (Fase 10):** el programa se sigue escribiendo en español (XAML, ViewModels y mensajes de las excepciones). Cada idioma tiene un catálogo `MiPOS.WPF/Idiomas/<código>.json` (recurso incrustado) cuya clave es el texto en español y el valor la traducción; los datos variables se escriben `{0}`, `{1}` (plantillas), y los datos también se traducen si están en el catálogo (un mensaje de error dentro de otro). `Traductor` hace la traducción; `TraduccionVentanas` traduce cada ventana al cargarse (registrado una vez en `App.xaml.cs`): textos fijos (Text, Content, Header, Title, ToolTip, encabezados de grillas), agrega el convertidor `ConvertidorTraduccion` a los Binding de texto, a las columnas de las grillas y a las listas desplegables (también traduce valores de enums, como `Rol`). Nunca toca `TextBox` (lo que escribe el usuario). Lo que falta en el catálogo se muestra en español. `DialogoServicio` traduce los `MessageBox` y el diálogo de archivos. **Los comprobantes (tickets, facturas, cierre) siempre salen en español** (documentos legales en Argentina). Idiomas: `es` y `zh` (chino simplificado). El idioma es por usuario (`Usuario.Idioma`, null = el de la caja; migración `AgregarIdiomaUsuario`, se elige en F9), y el de la caja está en `Idioma:Predeterminado` de `appsettings.json` (lo usa el inicio de sesión). Los números y la moneda siguen en formato `es-AR`. En chino las ventanas usan "Segoe UI, Microsoft YaHei UI". Al agregar un texto nuevo, agregar su traducción al catálogo.
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
- **Ticket:** lo arma `TicketVenta` (en `MiPOS.Negocio/Comprobantes`) siempre desde la venta guardada en la base, con letra de ancho fijo y montos en formato es-AR. Sin factura (facturación desactivada) o con la factura pendiente/rechazada dice "NO VÁLIDO COMO FACTURA". Se imprime automáticamente al cobrar; si falla, la venta queda registrada igual y se avisa para reimprimir con F8 (que marca "REIMPRESIÓN").
- **Datos del comercio:** el software se vende a distintos negocios, así que nombre, dirección, CUIT, teléfono y mensaje de pie van en la sección `Comercio` de `appsettings.json` (los vacíos no se imprimen). Más adelante pasarán a una pantalla de configuración.
- **Control de caja:** por ahora siempre activo (el interruptor del módulo llegará con la pantalla de configuración). Las cajas están en la tabla `Cajas` y cada PC indica la suya en `Caja:Numero` de `appsettings.json`. El turno (`TurnoCaja`) es de la caja, no del cajero: si cambia el usuario se sigue en el mismo turno, y cada venta guarda igual quién la hizo. La base impide dos turnos abiertos en la misma caja (índice único filtrado). Las ventas anteriores al control de caja quedan con `TurnoCajaId` en null.
- **Arqueo ciego:** al cerrar, el cajero informa el efectivo contado sin ver el esperado; después se muestra la diferencia (contado − esperado). Retiros e ingresos de efectivo llevan motivo y entran en el arqueo; el retiro requiere el permiso `RetirarEfectivo` (autorización de administrador, verificada de nuevo en la base) y no puede superar el efectivo esperado. El efectivo esperado se calcula solo en `ResumenTurno` (inicial + ventas en efectivo + ingresos − retiros). No se vende con la caja cerrada: `VentaServicio` lo exige y guarda el turno. Al entrar a la caja, si está cerrada se ofrece abrirla. El cierre imprime un comprobante (`TicketCierreCaja`) y requiere el carrito vacío. Errores de caja: `CajaInvalidaException` (mensaje mostrado tal cual).
- **Stock:** el módulo queda siempre activo por ahora (el interruptor llega con la pantalla de configuración). `Producto.Stock` solo cambia a través de un `MovimientoStock` (tipos Venta, Compra, Ajuste; cantidad con signo: positivo entra, negativo sale; guarda usuario, motivo y la venta o compra que lo originó). El cambio se hace con una sola instrucción en la base (`Stock = Stock + cantidad`, en `StockServicio.RegistrarMovimientoAsync`) dentro de la misma transacción que la venta o la compra, para que varias cajas no se pisen. Se puede vender sin stock: el stock queda negativo, el cajero ve un aviso y el producto aparece en las alertas. Alerta = stock negativo, o `StockMinimo > 0` y `Stock <= StockMinimo`. El ajuste por "stock contado" solo se aplica si el stock no cambió desde que se leyó. Errores de productos y stock: `ProductoInvalidoException`.
- **Productos:** el código se guarda en mayúsculas, sin espacios; `ProductoServicio.GenerarCodigoInternoAsync` propone el próximo `INT0001`. Cambiar el precio exige además el permiso `ModificarPrecios`. El stock inicial de un producto nuevo se registra como ajuste "Stock inicial". Las categorías se crean desde la edición de producto (no se borran; si existía desactivada, se reactiva).
- **Compras:** proveedores en tabla propia (nombre único, CUIT opcional validado con dígito verificador y guardado sin guiones). Registrar una compra suma stock en la misma transacción. Las compras no se anulan: un error se corrige con un ajuste de stock. Cada `DetalleCompra` guarda copia del nombre y el costo; el último costo se propone en la compra siguiente. Errores: `CompraInvalidaException`.
- **Atajos de administración:** ya no quedan teclas F libres, así que las pantallas de administración usan Ctrl + letra (Ctrl+P productos y stock, Ctrl+M compras). Los botones y atajos solo se habilitan con el permiso.
- **Facturación electrónica (ARCA):** proyecto `MiPOS.Facturacion` sin dependencias; `IServicioArca` es una interfaz aprobada por tener dos implementaciones reales (`ServicioArcaWs` para homologación/producción y `ServicioArcaSimulado` para desarrollo), igual que `IImpresoraTicket`. `Facturacion:Modo` = `Desactivada` (valor del repositorio), `Simulada`, `Homologacion` o `Produccion`. El CUIT del emisor es `Comercio:Cuit`; los datos fiscales (`CondicionIva`, `RazonSocial`, `IngresosBrutos`, `InicioActividades`, `MontoMaximoSinIdentificar`) están en `DatosFiscales`, en la misma sección. Cada caja usa su propio `PuntoVenta`. Monotributo/Exento emite C; Responsable Inscripto emite A (a inscriptos y monotributistas, con CUIT) o B. Los precios incluyen IVA: `CalculadoraFactura` separa neto e IVA por alícuota y reparte el descuento en proporción (la última alícuota se lleva el redondeo). `Producto.AlicuotaIva` (códigos de ARCA, 21% por defecto) se copia a `DetalleVenta`. La venta guarda su `Factura` **Pendiente en la misma transacción**; después `FacturacionServicio` pide el CAE. El número se guarda **antes** de pedir el CAE y, al reintentar, si ARCA ya tiene ese número se consulta (`FECompConsultar`) en vez de pedir otro: nunca se duplica una factura. Sin conexión la venta queda registrada, el ticket sale "PENDIENTE DE AUTORIZACIÓN" y se reintenta con la próxima venta o con Ctrl+F. Los rechazos quedan `Rechazada` (un administrador con `ConfigurarFacturacion` puede volverlas a pendiente). El permiso de acceso de WSAA (12 h) se guarda en `CarpetaDatos`. Factura B muestra "IVA contenido" (Ley 27.743); factura A discrimina el IVA; las facturas autorizadas llevan CAE, vencimiento y QR (RG 4892); las de `Simulada`/`Homologacion` dicen "SIN VALIDEZ FISCAL". F2 en el cobro identifica al cliente (DNI o CUIT, condición frente al IVA). Errores: `FacturaInvalidaException` (reglas, rechazos) y `FacturacionException` (comunicación; `Reintentable`). No hay notas de crédito todavía.
- **Hardware (Fase 7):** cada caja configura en `appsettings.json` las secciones `Cajon` (`Modo` = `Desactivado` en el repositorio, `Impresora` o `Serial`), `Lector` (`Teclado` en el repositorio, o `Serial`) y `Terminal` (`Manual` en el repositorio, `MercadoPagoPoint` o `Simulada`). **Excepción aprobada a "no usar ESC/POS":** el cajón se abre mandando un único comando crudo configurable (`Cajon:Comando`, por defecto `1B 70 00 19 FA`) a la cola de la impresora en modo RAW (winspool) o al puerto serie; los tickets siguen saliendo por el driver. El cajón se abre solo en ventas en efectivo, apertura, F7 y cierre; a mano con Ctrl+G y el permiso `AbrirCajon` (autorización). Si falla el cajón, se avisa sin interrumpir. El lector serie entrega los códigos al hilo de la pantalla y los ignora si hay otra ventana abierta; el puerto se libera al cerrar la caja. `ITerminalPago` es una interfaz aprobada (dos implementaciones: Mercado Pago Point con la Orders API y simulada). `MedioPago.UsaTerminal` marca débito, crédito y Mercado Pago; `Venta.ReferenciaPago` guarda el cupón (terminal manual, opcional) o la operación de Mercado Pago (migración `AgregarTerminalPago`). Con terminal integrada se cobra **antes** de registrar la venta; la referencia del cobro se reutiliza en los reintentos (`X-Idempotency-Key`), y si el pago se aprobó pero falló el registro, Enter reintenta solo el registro. Mientras la terminal tiene el cobro, Esc pide cancelar pero se sigue esperando el resultado (nunca se abandona un cobro en curso). Ctrl+H (permiso `ConfigurarHardware`): prueba de impresora (`TicketPrueba`), cajón y lector.
- **Venta sin conexión (Fase 8):** cada caja tiene una base SQLite (`SinConexion:ArchivoBase`, por defecto `DatosLocales\caja.db` junto al programa) con una copia de productos activos, medios de pago activos y usuarios activos (con el hash, para iniciar sesión), la caja y su turno abierto. Se reemplaza completa cada `MinutosEntreDescargas` (y al entrar a la caja); la caja y el turno, en cada sincronización. `BaseLocal.EsCorte` decide si un error es un corte de conexión (`ErroresConexion`: errores de red de SQL Server, no los de reglas) y pasa toda la aplicación a modo sin conexión; desde ahí las búsquedas, los medios de pago, el inicio de sesión, la autorización y las ventas van directo a la base local, sin esperar el tiempo de conexión (`Connect Timeout=5` en la cadena). `SincronizacionServicio` corre cada `SegundosEntreIntentos` desde la caja (en otro hilo): prueba la conexión, sube las ventas pendientes de a una (cada una en su transacción, con su movimiento de stock "Venta sin conexión"), autoriza sus facturas y descarga la copia. Toda venta tiene `IdentificadorUnico` (Guid, índice único filtrado, migración `AgregarIdentificadorUnicoVenta`): una venta que llegue dos veces no se duplica. La venta local se guarda como JSON (`VentaPendiente.Datos`) con su factura pendiente; al subirse no se vuelven a validar las reglas (ya se cobró). Sin conexión no se puede: abrir ni cerrar la caja, retiros/ingresos, pantallas de administración, facturar (el ticket sale "Venta S/C N° x", "PENDIENTE DE AUTORIZACIÓN" o "NO VÁLIDO COMO FACTURA"). El bloqueo por intentos fallidos sin conexión es solo en memoria. El cierre de caja exige que no queden ventas sin subir. F8 reimprime la última venta sin conexión de la sesión. `SinConexion:Activo = false` vuelve al comportamiento anterior. Los servicios reciben `BaseLocal?` como parámetro opcional (sin ella, en las pruebas, trabajan solo con la base central).
- **Licencias (Fase 9):** una por PC, ligada al código de instalación (`CodigoInstalacion`: SHA-256 del `MachineGuid` de Windows, 16 caracteres). El archivo `licencia.lic` (`Licencia:Archivo`) tiene los datos (`Licencia`: número, comercio, CUIT, código, emisión, vencimiento opcional) firmados con ECDSA P-256; el programa trae solo la clave pública (`ArchivoLicencia.ClavePublica`). La clave privada está fuera del repositorio (`%USERPROFILE%\MiPOS-Licencias\clave-privada.txt`) y se usa con `MiPOS.GeneradorLicencias`. Sin licencia válida (falta, dañada, de otra PC o vencida) hay **modo demostración**: todo funciona, los comprobantes dicen "MODO DEMOSTRACIÓN - SIN LICENCIA" (`TicketServicio`) y `FacturacionServicio` no factura en `Produccion`. Al arrancar sin licencia se muestra la ventana de licencia (se puede continuar). Ctrl+L la abre desde la caja; reemplazar una licencia válida requiere `GestionarLicencia`. Errores: `LicenciaInvalidaException`. Guía en `MiPOS.Licencias/LEEME.md`.
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

**Atajos de teclado de la caja:** F1 buscar, F2 producto, F3 cobrar, F4 eliminar, F5 cambiar cantidad, F6 descuento, F7 retiro/ingreso de efectivo, F8 reimprimir último ticket, F9 usuarios (solo administrador), F10 cambiar mi contraseña, F11 abrir/cerrar caja, F12 cerrar sesión, ESC cancelar, Ctrl+P productos y stock, Ctrl+M compras (estos dos, solo administrador), Ctrl+F facturas sin autorizar, Ctrl+G abrir cajón (con autorización), Ctrl+H prueba de hardware (administrador), Ctrl+L licencia. En el cobro, F2 cliente de la factura.

**Medios de pago:** efectivo, débito, crédito, Mercado Pago, QR, transferencia. Un único medio por venta. En efectivo se calcula el vuelto.

**Módulos opcionales** (se activan/desactivan desde configuración): stock (con mínimos y alertas), compras (requiere stock), control de caja (apertura, cierre y arqueo), cajón de dinero.

**Otros requisitos:** varias cajas simultáneas contra una base central; ventas básicas sin Internet; facturación con ARCA; impresora térmica; contraseñas con hashing seguro; licenciamiento por instalación; idiomas español y chino.

## Plan de fases

1. POS básico — **terminada**
2. Usuarios (Cajero, Administrador, permisos, autorización por contraseña) — **terminada**
3. Impresión (ticket, impresora térmica) — **terminada** (pendiente la prueba en pantalla junto con el testeo general)
4. Control de caja — **terminada** (pendiente la prueba en pantalla junto con el testeo general)
5. Stock y compras — **terminada** (pendiente la prueba en pantalla junto con el testeo general)
6. Facturación (ARCA) — **terminada** (probada en modo Simulada; falta probar en Homologacion con un certificado real)
7. Hardware (scanner, impresora, cajón, terminal POS) — **terminada** (probada sin dispositivos reales: falta probar con cajón, lector serie y terminal Point físicos)
8. Offline y sincronización — **terminada** (probada con una base central inaccesible desde `MiPOS.Pruebas`; falta probar desenchufando la red en una caja real)
9. Licenciamiento — **terminada**
10. Multidioma (español y chino) — **terminada** (revisada con capturas de pantallas en chino; conviene que un hablante nativo revise el catálogo)

## Estado de la Fase 10 (terminada)

- [x] 10.1 `Traductor` y catálogo `Idiomas/zh.json` (unos 580 textos y plantillas, incluidos los mensajes de error del negocio)
- [x] 10.2 `TraduccionVentanas`: traducción automática de cada ventana, Binding, grillas y listas
- [x] 10.3 Idioma por usuario (`Usuario.Idioma`, migración `AgregarIdiomaUsuario`, F9) e idioma de la caja (`Idioma:Predeterminado`); base local versión 2

## Estado de la Fase 9 (terminada)

- [x] 9.1 Proyecto `MiPOS.Licencias`: código de instalación, archivo firmado (ECDSA P-256), estados
- [x] 9.2 `MiPOS.GeneradorLicencias` (claves fuera del repositorio) y guía `MiPOS.Licencias/LEEME.md`
- [x] 9.3 `LicenciaServicio`, modo demostración en tickets y bloqueo de facturación en Produccion
- [x] 9.4 Ventana de licencia al arrancar y con Ctrl+L, aviso en la caja

## Estado de la Fase 8 (terminada)

- [x] 8.1 Proyecto `MiPOS.DatosLocal` (SQLite) y `Venta.IdentificadorUnico` (migración `AgregarIdentificadorUnicoVenta`)
- [x] 8.2 `BaseLocal`, `ErroresConexion`: búsqueda, medios de pago, inicio de sesión, caja y turno con la copia local
- [x] 8.3 Registro de la venta sin conexión y ticket "Venta S/C"
- [x] 8.4 `SincronizacionServicio`: subida sin duplicar, stock, facturas, descarga del catálogo; aviso en la caja y cierre bloqueado con ventas sin subir

## Estado de la Fase 7 (terminada)

- [x] 7.1 Cajón de dinero por impresora (RAW) o puerto serie, apertura automática y manual (Ctrl+G con autorización)
- [x] 7.2 Lector de códigos por puerto serie (los USB tipo teclado ya funcionaban)
- [x] 7.3 Terminal de pago: manual con cupón, Mercado Pago Point integrado y simulada; `UsaTerminal` y `ReferenciaPago` (migración `AgregarTerminalPago`)
- [x] 7.4 Pantalla de prueba de hardware (Ctrl+H) y guía `MiPOS.Hardware/LEEME.md`

## Estado de la Fase 6 (terminada)

- [x] 6.1 Proyecto `MiPOS.Facturacion`: WSAA (firma CMS con el certificado, permiso guardado 12 h), WSFEv1 (último número, CAE, consulta), modo simulado, texto del QR
- [x] 6.2 `Factura` y `FacturaIva`, `AlicuotaIva` en productos y detalles (migración `AgregarFacturacion`), `CalculadoraFactura`, `DatosReceptor`
- [x] 6.3 `FacturacionServicio`: factura pendiente en la transacción de la venta, CAE con número guardado antes de pedirlo, reintentos sin duplicar
- [x] 6.4 Ticket fiscal (A, B, C, pendiente) con CAE y QR impreso
- [x] 6.5 Pantallas: F2 cliente en el cobro, Ctrl+F facturas sin autorizar, IVA en la edición de producto, aviso en la caja

Pendiente para más adelante: notas de crédito (anular o devolver una venta facturada), CAEA (contingencia de ARCA).

## Estado de la Fase 5 (terminada)

- [x] 5.1 `Stock` y `StockMinimo` en `Producto`, entidad `MovimientoStock` (migración `AgregarStock`)
- [x] 5.2 `StockServicio` (ajuste manual, stock contado, alertas, historia) y descuento de stock al vender, en la misma transacción
- [x] 5.3 Pantallas: productos (Ctrl+P: alta, edición, código interno, categorías, activar/desactivar), ajuste de stock, movimientos, aviso de stock bajo en la caja
- [x] 5.4 Compras: `Proveedor`, `Compra`, `DetalleCompra` (migración `AgregarCompras`); registrar una compra suma stock
- [x] 5.5 Pantallas de compras (Ctrl+M), carga de compra y proveedores

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
