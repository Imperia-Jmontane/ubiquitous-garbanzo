# Manual de despliegue - Vinculación de GitHub

Este documento describe los pasos necesarios para desplegar la funcionalidad de vinculación OAuth con GitHub en entornos de preproducción o producción.

## Requisitos previos
- **Base de datos**: aplicar las migraciones generadas en `MyApp.Infrastructure` sobre una instancia de SQL Server.
- **Secret Manager / Azure Key Vault**: disponer de un almacén seguro con acceso concedido a la identidad administrada o credenciales del servicio.
- **Registro OAuth**: contar con una aplicación OAuth en GitHub con los scopes `repo` y `read:user` habilitados y las URIs de redirección autorizadas.

## Configuración
1. **Variables de configuración**
   - `GitHubOAuth:ClientId` (cargar desde secreto)
   - `GitHubOAuth:ClientSecret` (cargar desde secreto)
   - `GitHubOAuth:Scopes` (por defecto `repo read:user`)
   - `GitHubOAuth:AllowedRedirectUris` (lista separada por comas)
   - `GitHubOAuth:AuthorizeUrl` y `TokenUrl` (usar endpoints de GitHub, permiten overrides para pruebas)
   - `Serilog:WriteTo` y `Serilog:MinimumLevel` según políticas del entorno
   - `ConnectionStrings:DefaultConnection`
2. **Carga de secretos**
   - Registrar `ClientId` y `ClientSecret` en Azure Key Vault o Secret Manager.
   - Configurar `KeyVault:Uri` en `appsettings.Production.json` o variables de entorno.
   - Garantizar que el servicio tenga permisos `get`/`list` sobre los secretos.
3. **Credenciales de clonado**
   - Habilitar la clave `GitCredentials` en el almacén de secretos para permitir la persistencia de PATs cifrados.

## Despliegue
1. Ejecutar `dotnet ef database update` en el proyecto `MyApp` apuntando al entorno objetivo.
2. Publicar la API (`dotnet publish MyApp/MyApp.csproj -c Release`).
3. Desplegar artefactos y configurar variables/secrets en la infraestructura (Kubernetes, App Service, etc.).
4. Validar conectividad con GitHub ejecutando el comando `POST /api/auth/github/start` desde un cliente autorizado.

## Post-despliegue
- Revisar logs de Serilog asegurando que se correlacionen por `state` y `userId`.
- Monitorizar métricas de `IGitHubLinkMetrics` para confirmar tasas de éxito y fallos.
- Configurar alertas para errores repetidos en el intercambio `code` ↔ `token`.
- Verificar que las entradas de auditoría aparezcan en `UserExternalLogins`.
