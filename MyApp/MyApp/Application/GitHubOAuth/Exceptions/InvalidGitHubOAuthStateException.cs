using System;

namespace MyApp.Application.GitHubOAuth.Exceptions
{
    public sealed class InvalidGitHubOAuthStateException : Exception
    {
        public InvalidGitHubOAuthStateException(string message)
            : base(message)
        {
        }
    }
}
