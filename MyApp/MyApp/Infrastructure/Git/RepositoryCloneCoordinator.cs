using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions;
using MyApp.Domain.Repositories;

namespace MyApp.Infrastructure.Git
{
    public sealed class RepositoryCloneCoordinator : IRepositoryCloneCoordinator
    {
        private readonly ILocalRepositoryService _repositoryService;
        private readonly ILogger<RepositoryCloneCoordinator> _logger;
        private readonly ConcurrentDictionary<Guid, CloneOperationState> _operations;
        private readonly ConcurrentDictionary<string, Guid> _operationKeys;

        public RepositoryCloneCoordinator(ILocalRepositoryService repositoryService, ILogger<RepositoryCloneCoordinator> logger)
        {
            if (repositoryService == null)
            {
                throw new ArgumentNullException(nameof(repositoryService));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _repositoryService = repositoryService;
            _logger = logger;
            _operations = new ConcurrentDictionary<Guid, CloneOperationState>();
            _operationKeys = new ConcurrentDictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        }

        public RepositoryCloneTicket QueueClone(string repositoryUrl)
        {
            if (string.IsNullOrWhiteSpace(repositoryUrl))
            {
                throw new ArgumentException("The repository URL must be provided.", nameof(repositoryUrl));
            }

            string normalizedKey = NormalizeRepositoryKey(repositoryUrl);

            Guid existingOperationId;

            if (_operationKeys.TryGetValue(normalizedKey, out existingOperationId))
            {
                return new RepositoryCloneTicket(existingOperationId, repositoryUrl, false, false);
            }

            if (RepositoryAlreadyCloned(repositoryUrl))
            {
                return new RepositoryCloneTicket(Guid.Empty, repositoryUrl, true, false);
            }

            Guid operationId = Guid.NewGuid();
            CloneOperationState queuedState = new CloneOperationState(operationId, repositoryUrl, RepositoryCloneState.Queued, 0.0, "Queued", string.Empty, DateTimeOffset.UtcNow);
            _operations[operationId] = queuedState;
            _operationKeys[normalizedKey] = operationId;

            Task.Run(() => RunCloneAsync(operationId, repositoryUrl), CancellationToken.None);

            return new RepositoryCloneTicket(operationId, repositoryUrl, false, true);
        }

        public bool TryGetStatus(Guid operationId, out RepositoryCloneStatus? status)
        {
            status = null;
            CloneOperationState? operationState;

            if (_operations.TryGetValue(operationId, out operationState) && operationState != null)
            {
                status = operationState.ToStatus();
                return true;
            }

            return false;
        }

        public IReadOnlyCollection<RepositoryCloneStatus> GetActiveClones()
        {
            List<RepositoryCloneStatus> statuses = new List<RepositoryCloneStatus>();

            foreach (KeyValuePair<Guid, CloneOperationState> pair in _operations)
            {
                statuses.Add(pair.Value.ToStatus());
            }

            return statuses;
        }

        private bool RepositoryAlreadyCloned(string repositoryUrl)
        {
            IReadOnlyCollection<LocalRepository> repositories = _repositoryService.GetRepositories();

            foreach (LocalRepository repository in repositories)
            {
                if (RepositoryUrlsMatch(repository.RemoteUrl, repositoryUrl))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RepositoryUrlsMatch(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
            {
                return false;
            }

            string normalizedFirst = NormalizeRepositoryUrl(first);
            string normalizedSecond = NormalizeRepositoryUrl(second);
            return string.Equals(normalizedFirst, normalizedSecond, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRepositoryUrl(string url)
        {
            string trimmed = url.Trim();

            if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(0, trimmed.Length - 4);
            }

            return trimmed;
        }

        private static string NormalizeRepositoryKey(string repositoryUrl)
        {
            return NormalizeRepositoryUrl(repositoryUrl).ToLowerInvariant();
        }

        private async Task RunCloneAsync(Guid operationId, string repositoryUrl)
        {
            try
            {
                Progress<RepositoryCloneProgress> progress = new Progress<RepositoryCloneProgress>(progressUpdate =>
                {
                    UpdateStatus(operationId, repositoryUrl, RepositoryCloneState.Running, progressUpdate.Percentage, progressUpdate.Stage, progressUpdate.Details);
                });

                CloneRepositoryResult result = await _repositoryService.CloneRepositoryAsync(repositoryUrl, progress, CancellationToken.None).ConfigureAwait(false);

                if (result.Succeeded)
                {
                    string completionStage = "Completed";
                    string completionMessage = result.AlreadyExists ? "Repository already cloned." : string.Empty;
                    UpdateStatus(operationId, repositoryUrl, RepositoryCloneState.Completed, 100.0, completionStage, completionMessage);
                }
                else
                {
                    string failureStage = "Failed";
                    string failureMessage = string.IsNullOrWhiteSpace(result.Message) ? "Failed to clone repository." : result.Message;
                    UpdateStatus(operationId, repositoryUrl, RepositoryCloneState.Failed, 100.0, failureStage, failureMessage);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Clone operation failed for {RepositoryUrl}", repositoryUrl);
                UpdateStatus(operationId, repositoryUrl, RepositoryCloneState.Failed, 100.0, "Failed", exception.Message ?? "Clone failed.");
            }
            finally
            {
                Guid removed;
                _operationKeys.TryRemove(NormalizeRepositoryKey(repositoryUrl), out removed);
            }
        }

        private void UpdateStatus(Guid operationId, string repositoryUrl, RepositoryCloneState state, double percentage, string stage, string message)
        {
            string safeStage = stage ?? string.Empty;
            string safeMessage = message ?? string.Empty;

            _operations.AddOrUpdate(
                operationId,
                id => new CloneOperationState(operationId, repositoryUrl, state, percentage, safeStage, safeMessage, DateTimeOffset.UtcNow),
                (id, existing) => existing.With(state, percentage, safeStage, safeMessage));
        }

        private sealed class CloneOperationState
        {
            public CloneOperationState(Guid operationId, string repositoryUrl, RepositoryCloneState state, double percentage, string stage, string message, DateTimeOffset lastUpdatedUtc)
            {
                OperationId = operationId;
                RepositoryUrl = repositoryUrl;
                State = state;
                Percentage = percentage;
                Stage = stage;
                Message = message;
                LastUpdatedUtc = lastUpdatedUtc;
            }

            public Guid OperationId { get; }

            public string RepositoryUrl { get; }

            public RepositoryCloneState State { get; }

            public double Percentage { get; }

            public string Stage { get; }

            public string Message { get; }

            public DateTimeOffset LastUpdatedUtc { get; }

            public CloneOperationState With(RepositoryCloneState state, double percentage, string stage, string message)
            {
                string nextStage = string.IsNullOrWhiteSpace(stage) ? Stage : stage;
                string nextMessage = string.IsNullOrWhiteSpace(message) ? Message : message;
                return new CloneOperationState(OperationId, RepositoryUrl, state, percentage, nextStage, nextMessage, DateTimeOffset.UtcNow);
            }

            public RepositoryCloneStatus ToStatus()
            {
                return new RepositoryCloneStatus(OperationId, RepositoryUrl, State, Percentage, Stage, Message, LastUpdatedUtc);
            }
        }
    }
}
