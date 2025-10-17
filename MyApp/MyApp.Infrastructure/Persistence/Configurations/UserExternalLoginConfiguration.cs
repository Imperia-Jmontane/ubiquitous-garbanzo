using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Domain.Entities;

namespace MyApp.Infrastructure.Persistence.Configurations
{
    public sealed class UserExternalLoginConfiguration : IEntityTypeConfiguration<UserExternalLogin>
    {
        public void Configure(EntityTypeBuilder<UserExternalLogin> builder)
        {
            builder.ToTable("UserExternalLogins");

            builder.HasKey(login => login.Id);

            builder.Property(login => login.Provider)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(login => login.ProviderAccountId)
                .HasMaxLength(256);

            builder.Property(login => login.State)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(login => login.SecretName)
                .HasMaxLength(512);

            builder.Property(login => login.CreatedAt)
                .IsRequired();

            builder.HasIndex(login => login.State)
                .IsUnique();

            builder.HasIndex(login => new { login.UserId, login.Provider })
                .IsUnique();
        }
    }
}
