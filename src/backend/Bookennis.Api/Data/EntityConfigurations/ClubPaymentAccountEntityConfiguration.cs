using Bookennis.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class ClubPaymentAccountEntityConfiguration : IEntityTypeConfiguration<ClubPaymentAccount>
{
    public void Configure(EntityTypeBuilder<ClubPaymentAccount> builder)
    {
        builder.ConfigureTenantRelation();

        builder.Property(x => x.StripeAccountId).HasMaxLength(ClubPaymentAccount.MaxStripeAccountIdLength);

        builder.HasIndex(x => x.ClubId).IsUnique();
        builder.HasIndex(x => x.StripeAccountId).IsUnique();
    }
}
