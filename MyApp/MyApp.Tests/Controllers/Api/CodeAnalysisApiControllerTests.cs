#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Application.CodeAnalysis.DTOs;
using MyApp.Controllers.Api;
using MyApp.Domain.CodeAnalysis;
using MyApp.Domain.Repositories;
using MyApp.Models.CodeAnalysis;
using Xunit;

namespace MyApp.Tests.Controllers.Api
{
    public sealed class CodeAnalysisApiControllerTests
    {
        [Fact]
        public async Task IndexRepository_ShouldReturnNotFound_WhenRepositoryMissing()
        {
            Mock<IIndexingJobService> indexingJobServiceMock = new Mock<IIndexingJobService>();
            Mock<ICodeGraphRepository> codeGraphRepositoryMock = new Mock<ICodeGraphRepository>();
            Mock<ILocalRepositoryService> localRepositoryServiceMock = new Mock<ILocalRepositoryService>();
            Mock<ILogger<CodeAnalysisApiController>> loggerMock = new Mock<ILogger<CodeAnalysisApiController>>();

            localRepositoryServiceMock.Setup(service => service.GetRepositories())
                .Returns(new List<LocalRepository>());

            CodeAnalysisApiController controller = new CodeAnalysisApiController(indexingJobServiceMock.Object, codeGraphRepositoryMock.Object, localRepositoryServiceMock.Object, loggerMock.Object);

            CodeAnalysisIndexRequest request = new CodeAnalysisIndexRequest
            {
                RepositoryId = "missing-repo"
            };

            IActionResult result = await controller.IndexRepository(request, CancellationToken.None);

            NotFoundObjectResult notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            ProblemDetails details = notFound.Value.Should().BeOfType<ProblemDetails>().Subject;
            details.Status.Should().Be(StatusCodes.Status404NotFound);
        }

        [Fact]
        public async Task IndexRepository_ShouldReturnServerError_WhenQueueFails()
        {
            string repositoryId = "repo-one";
            Mock<IIndexingJobService> indexingJobServiceMock = new Mock<IIndexingJobService>();
            Mock<ICodeGraphRepository> codeGraphRepositoryMock = new Mock<ICodeGraphRepository>();
            Mock<ILocalRepositoryService> localRepositoryServiceMock = new Mock<ILocalRepositoryService>();
            Mock<ILogger<CodeAnalysisApiController>> loggerMock = new Mock<ILogger<CodeAnalysisApiController>>();

            LocalRepository repository = BuildRepository(repositoryId, Path.GetTempPath());
            localRepositoryServiceMock.Setup(service => service.GetRepositories())
                .Returns(new List<LocalRepository> { repository });
            indexingJobServiceMock.Setup(service => service.QueueIndexingAsync(repositoryId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("MSBuild not available"));

            CodeAnalysisApiController controller = new CodeAnalysisApiController(indexingJobServiceMock.Object, codeGraphRepositoryMock.Object, localRepositoryServiceMock.Object, loggerMock.Object);

            CodeAnalysisIndexRequest request = new CodeAnalysisIndexRequest
            {
                RepositoryId = repositoryId
            };

            IActionResult result = await controller.IndexRepository(request, CancellationToken.None);

            ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            ProblemDetails details = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
            details.Title.Should().Be("Indexing failed");
        }

        [Fact]
        public async Task GetStatus_ShouldReturnBadRequest_WhenRepositoryIdMissing()
        {
            Mock<IIndexingJobService> indexingJobServiceMock = new Mock<IIndexingJobService>();
            Mock<ICodeGraphRepository> codeGraphRepositoryMock = new Mock<ICodeGraphRepository>();
            Mock<ILocalRepositoryService> localRepositoryServiceMock = new Mock<ILocalRepositoryService>();
            Mock<ILogger<CodeAnalysisApiController>> loggerMock = new Mock<ILogger<CodeAnalysisApiController>>();

            CodeAnalysisApiController controller = new CodeAnalysisApiController(indexingJobServiceMock.Object, codeGraphRepositoryMock.Object, localRepositoryServiceMock.Object, loggerMock.Object);

            IActionResult result = await controller.GetStatus(string.Empty, CancellationToken.None);

            BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            ProblemDetails details = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
            details.Status.Should().Be(StatusCodes.Status400BadRequest);
        }

        [Fact]
        public async Task GetGraph_ShouldReturnNotFound_WhenRepositoryMissing()
        {
            Mock<IIndexingJobService> indexingJobServiceMock = new Mock<IIndexingJobService>();
            Mock<ICodeGraphRepository> codeGraphRepositoryMock = new Mock<ICodeGraphRepository>();
            Mock<ILocalRepositoryService> localRepositoryServiceMock = new Mock<ILocalRepositoryService>();
            Mock<ILogger<CodeAnalysisApiController>> loggerMock = new Mock<ILogger<CodeAnalysisApiController>>();

            localRepositoryServiceMock.Setup(service => service.GetRepositories())
                .Returns(new List<LocalRepository>());

            CodeAnalysisApiController controller = new CodeAnalysisApiController(indexingJobServiceMock.Object, codeGraphRepositoryMock.Object, localRepositoryServiceMock.Object, loggerMock.Object);

            IActionResult result = await controller.GetGraph("missing", 2, 100, 500, false, null, null, null, CancellationToken.None);

            NotFoundObjectResult notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            ProblemDetails details = notFound.Value.Should().BeOfType<ProblemDetails>().Subject;
            details.Status.Should().Be(StatusCodes.Status404NotFound);
        }

        [Fact]
        public async Task GetGraph_ShouldReturnBadRequest_WhenSymbolKindsInvalid()
        {
            string repositoryId = "repo-one";
            Mock<IIndexingJobService> indexingJobServiceMock = new Mock<IIndexingJobService>();
            Mock<ICodeGraphRepository> codeGraphRepositoryMock = new Mock<ICodeGraphRepository>();
            Mock<ILocalRepositoryService> localRepositoryServiceMock = new Mock<ILocalRepositoryService>();
            Mock<ILogger<CodeAnalysisApiController>> loggerMock = new Mock<ILogger<CodeAnalysisApiController>>();

            LocalRepository repository = BuildRepository(repositoryId, Path.GetTempPath());
            localRepositoryServiceMock.Setup(service => service.GetRepositories())
                .Returns(new List<LocalRepository> { repository });

            CodeAnalysisApiController controller = new CodeAnalysisApiController(indexingJobServiceMock.Object, codeGraphRepositoryMock.Object, localRepositoryServiceMock.Object, loggerMock.Object);

            IActionResult result = await controller.GetGraph(repositoryId, 2, 100, 500, false, null, null, "NotAKind", CancellationToken.None);

            BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            ProblemDetails details = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
            details.Title.Should().Be("Invalid symbolKinds");
        }

        [Fact]
        public async Task GetSymbolReferences_ShouldReturnNotFound_WhenSymbolMissing()
        {
            Mock<IIndexingJobService> indexingJobServiceMock = new Mock<IIndexingJobService>();
            Mock<ICodeGraphRepository> codeGraphRepositoryMock = new Mock<ICodeGraphRepository>();
            Mock<ILocalRepositoryService> localRepositoryServiceMock = new Mock<ILocalRepositoryService>();
            Mock<ILogger<CodeAnalysisApiController>> loggerMock = new Mock<ILogger<CodeAnalysisApiController>>();

            codeGraphRepositoryMock.Setup(repository => repository.SymbolExistsAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            CodeAnalysisApiController controller = new CodeAnalysisApiController(indexingJobServiceMock.Object, codeGraphRepositoryMock.Object, localRepositoryServiceMock.Object, loggerMock.Object);

            IActionResult result = await controller.GetSymbolReferences(99, CancellationToken.None);

            NotFoundObjectResult notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            ProblemDetails details = notFound.Value.Should().BeOfType<ProblemDetails>().Subject;
            details.Title.Should().Be("Symbol not found");
        }

        [Fact]
        public async Task GetInheritance_ShouldReturnNotFound_WhenSymbolMissing()
        {
            Mock<IIndexingJobService> indexingJobServiceMock = new Mock<IIndexingJobService>();
            Mock<ICodeGraphRepository> codeGraphRepositoryMock = new Mock<ICodeGraphRepository>();
            Mock<ILocalRepositoryService> localRepositoryServiceMock = new Mock<ILocalRepositoryService>();
            Mock<ILogger<CodeAnalysisApiController>> loggerMock = new Mock<ILogger<CodeAnalysisApiController>>();

            codeGraphRepositoryMock.Setup(repository => repository.SymbolExistsAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            CodeAnalysisApiController controller = new CodeAnalysisApiController(indexingJobServiceMock.Object, codeGraphRepositoryMock.Object, localRepositoryServiceMock.Object, loggerMock.Object);

            IActionResult result = await controller.GetInheritance(42, null, null, CancellationToken.None);

            NotFoundObjectResult notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            ProblemDetails details = notFound.Value.Should().BeOfType<ProblemDetails>().Subject;
            details.Title.Should().Be("Symbol not found");
        }

        [Fact]
        public async Task SearchSymbols_ShouldReturnBadRequest_WhenQueryMissing()
        {
            Mock<IIndexingJobService> indexingJobServiceMock = new Mock<IIndexingJobService>();
            Mock<ICodeGraphRepository> codeGraphRepositoryMock = new Mock<ICodeGraphRepository>();
            Mock<ILocalRepositoryService> localRepositoryServiceMock = new Mock<ILocalRepositoryService>();
            Mock<ILogger<CodeAnalysisApiController>> loggerMock = new Mock<ILogger<CodeAnalysisApiController>>();

            CodeAnalysisApiController controller = new CodeAnalysisApiController(indexingJobServiceMock.Object, codeGraphRepositoryMock.Object, localRepositoryServiceMock.Object, loggerMock.Object);

            IActionResult result = await controller.SearchSymbols("repo", string.Empty, 10, CancellationToken.None);

            BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            ProblemDetails details = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
            details.Status.Should().Be(StatusCodes.Status400BadRequest);
        }

        [Fact]
        public async Task GetSource_ShouldReturnBadRequest_WhenPathTraversalAttempted()
        {
            string repositoryId = "repo-one";
            string repositoryRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Mock<IIndexingJobService> indexingJobServiceMock = new Mock<IIndexingJobService>();
            Mock<ICodeGraphRepository> codeGraphRepositoryMock = new Mock<ICodeGraphRepository>();
            Mock<ILocalRepositoryService> localRepositoryServiceMock = new Mock<ILocalRepositoryService>();
            Mock<ILogger<CodeAnalysisApiController>> loggerMock = new Mock<ILogger<CodeAnalysisApiController>>();

            Directory.CreateDirectory(repositoryRoot);

            try
            {
                LocalRepository repository = BuildRepository(repositoryId, repositoryRoot);
                localRepositoryServiceMock.Setup(service => service.GetRepositories())
                    .Returns(new List<LocalRepository> { repository });

                CodeAnalysisApiController controller = new CodeAnalysisApiController(indexingJobServiceMock.Object, codeGraphRepositoryMock.Object, localRepositoryServiceMock.Object, loggerMock.Object);

                string traversalPath = ".." + Path.DirectorySeparatorChar + "secrets.txt";

                IActionResult result = await controller.GetSource(repositoryId, traversalPath, null, null, CancellationToken.None);

                BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
                ProblemDetails details = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
                details.Title.Should().Be("Invalid file path");
            }
            finally
            {
                if (Directory.Exists(repositoryRoot))
                {
                    Directory.Delete(repositoryRoot, true);
                }
            }
        }

        [Fact]
        public async Task GetSource_ShouldReturnContent_WhenFileExists()
        {
            string repositoryId = "repo-one";
            string repositoryRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(repositoryRoot, "Sample.cs");
            Mock<IIndexingJobService> indexingJobServiceMock = new Mock<IIndexingJobService>();
            Mock<ICodeGraphRepository> codeGraphRepositoryMock = new Mock<ICodeGraphRepository>();
            Mock<ILocalRepositoryService> localRepositoryServiceMock = new Mock<ILocalRepositoryService>();
            Mock<ILogger<CodeAnalysisApiController>> loggerMock = new Mock<ILogger<CodeAnalysisApiController>>();

            Directory.CreateDirectory(repositoryRoot);
            File.WriteAllText(filePath, "public class Sample { }", System.Text.Encoding.UTF8);

            try
            {
                LocalRepository repository = BuildRepository(repositoryId, repositoryRoot);
                localRepositoryServiceMock.Setup(service => service.GetRepositories())
                    .Returns(new List<LocalRepository> { repository });

                CodeAnalysisApiController controller = new CodeAnalysisApiController(indexingJobServiceMock.Object, codeGraphRepositoryMock.Object, localRepositoryServiceMock.Object, loggerMock.Object);

                IActionResult result = await controller.GetSource(repositoryId, "Sample.cs", null, null, CancellationToken.None);

                OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
                SourceContentResponse response = okResult.Value.Should().BeOfType<SourceContentResponse>().Subject;
                response.Content.Should().Contain("Sample");
                response.Language.Should().Be("csharp");
            }
            finally
            {
                if (Directory.Exists(repositoryRoot))
                {
                    Directory.Delete(repositoryRoot, true);
                }
            }
        }

        private static LocalRepository BuildRepository(string repositoryId, string repositoryRoot)
        {
            List<RepositoryBranch> branches = new List<RepositoryBranch>();
            return new LocalRepository(repositoryId, repositoryRoot, string.Empty, branches, false, false, null);
        }
    }
}
