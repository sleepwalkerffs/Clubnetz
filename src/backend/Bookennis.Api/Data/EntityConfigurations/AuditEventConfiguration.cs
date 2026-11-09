using Bookennis.Api.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityAlwaysColumn();

        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Outcome).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Action).HasMaxLength(200);
        builder.Property(e => e.ErrorCode).HasMaxLength(500);
        builder.Property(e => e.EntityType).HasMaxLength(200);
        builder.Property(e => e.EntityId).HasMaxLength(100);
        builder.Property(e => e.SubjectName).HasMaxLength(300);
        builder.Property(e => e.ActorName).HasMaxLength(300);
        builder.Property(e => e.ActorEmail).HasMaxLength(256);
        builder.Property(e => e.ImpersonatorName).HasMaxLength(300);
        builder.Property(e => e.System).HasMaxLength(200);
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Property(e => e.UserAgent).HasMaxLength(256);
        builder.Property(e => e.Request).HasMaxLength(512);
        builder.Property(e => e.Summary).HasMaxLength(1000);
        builder.Property(e => e.Changes).HasColumnType("jsonb");

        // The filters of the explorer: newest first, by club, by actor, by the user it is about, the history of a row, one request
        builder.HasIndex(e => new { e.OccurredAt, e.Id }).IsDescending(true, true);
        builder.HasIndex(e => new { e.ClubId, e.OccurredAt });
        builder.HasIndex(e => new { e.ActorUserId, e.OccurredAt });
        builder.HasIndex(e => new { e.SubjectUserId, e.OccurredAt });
        builder.HasIndex(e => new { e.EntityType, e.EntityId, e.OccurredAt });
        builder.HasIndex(e => e.CorrelationId);
    }
}
