using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SSSP.DAL.Enums;
using SSSP.DAL.Models;
using System;

namespace SSSP.DAL.Context
{
    public class AppDbContext
        : IdentityDbContext<User, Role, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        // ==========================
        // DbSets
        // ==========================

        public DbSet<Operator> Operators => Set<Operator>();
        public DbSet<Camera> Cameras => Set<Camera>();
        public DbSet<Sensor> Sensors => Set<Sensor>();
        public DbSet<Incident> Incidents => Set<Incident>();
        //public DbSet<Role> Roles => Set<Role>();

        // Face Recognition
        public DbSet<FaceProfile> FaceProfiles => Set<FaceProfile>();


        // ==========================
        // Configuration
        // ==========================

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            optionsBuilder.ConfigureWarnings(w =>
                w.Ignore(RelationalEventId.PendingModelChangesWarning));
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            ConfigureEnums(builder);
            ConfigureOwnedTypes(builder);
            ConfigureRelationships(builder);
            ConfigureFaceProfiles(builder);
            ConfigureEmbedding(builder);
            //SeedInitialData(builder);
        }

        // ==========================
        // ENUM MAPPING
        // ==========================

        private static void ConfigureEnums(ModelBuilder builder)
        {
            builder.Entity<Incident>().Property(i => i.Type).HasConversion<string>();
            builder.Entity<Incident>().Property(i => i.Status).HasConversion<string>();
            builder.Entity<Incident>().Property(i => i.Severity).HasConversion<string>();
            builder.Entity<Incident>().Property(i => i.Source).HasConversion<string>();
        }

        // ==========================
        // OWNED VALUE OBJECTS
        // ==========================

        private static void ConfigureOwnedTypes(ModelBuilder builder)
        {
            builder.Entity<Incident>().OwnsOne(i => i.Location);
            builder.Entity<Sensor>().OwnsOne(s => s.Location);
            builder.Entity<Camera>().OwnsOne(c => c.Location);
        }

        // ==========================
        // RELATIONSHIPS
        // ==========================

        private static void ConfigureRelationships(ModelBuilder builder)
        {
            // Operator → Users
            builder.Entity<Operator>()
                .HasMany(o => o.Users)
                .WithOne(u => u.Operator)
                .HasForeignKey(u => u.OperatorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Operator → Cameras
            builder.Entity<Operator>()
                .HasMany(o => o.Cameras)
                .WithOne(c => c.Operator)
                .HasForeignKey(c => c.OperatorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Operator → Sensors
            builder.Entity<Operator>()
                .HasMany(o => o.Sensors)
                .WithOne(s => s.Operator)
                .HasForeignKey(s => s.OperatorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Operator → Incidents
            builder.Entity<Operator>()
                .HasMany(o => o.Incidents)
                .WithOne(i => i.Operator)
                .HasForeignKey(i => i.OperatorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Incident → Assigned User
            builder.Entity<Incident>()
                .HasOne(i => i.AssignedToUser)
                .WithMany()
                .HasForeignKey(i => i.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }

        // ==========================
        // FACE PROFILES (AI LINK)
        // ==========================

        private static void ConfigureFaceProfiles(ModelBuilder builder)
        {
            builder.Entity<FaceProfile>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasOne(x => x.User)
                      .WithMany(u => u.FaceProfiles)
                      .HasForeignKey(x => x.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(p => p.Embeddings)
                      .WithOne(e => e.FaceProfile)
                      .HasForeignKey(e => e.FaceProfileId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(x => x.IsPrimary)
                      .HasDefaultValue(true);

                entity.Property(x => x.CreatedAt)
                      .HasDefaultValueSql("GETUTCDATE()");
            });
        }
        private static void ConfigureEmbedding(ModelBuilder builder)
        {
            builder.Entity<FaceEmbedding>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Vector)
                      .IsRequired();
            });
        }

        // ==========================
        // SEED DATA
        // ==========================

        private static void SeedInitialData(ModelBuilder builder)
        {
            var seedTime = new DateTime(2025, 11, 19, 12, 0, 0);

            // Operators
            builder.Entity<Operator>().HasData(
                new Operator
                {
                    Id = 1,
                    Name = "City Security",
                    Type = OperatorType.City,
                    Location = "Downtown",
                    CreatedAt = seedTime,
                    IsActive = true
                },
                new Operator
                {
                    Id = 2,
                    Name = "Hospital Central",
                    Type = OperatorType.Hospital,
                    Location = "Medical District",
                    CreatedAt = seedTime,
                    IsActive = true
                }
            );

            // Cameras
            builder.Entity<Camera>().HasData(
                new { Id = 1, Name = "Camera 1", OperatorId = 1, RtspUrl = "rtsp://camera1", IsActive = true, CreatedAt = seedTime },
                new { Id = 2, Name = "Camera 2", OperatorId = 2, RtspUrl = "rtsp://camera2", IsActive = true, CreatedAt = seedTime }
            );

            builder.Entity<Camera>().OwnsOne(c => c.Location).HasData(
                new { CameraId = 1, Latitude = 30.0, Longitude = 31.0, Address = "Main Street" },
                new { CameraId = 2, Latitude = 30.1, Longitude = 31.1, Address = "Hospital Gate" }
            );

            // Sensors
            builder.Entity<Sensor>().HasData(
                new { Id = 1, Name = "Sensor 1", OperatorId = 1, Type = SensorType.Noise, IsActive = true, CreatedAt = seedTime },
                new { Id = 2, Name = "Sensor 2", OperatorId = 2, Type = SensorType.Temperature, IsActive = true, CreatedAt = seedTime }
            );

            builder.Entity<Sensor>().OwnsOne(s => s.Location).HasData(
                new { SensorId = 1, Latitude = 30.0, Longitude = 31.0, Address = "City Park" },
                new { SensorId = 2, Latitude = 30.2, Longitude = 31.2, Address = "Hospital Roof" }
            );

            // Incidents
            builder.Entity<Incident>().HasData(
                new
                {
                    Id = 1,
                    Title = "Test Incident",
                    Description = "Example incident",
                    Type = IncidentType.Waste,
                    Severity = IncidentSeverity.High,
                    Status = IncidentStatus.Open,
                    Source = IncidentSource.Sensor,
                    OperatorId = 1,
                    AssignedToUserId = (Guid?)null,
                    Timestamp = seedTime.AddMinutes(30)
                }
            );

            builder.Entity<Incident>().OwnsOne(i => i.Location).HasData(
                new { IncidentId = 1, Latitude = 30.0, Longitude = 31.0, Address = "Downtown" }
            );
        }
    }
}
