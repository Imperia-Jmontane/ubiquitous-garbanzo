# Project Guidelines

## Architecture
- Use Clean Architecture.
- Implement the following layers: Domain, Application, Infrastructure, and API.
- Use Entity Framework Core for data access.
- Store configuration in `appsettings.json`.
- Build RESTful controllers with validation and DTOs.
- Configure dependency injection in `Program.cs`.
- Enable structured logging with Serilog.
- Write unit tests with xUnit and FluentAssertions.
- Provide Swagger/OpenAPI documentation.

## Code Style
- Clear type definitions only; the `var` keyword is not allowed.
- Use good, descriptive variable naming.
- Apply camelCase for local variables, PascalCase for methods and class variables, and UPPER_SNAKE_CASE for Ta properties.
- Endpoints cannot return Ta elements.
- LINQ and Entity Framework transformations are allowed when used simply and reasonably.
- Use four spaces instead of tabs.
- Use CRLF for new lines.
- Place braces (`{}`) on new lines.
- Insert a blank line between methods.
- Use common sense for blank lines inside methods; keep related code together.
- Include a space before and after any operator (except for `++`).
- Apply the `++` operator only as a postfix (after the variable).
- Prefer doubles over floats.
- Prefer lists over arrays.
- Avoid `out` and optional parameters.

## Pending Feature: Code Analysis Integration

### Objective
Integrate Sourcetrail-like code analysis functionality using Roslyn for C# projects, with interactive graph visualization. This allows users to:
- Index C# repositories to extract symbols (classes, methods, properties, etc.)
- Visualize code relationships as an interactive graph
- Navigate from graph nodes to source code
- Search and filter symbols
- Track inheritance, method calls, and type usage

### Key Resources
- **`Resources/README.md`** - Start here. Contains implementation order, key concepts, and all reference files.
- **`Resources/`** folder - Complete reference implementations for all layers:
  - `SourcetrailReference/DatabaseSchema.sql` - Database schema with all tables and indices
  - `EFCoreExamples/CodeAnalysisDbContext.cs` - EF Core configuration
  - `RoslynExamples/` - Roslyn syntax walkers for symbol collection
  - `BackgroundJobExamples/IndexingBackgroundService.cs` - Background job processing
  - `ApiExamples/CodeAnalysisApiController.cs` - API endpoints with security
  - `ViewExamples/CodeAnalysisIndex.cshtml` - Complete UI with Cytoscape.js
- **`checklist.md`** - Detailed 6-phase implementation checklist
- **`code_analysis_integration_plan.md`** - High-level implementation plan

### Implementation Notes
1. **Create separate project**: `MyApp.CodeAnalysis` with internal Domain/Application/Infrastructure folders
2. **Separate database**: Use `code_analysis.db` (SQLite), not the main application database
3. **MSBuildLocator**: Must be initialized FIRST in Program.cs before any other code
4. **Two-pass indexing**: Declarations first, then references (ensures all symbols exist before recording edges)
5. **Background processing**: Indexing must run in background, not block HTTP requests
6. **Security**: Use repositoryId (not file paths) in API requests to prevent path traversal

### Technical Stack
- **Backend**: Roslyn (Microsoft.CodeAnalysis), EF Core, BackgroundService
- **Frontend**: Cytoscape.js with dagre layout, Prism.js for syntax highlighting
- **Database**: SQLite with optimized indices for graph queries

