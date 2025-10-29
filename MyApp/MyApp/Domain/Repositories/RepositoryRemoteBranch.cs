using System;

namespace MyApp.Domain.Repositories
{
    public sealed class RepositoryRemoteBranch
    {
        public RepositoryRemoteBranch(string remoteName, string name, bool existsLocally)
        {
            if (string.IsNullOrWhiteSpace(remoteName))
            {
                throw new ArgumentException("The remote name must be provided.", nameof(remoteName));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The branch name must be provided.", nameof(name));
            }

            RemoteName = remoteName;
            Name = name;
            ExistsLocally = existsLocally;
        }

        public string RemoteName { get; }

        public string Name { get; }

        public bool ExistsLocally { get; }

        public string FullName
        {
            get
            {
                return string.Format("{0}/{1}", RemoteName, Name);
            }
        }
    }
}

