using Bookennis.Domain.Bookings;
using Bookennis.Domain.Members;
using Bookennis.Domain.PlayRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class PlayRequestEntityConfiguration : IEntityTypeConfiguration<PlayRequest>
{
    public void Configure(EntityTypeBuilder<PlayRequest> builder)
    {
        builder.ConfigureTenantRelation();

        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.RequesterMemberId)
               .OnDelete(DeleteBehavior.Cascade);

        // A request that was filled keeps its link only while the booking exists
        builder.HasOne<Booking>()
               .WithMany()
               .HasForeignKey(x => x.BookingId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Invitees)
               .WithOne(x => x.Request)
               .HasForeignKey(x => x.PlayRequestId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Invitees).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.Comment).HasMaxLength(PlayRequest.MaxCommentLength);
        builder.Property(x => x.TimeZoneInfoId).HasMaxLength(100);

        builder.Ignore(x => x.AcceptedCount);
        builder.Ignore(x => x.OpenSpots);

        builder.HasIndex(x => new { x.ClubId, x.Status, x.StartsAt });
        builder.HasIndex(x => x.RequesterMemberId);
    }
}

public class PlayRequestInviteeEntityConfiguration : IEntityTypeConfiguration<PlayRequestInvitee>
{
    public void Configure(EntityTypeBuilder<PlayRequestInvitee> builder)
    {
        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.MemberId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.PlayRequestId, x.MemberId }).IsUnique();
        // "The requests I was asked for"
        builder.HasIndex(x => x.MemberId);
    }
}

public class PlayerGroupEntityConfiguration : IEntityTypeConfiguration<PlayerGroup>
{
    public void Configure(EntityTypeBuilder<PlayerGroup> builder)
    {
        builder.ConfigureTenantRelation();

        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.OwnerMemberId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Members)
               .WithOne(x => x.Group)
               .HasForeignKey(x => x.PlayerGroupId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Members).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.Name).HasMaxLength(PlayerGroup.MaxNameLength);

        builder.HasIndex(x => x.OwnerMemberId);
    }
}

public class PlayerGroupMemberEntityConfiguration : IEntityTypeConfiguration<PlayerGroupMember>
{
    public void Configure(EntityTypeBuilder<PlayerGroupMember> builder)
    {
        builder.HasOne<Member>()
               .WithMany()
               .HasForeignKey(x => x.MemberId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.PlayerGroupId, x.MemberId }).IsUnique();
    }
}
