namespace MyApp.Infrastructure.Security
{
    public sealed class GitCredentialStoreOptions
    {
        public string SecretNamePrefix { get; set; } = "git/github/";
    }
}
