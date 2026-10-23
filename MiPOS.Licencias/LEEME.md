# Licencias de MiPOS

Cada PC donde se instala MiPOS necesita su propia licencia. Sin licencia, el sistema funciona completo
en **modo demostración**:

- todos los tickets y comprobantes salen con la leyenda `*** MODO DEMOSTRACIÓN - SIN LICENCIA ***`;
- no se pueden emitir facturas reales (`Facturacion:Modo = Produccion`). Sí se puede probar con `Simulada` y `Homologacion`;
- la caja muestra `MODO DEMOSTRACIÓN · Ctrl+L` arriba.

## Cómo funciona

1. Cada PC tiene un **código de instalación** de 16 letras y números (ej: `MDRH-55LV-6UVD-D2N4`).
   Sale del identificador que Windows le da a la instalación (`MachineGuid`): no cambia al reiniciar
   ni al actualizar MiPOS, pero **sí cambia si se reinstala Windows** (hay que emitir una licencia nueva).
2. El comercio le pasa ese código a quien le vende el sistema.
3. Quien vende emite el archivo `licencia.lic` con `MiPOS.GeneradorLicencias`. El archivo va **firmado
   digitalmente** (ECDSA P-256): si se cambia un solo dato (el comercio, la fecha), deja de valer.
4. En la PC del comercio se instala con **Ctrl+L → Instalar archivo de licencia...** (o en la ventana
   que aparece al arrancar sin licencia). Queda copiado como `licencia.lic` junto al programa
   (`Licencia:Archivo` en `appsettings.json`).

Una licencia puede no vencer nunca o tener fecha de vencimiento (abonos). 15 días antes del vencimiento
la caja lo avisa. Una licencia válida solo la puede reemplazar un administrador (permiso `GestionarLicencia`).

## Las claves (MUY IMPORTANTE)

- La **clave privada** firma las licencias. Está en `C:\Users\<usuario>\MiPOS-Licencias\clave-privada.txt`
  de la PC del desarrollador. **Nunca** se sube al repositorio ni se instala en los comercios.
  **Haga una copia de seguridad** (un pendrive guardado, un gestor de contraseñas): si se pierde,
  no se pueden emitir más licencias para esta versión del programa.
- La **clave pública** está en el código (`ArchivoLicencia.ClavePublica`) y solo sirve para verificar.

Si la clave privada se pierde o se filtra: generar un par nuevo (`claves`, después de mover la anterior),
reemplazar `ClavePublica`, compilar y volver a emitir todas las licencias.

## Comandos del generador

```powershell
# Una sola vez: crear el par de claves (no reemplaza una existente)
dotnet run --project MiPOS.GeneradorLicencias -- claves

# Emitir una licencia
dotnet run --project MiPOS.GeneradorLicencias -- emitir --codigo MDRH-55LV-6UVD-D2N4 --comercio "Almacén Don Pepe" --cuit 20123456789 --numero 2026-0001 --vence 2027-12-31 --salida licencia.lic

# Ver el código de instalación de la PC donde se ejecuta
dotnet run --project MiPOS.GeneradorLicencias -- codigo
```

`--vence` es opcional (sin él la licencia no vence). `--numero` es opcional (por defecto, fecha y hora).
Al emitir, el generador verifica la licencia con la clave pública del programa y avisa si no coinciden.

## Límites conocidos

- La fecha se toma del reloj de la PC: atrasar el reloj extiende una licencia con vencimiento.
- Es una protección para uso comercial honesto, no contra alguien que modifique el programa.
