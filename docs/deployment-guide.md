# Manual de despliegue

## Preparación del entorno
1. Instala .NET SDK 9.0 y un servidor SQL Server (o la base indicada en `appsettings.json`).
2. Configura variables de entorno para todos los secretos requeridos:
   - `GITHUB__CLIENT_ID`
   - `GITHUB__CLIENT_SECRET`
   - `GITHUB__WEBHOOK_SECRET`
   - `GITHUB__PERSONAL_ACCESS_TOKEN`
   - `CODEX__CHATGPT_SESSION_TOKEN`
3. Registra la URL pública de tu instancia en la app OAuth de GitHub como `Authorization callback URL`.

## Configuración de la aplicación
1. Copia `appsettings.json` y `appsettings.Development.json` si necesitas personalizarlos.
2. Asegúrate de que `ConnectionStrings:DefaultConnection` apunte a tu base de datos.
3. Ejecuta las migraciones de Entity Framework:
   ```bash
   dotnet ef database update --project MyApp/MyApp/MyApp.csproj
   ```

## Despliegue
1. Publica la aplicación:
   ```bash
   dotnet publish MyApp/MyApp/MyApp.csproj -c Release -o out
   ```
2. Copia el contenido de `out/` al servidor destino.
3. Define las variables de entorno en el servicio (systemd, IIS o contenedor) usando un gestor de secretos seguro.
4. Arranca el servicio web. Verifica los logs de Serilog para confirmar arranque sin errores.

## Post-despliegue
1. Accede a `/swagger` y comprueba que la documentación carga correctamente.
2. Ejecuta el flujo de vinculación de GitHub en el entorno productivo y verifica que se almacenen tokens y auditorías.
3. Monitorea las métricas expuestas (`github.oauth.link.*`, `github.oauth.refresh.*`) para confirmar que el hardening quedó operativo.
4. Actualiza la checklist del MVP registrando la fecha y responsable del despliegue.

## Rollback rápido
1. Detén el servicio actual.
2. Restaura la versión previa del paquete publicado.
3. Ejecuta las migraciones de rollback si aplica (`dotnet ef database update <MigrationAnterior>`).
4. Arranca nuevamente el servicio y valida healthcheck.
