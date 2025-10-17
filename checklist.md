# MVP Checklist — Visualizador de repos GitHub con flowcharts (Codex CLI con login ChatGPT)

> Objetivo: Ingerir un repo de GitHub, generar un flowchart multinivel (L1/L2/L3/L4/.../Ln) con trazabilidad a líneas de código, y visualizarlo en web (ASP.NET MVC + EF backend, Tailwind frontend). El flowchart lo genera una instancia **Codex CLI** autenticada con **cuenta de ChatGPT** y se guarda como comentarios en el código. El repostiorio de Github se clona iniciando sesión con la cuenta del usuario.

---

## 0) Prerrequisitos

- **Cuenta de ChatGPT compatible con Codex** (Plus, Pro, Team/Business, Edu o Enterprise).  
- **Codex CLI** instalado localmente en la máquina de orquestación.
- **Acceso a GitHub** para el/los repos a ingerir (elige 1 de 3 para clonado/lectura):  
  - **GitHub CLI (gh) + login** (flow web) para actuar “como tú” al clonar.  
  - **SSH keys** cargadas en GitHub si prefieres `git@...`.  
  - **Personal Access Token (PAT)** (ideal: fine‑grained) si eliges HTTPS + token o invocar API REST/GraphQL.
- **Repositorio(s) de prueba** (público y, si aplica, privado) con ASP.NET MVC + EF, controladores y servicios.
- **Entorno** con .NET SDK, Node (para build UI si aplica) y base de datos (SQL Server/SQLite) para persistencia.

> Nota: Si la organización usa SSO (SAML), autoriza el PAT para esa org antes de usarlo.


## 1) Decisiones de alcance y datos

- **Modo de ingesta principal**:
  - [ ] **Git Clone** (rápido para MVP, evita límites de API al recorrer archivos). (no API key en esta iteración, requiere que el usuario haga login de su cuenta de github, pero sí en futuras)
- **Repos admitidos**:
  - [ ] Solo público en MVP o incluye privado (requiere permisos claros + secretos).
- **Tamaño y límites**:
  - [ ] Exclusiones de binarios, `obj/`, `bin/`, `node_modules/`, etc.
  - [ ] Tamaño máx. de archivo y de repo para el MVP.
- **Niveles del flowchart**:
  - [ ] L1: Entry points (endpoints/controllers, minimal APIs, etc.).
  - [ ] L2: Servicios/repositorios/DbContext.
  - [ ] L3: Control‑flow intra‑método (bucles, condicionales, llamadas externas).


## 2) Autenticación y seguridad

- **Codex CLI**:
  - [ ] Autenticación vía **ChatGPT login** (no API key en esta iteración).
  - [ ] Verificar política de datos y uso en tu plan.
- **GitHub**:
  - [ ] Elegir mecanismo de identidad para clonado/acceso (gh login, SSH o PAT).  
  - [ ] Si PAT, definir caducidad, permisos mínimos, y almacenamiento seguro de secretos.
  - [ ] Revisar política de la organización (SSO, tokens, expiración).
- **App web**:
  - [ ] Variables de entorno para secretos (no en código).
  - [ ] Lista blanca de repos que se pueden ingerir.
  - [ ] Registro de auditoría de acciones (quién ingiere/genera).


## 3) Pipeline de ingesta (Repo → IR)

- **Clonado/descarga**:
  - [ ] Comando para clonar o descargar snapshot del repo en una carpeta temporal por commit.
  - [ ] Gestión de cache por `commit SHA` (evitar re-procesar si ya existe).
- **Parser C# (Roslyn)**:
  - [ ] Construir AST y símbolos por archivo.
  - [ ] Detectar **entry points**: controladores (atributos HTTP/Route), minimal APIs, `Main`, jobs.
  - [ ] Extraer llamadas externas (HttpClient/SDK), condicionales, bucles, try/catch.
  - [ ] Reconocer capas MVC/EF (Controllers → Services → Repositories → DbContext).
- **Convenciones de etiquetas en comentarios**:
  - [ ] Definir gramática (`// f1`, `// f1.1`, etc.) con unicidad por archivo y niveles válidos.
  - [ ] Linter de etiquetas (validación: bien formadas, no solapadas).
- **IR (intermediate representation)**:
  - [ ] Esquema de `Node` (id, tipo, label, archivo, rango de líneas, nivel).
  - [ ] Esquema de `Edge` (from, to, condición).
  - [ ] Reglas de consolidación por nivel (L1/L2/L3) y manejo de ciclos/recursión.


## 4) Orquestación del agente (Codex CLI)

- **Contrato de entrada/salida**:
  - [ ] Entrada: IR parcial + contexto de repo + convenciones de etiquetas + objetivos del diagrama.
  - [ ] Salida: **flowchart JSON** (nodos/aristas con referencias a código) + **pseudocódigo** por nodo.
- **Ejecución**:
  - [ ] Lanzar tarea de generación (CLI) con autenticación ChatGPT.
  - [ ] Tiempos máximos, reintentos, y control de concurrencia.
  - [ ] Idempotencia: hash(commit + parámetros) para evitar regeneraciones.
- **Validación**:
  - [ ] Validar esquema de salida, conteo razonable de nodos/edges.
  - [ ] Verificar que todas las referencias de líneas existen en el repo clonado.


## 5) Backend (ASP.NET MVC + EF)

- **Modelo de datos**:
  - [ ] Entidades: Repos, Commits, Graphs, Nodes, Edges, Artifacts, Jobs.
- **Servicios**:
  - [ ] GitService (clonado/descarga, cache).
  - [ ] ParseService (Roslyn).
  - [ ] CodexJobService (orquestación CLI).
  - [ ] GraphService (normalización, niveles, ciclos).
- **API**:
  - [ ] `POST /repos/{slug}/ingest` – iniciar ingesta + job Codex.
  - [ ] `GET /repos/{slug}/graphs/{commit}?level=L1|L2|L3` – obtener flowchart JSON.
  - [ ] `GET /repos/{slug}/nodes/{id}/code` – devolver snippet y enlace al rango en GitHub.
  - [ ] `POST /webhooks/github` – recibir `push`/`pull_request` y re‑generar.
- **Persistencia**:
  - [ ] Migraciones EF, índices por `repoId`, `commit`, `level`.
  - [ ] Limpieza de artefactos antiguos.


## 6) Frontend (Tailwind)

- **Vista principal**:
  - [ ] Render del gráfico (Mermaid o Cytoscape/ELK) con zoom/pan y mini‑map.
  - [ ] Selector de **nivel** (L1/L2/L3) y de **commit**.
  - [ ] Búsqueda de nodos y leyenda por tipo (endpoint, service, repo, DB, API call, loop, cond).
- **Node drawer**:
  - [ ] Firma del método, pseudocódigo, archivo y rango de líneas.
  - [ ] Botón “Ver en GitHub” (permalink con líneas).
- **Estados y errores**:
  - [ ] “Generando…”, “Listo”, “Error” con mensajes accionables.
  - [ ] Exportar a SVG/PNG y copiar permalink del gráfico.


## 7) Sincronización (event‑driven)

- [ ] Registrar **webhook** de GitHub para `push` (y `pull_request` si aplica).
- [ ] Al recibir evento → validar firma → encolar job → clonar/parsear → regenerar flowchart.
- [ ] Fallback manual: botón “Regenerar” en la UI.


## 8) Observabilidad y DX

- [ ] Logging estructurado (correlación por `jobId` y `commit`).
- [ ] Métricas clave: duración ingesta, tiempo de generación Codex, tamaño del gráfico, errores por tipo.
- [ ] Página interna de salud/cola y últimos jobs (estado y tiempos).


## 9) Pruebas del MVP

- **Repos de prueba**:
  - [ ] Pequeño (controladores simples), mediano (servicios/repos), con ciclos y llamadas HTTP.
- **Tests**:
  - [ ] Parser: detección de endpoints, bucles, condicionales, llamadas externas.
  - [ ] IR → Flowchart: conteo de nodos/aristas, etiquetado de ciclos.
  - [ ] E2E: ingesta → generación → render UI → navegación al código.


## 10) Despliegue

- [ ] Variables de entorno (secrets) para GitHub y Codex.
- [ ] Servicio de **worker** para jobs (separado del servidor web).
- [ ] Política de retención de artefactos/clones.
- [ ] Documentación “Cómo usar” y “Troubleshooting”.


---

## FAQ rápida para tu caso

- **¿Puedo iniciar sesión con mi cuenta de GitHub y clonar “como yo”?**  
  Sí. En el MVP, lo más directo es **gh auth login** (flujo web) o **SSH keys**; alternativamente, usa **PAT**. Una vez autenticado, cualquier `git clone` o `gh repo clone` actuará con **tus** permisos.  
  Consejos: habilita expiración del token, mínimos permisos necesarios y respeta políticas de SSO si tu org las exige.

- **¿Necesito API de GitHub si ya clono?**  
  No estrictamente para leer archivos, pero **recomendable** para: webhooks, metadatos (árbol, commits, enlaces permalinks por línea) y controles de rate‑limit. Para MVP, puedes clonar y añadir webhooks a la vez.

- **¿Codex CLI con login ChatGPT o API key?**  
  En esta iteración usa **login ChatGPT**. Si más adelante quieres integrar pipelines sin sesión interactiva, contempla **API key** y colas CI/CD.

---

## Criterios de “Listo” (MVP)

- [ ] Doy un repo y se genera un flowchart L1/L2/L3 con enlaces a líneas de código.  
- [ ] Puedo buscar nodos y ver pseudocódigo y firmas.  
- [ ] Un push al repo dispara regeneración (webhook) o puedo forzarla manualmente.  
- [ ] Logs y métricas básicas disponibles.  
- [ ] Documentación breve para operar el sistema.
