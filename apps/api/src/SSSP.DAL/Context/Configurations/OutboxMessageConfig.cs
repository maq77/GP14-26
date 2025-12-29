using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSSP.DAL.Models;

namespace SSSP.DAL.Context.Configurations;

public sealed class OutboxMessageConfig : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("OutboxMessages");
        b.HasKey(x => x.Id);

        b.Property(x => x.AggregateType).HasMaxLength(128).IsRequired();
        b.Property(x => x.AggregateId).HasMaxLength(64).IsRequired();
        b.Property(x => x.Topic).HasMaxLength(64).IsRequired();
        b.Property(x => x.Event).HasMaxLength(64).IsRequired();
        b.Property(x => x.Scope).HasMaxLength(32).IsRequired();
        b.Property(x => x.ScopeKey).HasMaxLength(128);
        b.Property(x => x.IdempotencyKey).HasMaxLength(256);

        b.Property(x => x.PayloadType).HasMaxLength(256).IsRequired();
        b.Property(x => x.PayloadJson).IsRequired();

        b.Property(x => x.OccurredAtUtc).HasDefaultValueSql("GETUTCDATE()");
        b.Property(x => x.Status).HasDefaultValue(0);
        b.Property(x => x.Attempts).HasDefaultValue(0);

        // fast dequeue
        b.HasIndex(x => new { x.Status, x.OccurredAtUtc });

        // optional uniqueness for dedupe publishing
        b.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
    }
}
