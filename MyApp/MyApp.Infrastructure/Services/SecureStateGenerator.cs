using System;
using System.Security.Cryptography;
using System.Text;
using MyApp.Application.Common.Interfaces;

namespace MyApp.Infrastructure.Services
{
    public sealed class SecureStateGenerator : IStateGenerator
    {
        public string CreateState(Guid userId)
        {
            byte[] randomBytes = RandomNumberGenerator.GetBytes(24);
            string randomSegment = Convert.ToBase64String(randomBytes);
            string sanitizedSegment = randomSegment.Replace("+", string.Empty).Replace("/", string.Empty).Replace("=", string.Empty);

            StringBuilder builder = new StringBuilder();
            builder.Append(userId.ToString("N"));
            builder.Append('-');
            builder.Append(sanitizedSegment);

            return builder.ToString();
        }
    }
}
