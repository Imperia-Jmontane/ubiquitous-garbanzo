using System;
using System.Collections.Generic;
using System.Linq;

namespace MyApp.Models.Home
{
    public sealed class HomeIndexViewModel
    {
        public HomeIndexViewModel(IReadOnlyCollection<RepositoryListItemViewModel> repositories, AddRepositoryRequest addRepository, string? notification, IReadOnlyCollection<CloneProgressViewModel> cloneProgressItems)
        {
            if (repositories == null)
            {
                throw new ArgumentNullException(nameof(repositories));
            }

            if (addRepository == null)
            {
                throw new ArgumentNullException(nameof(addRepository));
            }

            if (cloneProgressItems == null)
            {
                throw new ArgumentNullException(nameof(cloneProgressItems));
            }

            Repositories = repositories.ToList();
            AddRepository = addRepository;
            Notification = notification;
            CloneProgressItems = cloneProgressItems.ToList();
        }

        public IReadOnlyCollection<RepositoryListItemViewModel> Repositories { get; }

        public AddRepositoryRequest AddRepository { get; }

        public string? Notification { get; }

        public IReadOnlyCollection<CloneProgressViewModel> CloneProgressItems { get; }

        public bool IsCloneInProgress
        {
            get
            {
                return CloneProgressItems.Any(item => item.IsActive);
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
