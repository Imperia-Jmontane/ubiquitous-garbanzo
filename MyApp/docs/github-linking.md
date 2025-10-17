# Cómo vincular GitHub

Este flujo permite que un usuario autorice a la aplicación para clonar repositorios en su nombre mediante OAuth. La integración se expone a través de los endpoints `POST /api/auth/github/start`, `POST /api/auth/github/callback` y `POST /api/auth/github/refresh`.

## Pasos
1. **Inicio del enlace**. El cliente invoca `POST /api/auth/github/start` proporcionando `userId` y `redirectUri`. La respuesta devuelve la URL de autorización de GitHub y el `state` que debe persistirse en el cliente para validar el callback.
2. **Redirección a GitHub**. El usuario sigue la URL, revisa los scopes `repo` y `read:user` y autoriza la aplicación.
3. **Callback**. GitHub redirige al `redirectUri` con `code` y `state`. El cliente reenvía esta información a `POST /api/auth/github/callback`. El backend intercambia el código por tokens, almacena las credenciales cifradas en el almacén de secretos y registra la vinculación en la base de datos.
4. **Refresco opcional**. Cuando el token expira se puede llamar a `POST /api/auth/github/refresh` para solicitar uno nuevo siempre que exista `refresh_token`.

## Seguridad
- Los tokens nunca se devuelven al cliente. Solo se expone la identidad de GitHub y la bandera `canClone` que indica si los scopes permiten clonar.
- Las credenciales persistentes se guardan cifradas mediante `IDataProtectionProvider` y se envían a otros servicios exclusivamente a través de la interfaz `IGitCredentialStore`.
- Se registran eventos de auditoría en la tabla `UserExternalLogins` y se emite el evento de dominio `GitHubAccountLinkedEvent` para integraciones posteriores.

## Métricas y auditoría
- Cada enlace registra métricas de éxito o fallo mediante `IGitHubLinkMetrics`, lo que permite alimentar dashboards y alertas.
- El `state` y el `userId` se incorporan al logging estructurado de Serilog para facilitar la trazabilidad.

## Requisitos previos
- Registrar la aplicación OAuth en GitHub y configurar el `ClientId`, `ClientSecret` y la lista de `AllowedRedirectUris` en `appsettings.json` o en el almacén de secretos.
- Proveer una base de datos SQL Server accesible para las migraciones generadas (`GitHubAccountLinks` y `UserExternalLogins`).
