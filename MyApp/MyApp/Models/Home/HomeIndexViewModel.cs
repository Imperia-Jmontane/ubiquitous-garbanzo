using System;
using System.Collections.Generic;
using System.Linq;

namespace MyApp.Models.Home
{
    public sealed class HomeIndexViewModel
    {
        public HomeIndexViewModel(IReadOnlyCollection<RepositoryListItemViewModel> repositories, AddRepositoryRequest addRepository, string? notification, CloneProgressViewModel cloneProgress)
        {
            if (repositories == null)
            {
                throw new ArgumentNullException(nameof(repositories));
            }

            if (addRepository == null)
            {
                throw new ArgumentNullException(nameof(addRepository));
            }

            if (cloneProgress == null)
            {
                throw new ArgumentNullException(nameof(cloneProgress));
            }

            Repositories = repositories.ToList();
            AddRepository = addRepository;
            Notification = notification;
            CloneProgress = cloneProgress;
        }

        public IReadOnlyCollection<RepositoryListItemViewModel> Repositories { get; }

        public AddRepositoryRequest AddRepository { get; }

        public string? Notification { get; }

        public CloneProgressViewModel CloneProgress { get; }

        public bool IsCloneInProgress
        {
            get
            {
                return CloneProgress.IsActive;
            }
        }

        public bool HasRepositories
        {
            get
            {
                return Repositories.Count > 0;
            }
        }
    }
}
