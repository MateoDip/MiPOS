# Factura electrónica con ARCA: cómo configurarla

MiPOS pide el CAE a ARCA con los servicios web **WSAA** (autenticación) y **WSFEv1** (factura electrónica A, B y C).
Todo se configura en la sección `Facturacion` de `appsettings.json` de cada caja.

## Modos (`Facturacion:Modo`)

| Modo | Para qué | Validez fiscal |
|---|---|---|
| `Desactivada` | Sin factura electrónica. El ticket dice "NO VÁLIDO COMO FACTURA". | No |
| `Simulada` | Desarrollar y probar sin certificado ni Internet. Inventa el CAE. | **No** (el ticket lo aclara) |
| `Homologacion` | Probar contra los servidores de prueba de ARCA. | **No** (el ticket lo aclara) |
| `Produccion` | Uso real en el comercio. | Sí |

Para probar `Simulada` alcanza con poner un CUIT válido en `Comercio:Cuit`.

## Pasos para un comercio (con el contador)

1. **Clave fiscal nivel 3** del titular del CUIT.
2. **Certificado digital**:
   - Generar una clave privada y un pedido de certificado (CSR), por ejemplo con OpenSSL:
     ```
     openssl genrsa -out mipos.key 2048
     openssl req -new -key mipos.key -subj "/C=AR/O=NOMBRE DEL COMERCIO/CN=MiPOS/serialNumber=CUIT 20123456786" -out mipos.csr
     ```
   - Homologación: en ARCA, servicio **"WSASS - Autogestión Certificados Homologación"**: subir el CSR y descargar el `.crt`. Después, en el mismo servicio, crear la autorización para el servicio **wsfe**.
   - Producción: servicio **"Administración de Certificados Digitales"**: subir el CSR y descargar el `.crt`. Después, en **"Administrador de Relaciones de Clave Fiscal"**, asociar el certificado al servicio **"Facturación Electrónica" (wsfe)**.
3. **Punto de venta**: en **"Administración de puntos de venta y domicilios"**, crear uno del tipo **"Factura Electrónica - Monotributo - Web Services"** o **"RECE para aplicativo y web services"**. **Cada caja necesita su propio punto de venta.**
4. Copiar `mipos.crt` y `mipos.key` a la PC de la caja (en una carpeta a la que solo accedan los encargados: la clave privada permite facturar en nombre del comercio) y completar:
   ```json
   "Facturacion": {
     "Modo": "Homologacion",
     "PuntoVenta": 3,
     "CondicionIva": "ResponsableInscripto",
     "RazonSocial": "Nombre legal en ARCA",
     "IngresosBrutos": "901-123456-7",
     "InicioActividades": "01/03/2020",
     "RutaCertificado": "C:\\MiPOS\\Certificados\\mipos.crt",
     "RutaClavePrivada": "C:\\MiPOS\\Certificados\\mipos.key"
   }
   ```
   También sirve un archivo `.pfx` en `RutaCertificado` (con `ContrasenaCertificado` si tiene).
5. Hacer ventas de prueba en `Homologacion`. Cuando todo funcione, tramitar el certificado de producción y pasar a `Produccion`.

## Qué pasa si no hay Internet

La venta se guarda siempre. La factura queda **pendiente** y el ticket sale como "PENDIENTE DE AUTORIZACIÓN - NO VÁLIDO COMO FACTURA".
Con la próxima venta que tenga conexión, o desde **Ctrl+F** en la caja, se piden los CAE pendientes. Después se puede reimprimir el ticket (F8 o desde Ctrl+F) y sale como factura.

## Datos que conviene confirmar con el contador

- `CondicionIva` del comercio (Monotributo emite C; Responsable Inscripto emite A o B).
- `MontoMaximoSinIdentificar`: desde qué total ARCA exige identificar al consumidor final (cambia con el tiempo).
- La alícuota de IVA de cada producto (pantalla de productos, Ctrl+P).
