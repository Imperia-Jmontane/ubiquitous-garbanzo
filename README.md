# Gestión de datos y comunicación con GitHub

## Resumen operativo
- La aplicación separa los secretos persistentes (PAT y credenciales OAuth) de los datos relacionales, usando un almacén cifrado en disco y Entity Framework Core para la parte transaccional.【F:MyApp/MyApp/Infrastructure/Secrets/DataProtectedWritableSecretStore.cs†L44-L151】【F:MyApp/MyApp/Data/ApplicationDbContext.cs†L16-L116】
- El backend coordina autenticaciones con GitHub mediante un flujo OAuth completo y reutiliza tokens personales para clonar repositorios con privilegios del usuario.【F:MyApp/MyApp/Application/GitHubOAuth/Commands/StartGitHubOAuth/StartGitHubOAuthCommandHandler.cs†L40-L66】【F:MyApp/MyApp/Infrastructure/Git/LocalRepositoryService.cs†L378-L470】

## Secretos y credenciales
- Los secretos se guardan en `App_Data/secret-store.json`, cifrados con IDataProtector y protegidos por un `SemaphoreSlim` que serializa el acceso, garantizando lecturas/escrituras atómicas.【F:MyApp/MyApp/Infrastructure/Secrets/DataProtectedWritableSecretStore.cs†L44-L107】
- Antes de acudir a la configuración, `ConfigurationSecretProvider` intenta resolver cada secreto desde el almacén persistente; los placeholders (`${...}`) se ignoran para evitar valores ficticios.【F:MyApp/MyApp/Infrastructure/Secrets/ConfigurationSecretProvider.cs†L30-L67】
- El `GitCredentialStore` extrae `GitHubClientId` y `GitHubClientSecret` del proveedor de secretos y bloquea cualquier consumo si faltan, evitando solicitudes sin credenciales válidas.【F:MyApp/MyApp/Infrastructure/GitHub/GitCredentialStore.cs†L21-L33】
- El inspector de tokens personales valida scopes y formato antes de persistir el PAT; solo tras superar las comprobaciones se sobrescribe la clave `GitHubPersonalAccessToken` en el almacén cifrado.【F:MyApp/MyApp/Application/GitHubPersonalAccessToken/Commands/ConfigureGitHubPersonalAccessToken/ConfigureGitHubPersonalAccessTokenCommandHandler.cs†L34-L75】
- El proveedor de settings de OAuth combina configuración dinámica y secretos almacenados para exponer el estado (`IsConfigured`) y los scopes activos que usarán los controladores durante el bootstrap.【F:MyApp/MyApp/Infrastructure/GitHub/GitHubOAuthSettingsProvider.cs†L32-L68】

## Flujo OAuth de GitHub
- Iniciar el flujo genera un `state` aleatorio, lo persiste en la tabla `GitHubOAuthStates` con expiración de 10 minutos y construye la URL de autorización usando scopes configurados y la `redirect_uri` solicitada.【F:MyApp/MyApp/Application/GitHubOAuth/Commands/StartGitHubOAuth/StartGitHubOAuthCommandHandler.cs†L40-L124】
- Al recibir el callback, el manejador valida y consume el estado, intercambia el código por tokens vía `GitHubOAuthClient` y guarda/actualiza el registro único del usuario en `UserExternalLogins`, incluyendo `AccessToken`, `RefreshToken` y `ExpiresAt`.【F:MyApp/MyApp/Application/GitHubOAuth/Commands/LinkGitHubAccount/LinkGitHubAccountCommandHandler.cs†L59-L112】
- El cliente OAuth invoca el endpoint de tokens de GitHub usando autenticación básica y serializa el payload JSON requerido tanto para intercambios iniciales como renovaciones de refresh token.【F:MyApp/MyApp/Infrastructure/GitHub/GitHubOAuthClient.cs†L31-L105】

## Uso del PAT y operaciones con repositorios
- Durante un `git clone`, el servicio local intenta recuperar el PAT almacenado y, si existe, genera una URL autenticada para ejecutar el comando sin prompts interactivos, controlando el ciclo de vida del proceso y los mensajes de progreso.【F:MyApp/MyApp/Infrastructure/Git/LocalRepositoryService.cs†L378-L516】

## Esquema de base de datos y persistencia de tokens
- `UserExternalLogins` almacena un vínculo por usuario/proveedor con campos para `ExternalUserId`, `AccessToken`, `RefreshToken`, `ExpiresAt` y metadatos de auditoría (`CreatedAt`, `UpdatedAt`), reforzado por un índice único sobre `(UserId, Provider)`.【F:MyApp/MyApp/Data/ApplicationDbContext.cs†L51-L74】【F:MyApp/MyApp/Domain/Identity/UserExternalLogin.cs†L7-L73】
- `GitHubOAuthStates` conserva estados temporales con `RedirectUri`, ventana de expiración y un índice único sobre `State` para impedir reutilizaciones, además de utilidades de dominio para verificar caducidad.【F:MyApp/MyApp/Data/ApplicationDbContext.cs†L76-L94】【F:MyApp/MyApp/Domain/Identity/GitHubOAuthState.cs†L7-L44】
- `AuditTrailEntries` registra eventos (`EventType`, `Provider`, `Payload`, `OccurredAt`, `CorrelationId`) que permiten auditar actividades críticas en GitHub.【F:MyApp/MyApp/Data/ApplicationDbContext.cs†L96-L116】【F:MyApp/MyApp/Domain/Observability/AuditTrailEntry.cs†L7-L44】

## Auditoría y trazabilidad
- Cuando se completa el enlace de una cuenta de GitHub, se publica un evento de dominio que encapsula scopes concedidos, si es un vínculo nuevo y la capacidad de clonado.【F:MyApp/MyApp/Application/GitHubOAuth/Events/GitHubAccountLinkedEvent.cs†L7-L47】
- El manejador del evento serializa un payload JSON con los detalles y lo persiste en `AuditTrailEntries`, dejando trazabilidad de cada vinculación junto con logs de éxito o error.【F:MyApp/MyApp/Application/GitHubOAuth/Events/GitHubAccountLinkedEventHandler.cs†L24-L57】
