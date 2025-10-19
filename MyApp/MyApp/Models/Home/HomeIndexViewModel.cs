using System;
using System.Collections.Generic;
using System.Linq;

namespace MyApp.Models.Home
{
    public sealed class HomeIndexViewModel
    {
        public HomeIndexViewModel(IReadOnlyCollection<RepositoryListItemViewModel> repositories, AddRepositoryRequest addRepository, string? notification, bool isCloneInProgress)
        {
            if (repositories == null)
            {
                throw new ArgumentNullException(nameof(repositories));
            }

            if (addRepository == null)
            {
                throw new ArgumentNullException(nameof(addRepository));
            }

            Repositories = repositories.ToList();
            AddRepository = addRepository;
            Notification = notification;
            IsCloneInProgress = isCloneInProgress;
        }

        public IReadOnlyCollection<RepositoryListItemViewModel> Repositories { get; }

        public AddRepositoryRequest AddRepository { get; }

        public string? Notification { get; }

        public bool IsCloneInProgress { get; }

        public bool HasRepositories
        {
            get
            {
                return Repositories.Count > 0;
            }
        }
    }
}
