using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Domain.Entities;

namespace MyApp.Infrastructure.Persistence.Configurations
{
    public sealed class GitHubAccountLinkConfiguration : IEntityTypeConfiguration<GitHubAccountLink>
    {
        public void Configure(EntityTypeBuilder<GitHubAccountLink> builder)
        {
            builder.ToTable("GitHubAccountLinks");

            builder.HasKey(link => link.UserId);

            builder.Property(link => link.SecretName)
                .IsRequired()
                .HasMaxLength(512);

            builder.OwnsOne(link => link.Identity, identityBuilder =>
            {
                identityBuilder.Property(identity => identity.AccountId)
                    .HasColumnName("GitHubAccountId")
                    .HasMaxLength(50)
                    .IsRequired();

                identityBuilder.Property(identity => identity.Login)
                    .HasColumnName("GitHubLogin")
                    .HasMaxLength(100)
                    .IsRequired();

                identityBuilder.Property(identity => identity.DisplayName)
                    .HasColumnName("GitHubDisplayName")
                    .HasMaxLength(200)
                    .IsRequired();

                identityBuilder.Property(identity => identity.AvatarUrl)
                    .HasColumnName("GitHubAvatarUrl")
                    .HasMaxLength(500);
            });
        }
    }
}
