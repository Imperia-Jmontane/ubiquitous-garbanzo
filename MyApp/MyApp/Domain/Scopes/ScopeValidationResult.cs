using System;
using System.Collections.Generic;

namespace MyApp.Domain.Scopes
{
    public sealed class ScopeValidationResult
    {
        public ScopeValidationResult(bool isValid, IReadOnlyCollection<string> missingScopes)
        {
            if (missingScopes == null)
            {
                throw new ArgumentNullException(nameof(missingScopes));
            }

            IsValid = isValid;
            MissingScopes = missingScopes;
        }

        public bool IsValid { get; }

        public IReadOnlyCollection<string> MissingScopes { get; }
    }
}
