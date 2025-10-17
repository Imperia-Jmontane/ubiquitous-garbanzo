using System.Collections.Generic;

namespace MyApp.Domain.Secrets
{
    public static class RepositorySecretRequirements
    {
        private static readonly IReadOnlyCollection<RepositorySecretRule> rules = new List<RepositorySecretRule>
        {
            new RepositorySecretRule("GITHUB__CLIENT_ID", "GitHub OAuth application client identifier.", true),
            new RepositorySecretRule("GITHUB__CLIENT_SECRET", "GitHub OAuth application client secret.", true),
            new RepositorySecretRule("GITHUB__WEBHOOK_SECRET", "Webhook secret used to validate GitHub callbacks.", true),
            new RepositorySecretRule("CODEX__CHATGPT_SESSION_TOKEN", "Authentication token for Codex CLI session initiated with ChatGPT login.", true),
            new RepositorySecretRule("GITHUB__PERSONAL_ACCESS_TOKEN", "Fine-grained personal access token for repository ingestion when HTTPS cloning is required.", false)
        };

        public static IReadOnlyCollection<RepositorySecretRule> Rules
        {
            get { return rules; }
        }
    }
}
