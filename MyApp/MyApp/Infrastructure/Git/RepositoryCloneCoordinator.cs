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
        private readonly ConcurrentDictionary<Guid, CloneOperationRegistration> _operations;
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
            _operations = new ConcurrentDictionary<Guid, CloneOperationRegistration>();
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
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            CloneOperationRegistration registration = new CloneOperationRegistration(queuedState, cancellationTokenSource);
            _operations[operationId] = registration;
            _operationKeys[normalizedKey] = operationId;

            Task.Run(() => RunCloneAsync(operationId, repositoryUrl, cancellationTokenSource), CancellationToken.None);

            return new RepositoryCloneTicket(operationId, repositoryUrl, false, true);
        }

        public bool TryGetStatus(Guid operationId, out RepositoryCloneStatus? status)
        {
            status = null;
            CloneOperationRegistration? registration;

            if (_operations.TryGetValue(operationId, out registration) && registration != null)
            {
                status = registration.ToStatus();
                return true;
            }

            return false;
        }

        public IReadOnlyCollection<RepositoryCloneStatus> GetActiveClones()
        {
            List<RepositoryCloneStatus> statuses = new List<RepositoryCloneStatus>();

            foreach (KeyValuePair<Guid, CloneOperationRegistration> pair in _operations)
            {
                RepositoryCloneStatus snapshot = pair.Value.ToStatus();

                if (snapshot.State == RepositoryCloneState.Queued || snapshot.State == RepositoryCloneState.Running)
                {
                    statuses.Add(snapshot);
                }
            }

            return statuses;
        }

        public bool CancelClone(Guid operationId)
        {
            CloneOperationRegistration? registration;

            if (!_operations.TryGetValue(operationId, out registration) || registration == null)
            {
                return false;
            }

            CloneOperationState currentState = registration.GetStateSnapshot();

            if (currentState.State == RepositoryCloneState.Completed)
            {
                bool removed = TryHandleCompletedCloneCancellation(operationId, currentState);
                return removed;
            }

            if (currentState.State == RepositoryCloneState.Failed || currentState.State == RepositoryCloneState.Canceled)
            {
                return false;
            }

            UpdateStatus(operationId, currentState.RepositoryUrl, RepositoryCloneState.Running, currentState.Percentage, "Canceling clone", "Stopping repository clone...");

            try
            {
                if (!registration.CancellationTokenSource.IsCancellationRequested)
                {
                    registration.CancellationTokenSource.Cancel();
                }
            }
            catch (ObjectDisposedException)
            {
            }

            return true;
        }

        private bool TryHandleCompletedCloneCancellation(Guid operationId, CloneOperationState state)
        {
            bool repositoryRemoved = TryDeleteRepositoryForUrl(state.RepositoryUrl);

            if (!repositoryRemoved)
            {
                return false;
            }

            UpdateStatus(operationId, state.RepositoryUrl, RepositoryCloneState.Canceled, 100.0, "Canceled", "Repository clone was canceled.");
            return true;
        }

        private bool TryDeleteRepositoryForUrl(string repositoryUrl)
        {
            if (string.IsNullOrWhiteSpace(repositoryUrl))
            {
                return false;
            }

            IReadOnlyCollection<LocalRepository> repositories = _repositoryService.GetRepositories();

            foreach (LocalRepository repository in repositories)
            {
                if (!RepositoryUrlsMatch(repository.RemoteUrl, repositoryUrl))
                {
                    continue;
                }

                DeleteRepositoryResult deleteResult = _repositoryService.DeleteRepository(repository.FullPath);

                if (!deleteResult.Succeeded)
                {
                    _logger.LogWarning("Unable to delete repository at {RepositoryPath} after clone completion: {Message}", repository.FullPath, deleteResult.Message);
                    return false;
                }

                return true;
            }

            _logger.LogInformation("Repository for {RepositoryUrl} not found during cancel request after completion.", repositoryUrl);
            return true;
        }

        private bool RepositoryAlreadyCloned(string repositoryUrl)
        {
            if (_repositoryService.RepositoryExists(repositoryUrl))
            {
                return true;
            }

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
            string trimmed = RemoveCredentialsFromUrl(url);

            if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(0, trimmed.Length - 4);
            }

            return trimmed;
        }

        private static string RemoveCredentialsFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            string trimmed = url.Trim();
            int schemeSeparatorIndex = trimmed.IndexOf("://", StringComparison.Ordinal);

            if (schemeSeparatorIndex < 0)
            {
                return trimmed;
            }

            int credentialsEndIndex = trimmed.IndexOf('@', schemeSeparatorIndex + 3);

            if (credentialsEndIndex < 0)
            {
                return trimmed;
            }

            string prefix = trimmed.Substring(0, schemeSeparatorIndex + 3);
            string remainder = trimmed.Substring(credentialsEndIndex + 1);
            return string.Concat(prefix, remainder);
        }

        private static string NormalizeRepositoryKey(string repositoryUrl)
        {
            return NormalizeRepositoryUrl(repositoryUrl).ToLowerInvariant();
        }

        private async Task RunCloneAsync(Guid operationId, string repositoryUrl, CancellationTokenSource cancellationTokenSource)
        {
            try
            {
                Progress<RepositoryCloneProgress> progress = new Progress<RepositoryCloneProgress>(progressUpdate =>
                {
                    UpdateStatus(operationId, repositoryUrl, RepositoryCloneState.Running, progressUpdate.Percentage, progressUpdate.Stage, progressUpdate.Details);
                });

                CloneRepositoryResult result = await _repositoryService.CloneRepositoryAsync(repositoryUrl, progress, cancellationTokenSource.Token).ConfigureAwait(false);

                if (result.Succeeded)
                {
                    string completionStage = "Completed";
                    string completionMessage = result.AlreadyExists ? "Repository already cloned." : string.Empty;
                    UpdateStatus(operationId, repositoryUrl, RepositoryCloneState.Completed, 100.0, completionStage, completionMessage);
                }
                else if (result.Canceled)
                {
                    string canceledStage = "Canceled";
                    string canceledMessage = string.IsNullOrWhiteSpace(result.Message) ? "Repository clone was canceled." : result.Message;
                    UpdateStatus(operationId, repositoryUrl, RepositoryCloneState.Canceled, 100.0, canceledStage, canceledMessage);
                }
                else
                {
                    string failureStage = "Failed";
                    string failureMessage = string.IsNullOrWhiteSpace(result.Message) ? "Failed to clone repository." : result.Message;
                    UpdateStatus(operationId, repositoryUrl, RepositoryCloneState.Failed, 100.0, failureStage, failureMessage);
                }
            }
            catch (OperationCanceledException exception)
            {
                _logger.LogInformation(exception, "Clone operation canceled for {RepositoryUrl}", repositoryUrl);
                UpdateStatus(operationId, repositoryUrl, RepositoryCloneState.Canceled, 100.0, "Canceled", "Repository clone was canceled.");
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

            CloneOperationRegistration? registration;

            if (_operations.TryGetValue(operationId, out registration) && registration != null)
            {
                registration.Update(state, percentage, safeStage, safeMessage);
                return;
            }

            CloneOperationState createdState = new CloneOperationState(operationId, repositoryUrl, state, percentage, safeStage, safeMessage, DateTimeOffset.UtcNow);
            CloneOperationRegistration createdRegistration = new CloneOperationRegistration(createdState, new CancellationTokenSource());
            _operations[operationId] = createdRegistration;
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

        private sealed class CloneOperationRegistration
        {
            private CloneOperationState _state;
            private readonly object _syncRoot;

            public CloneOperationRegistration(CloneOperationState state, CancellationTokenSource cancellationTokenSource)
            {
                _state = state;
                CancellationTokenSource = cancellationTokenSource;
                _syncRoot = new object();
            }

            public CancellationTokenSource CancellationTokenSource { get; }

            public CloneOperationState GetStateSnapshot()
            {
                lock (_syncRoot)
                {
                    return _state;
                }
            }

            public RepositoryCloneStatus ToStatus()
            {
                lock (_syncRoot)
                {
                    return _state.ToStatus();
                }
            }

            public void Update(RepositoryCloneState state, double percentage, string stage, string message)
            {
                lock (_syncRoot)
                {
                    _state = _state.With(state, percentage, stage, message);
                }
            }
        }
    }
}
