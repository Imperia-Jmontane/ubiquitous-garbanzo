# Cómo vincular GitHub

## Prerrequisitos
- Tener una cuenta de GitHub con permisos sobre los repositorios que deseas clonar.
- Contar con los secretos configurados en la aplicación (`GITHUB__CLIENT_ID`, `GITHUB__CLIENT_SECRET`, `GITHUB__WEBHOOK_SECRET`, `GITHUB__PERSONAL_ACCESS_TOKEN`).
- Acceso a la instancia desplegada de MyApp y a la URL de callback registrada en GitHub.

## Flujo paso a paso
1. Inicia sesión en MyApp y ve a la pantalla **Conectar GitHub**.
2. Pulsa **Iniciar vinculación**. El backend ejecuta `StartGitHubOAuthCommand` para generar el estado y la URL de autorización.
3. Serás redirigido a `github.com/login/oauth/authorize`. Revisa los scopes solicitados (`repo`, `workflow`, `read:user`) y acepta.
4. GitHub regresará a la `redirect_uri` configurada. El backend llamará `LinkGitHubAccountCommand` para intercambiar el código y guardar los tokens.
5. Verás un mensaje de confirmación. El backend emite el evento `GitHubAccountLinkedEvent` y registra la auditoría.

## Verificación manual
1. Repite el flujo anterior y valida que en la base de datos exista un registro en `UserExternalLogins` con tu `UserId` y `Provider = GitHub`.
2. Comprueba en la tabla `AuditTrailEntries` que se registró el evento `GitHubAccountLinked`.
3. Ejecuta el comando `dotnet test` y verifica que todos los tests terminen en verde antes de hacer deploy.
4. Consulta las métricas publicadas (`github.oauth.link.success.count`, `github.oauth.link.failure.count`) para confirmar que solo se incrementa un éxito.

## Resolución de problemas
- **Error de estado inválido**: vuelve a iniciar la vinculación desde la app; los estados expiran cada 10 minutos.
- **Scopes insuficientes**: revisa los scopes concedidos en GitHub y vuelve a autorizar garantizando `repo`, `workflow` y `read:user`.
- **Clonado bloqueado**: asegúrate de que la cuenta tenga acceso al repositorio y que el token no haya expirado. Puedes renovar desde el flujo de refresco en ajustes.
