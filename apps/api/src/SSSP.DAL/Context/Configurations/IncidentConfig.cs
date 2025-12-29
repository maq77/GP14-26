using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSSP.DAL.Models;

namespace SSSP.DAL.Context.Configurations;

public sealed class IncidentConfig : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> b)
    {
        b.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        b.HasIndex(x => x.DedupeKey)
            .IsUnique()
            .HasFilter("[Status] <> N'Closed'");
    }
}
