using System;

namespace MyApp.Domain.Secrets
{
    public sealed class RepositorySecretRule
    {
        public RepositorySecretRule(string name, string description, bool isMandatory)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The repository secret name cannot be null or whitespace.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("The repository secret description cannot be null or whitespace.", nameof(description));
            }

            Name = name;
            Description = description;
            IsMandatory = isMandatory;
        }

        public string Name { get; }

        public string Description { get; }

        public bool IsMandatory { get; }
    }
}
