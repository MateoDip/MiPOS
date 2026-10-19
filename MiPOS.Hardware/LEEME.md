# Hardware de la caja: cómo configurarlo

Cada caja (PC) configura sus dispositivos en su `appsettings.json`. Después de configurar, entrar con un administrador y presionar **Ctrl+H** (prueba de hardware).

## Impresora de tickets (`Impresion`)

Cualquier impresora instalada en Windows con su driver (térmica de 80 o 58 mm, o una común con hojas).

- `Modo`: `Windows` para imprimir de verdad, `Archivo` para guardar los tickets como texto (desarrollo).
- `Impresora`: el nombre tal como aparece en Windows (vacío = la predeterminada).
- `AnchoPapelMm` y `CaracteresPorLinea`: 80 mm → 42 caracteres; 58 mm → 32 caracteres.
- El "ticket de prueba" de Ctrl+H imprime una regla: tiene que ocupar exactamente una línea. Si se corta, bajar `CaracteresPorLinea`.

## Cajón de dinero (`Cajon`)

| `Modo` | Cuándo usarlo |
|---|---|
| `Desactivado` | No hay cajón, o el driver de la impresora ya lo abre solo al imprimir (algunos drivers tienen la opción "abrir cajón al final del documento"). |
| `Impresora` | El cajón está enchufado a la impresora de tickets con el cable telefónico (RJ11). Es lo más común. `Impresora` vacío = la misma de los tickets. |
| `Serial` | El cajón tiene su propio cable USB/serie y aparece como un puerto COM (ver el Administrador de dispositivos de Windows). |

`Comando`: el pulso de apertura. Casi siempre `1B 70 00 19 FA`; si no abre, probar `1B 70 01 19 FA` (pin 5).

El cajón se abre solo en las ventas en efectivo, al abrir la caja, al registrar un retiro o ingreso (F7) y al cerrar la caja (para contar). **Ctrl+G** lo abre a mano, con autorización de un administrador.

## Lector de códigos de barras (`Lector`)

- `Teclado` (lo más común): el lector USB funciona como un teclado. No hay que configurar nada; solo verificar que el lector mande un **Enter** al final de cada código (viene así de fábrica en casi todos).
- `Serial`: lectores que se conectan por puerto serie (o USB que aparece como COM). Indicar `Puerto` y `Baudios`. Si llega un código mientras hay otra ventana abierta (por ejemplo, el cobro), se ignora y se avisa.

## Terminal de pago con tarjeta (`Terminal`)

Los medios de pago Débito, Crédito y Mercado Pago usan la terminal.

| `Modo` | Cómo funciona |
|---|---|
| `Manual` | La terminal (Posnet, Lapos, Getnet, Payway, Clover…) no está conectada al programa. El cajero marca el monto en la terminal y, en el cobro, puede anotar el número de cupón o autorización (opcional). |
| `MercadoPagoPoint` | El monto se manda solo a la terminal Point y el programa espera el resultado. Se puede cancelar con Esc mientras la terminal no tomó el cobro; si el cliente ya está pagando, hay que cancelar en la terminal. |
| `Simulada` | Para probar sin terminal: aprueba a los 3 segundos y rechaza los montos terminados en ,99. |

Para `MercadoPagoPoint`:
1. En la cuenta de Mercado Pago del comercio (Tus integraciones), crear una aplicación y copiar el **Access Token** de producción.
2. Poner la terminal Point en **modo PDV** (punto de venta integrado) siguiendo la guía de Mercado Pago, y anotar su **identificador** (`terminal_id`).
3. Completar `AccessToken` y `TerminalId`. El Access Token permite cobrar en nombre del comercio: no compartirlo.

Si la terminal aprueba el pago pero la venta no se puede registrar (por ejemplo, se cortó la base de datos), el cobro muestra un aviso en rojo: hay que reintentar con Enter (no se vuelve a cobrar).
