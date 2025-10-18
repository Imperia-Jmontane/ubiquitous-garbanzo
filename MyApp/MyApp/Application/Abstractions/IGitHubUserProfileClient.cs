using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyApp.Application.Abstractions
{
    public interface IGitHubUserProfileClient
    {
        Task<GitHubUserProfileInfo> GetProfileAsync(string accessToken, CancellationToken cancellationToken);
    }

    public sealed class GitHubUserProfileInfo
    {
        public GitHubUserProfileInfo(string login, string? name, string? email, string? avatarUrl, string? profileUrl, IReadOnlyList<string> organizations)
        {
            if (string.IsNullOrWhiteSpace(login))
            {
                throw new ArgumentException("The login cannot be null or whitespace.", nameof(login));
            }

            Login = login;
            Name = name;
            Email = email;
            AvatarUrl = avatarUrl;
            ProfileUrl = profileUrl;
            Organizations = organizations ?? throw new ArgumentNullException(nameof(organizations));
        }

        public string Login { get; }

        public string? Name { get; }

        public string? Email { get; }

        public string? AvatarUrl { get; }

        public string? ProfileUrl { get; }

        public IReadOnlyList<string> Organizations { get; }
    }
}
