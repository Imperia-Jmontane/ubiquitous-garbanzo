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
