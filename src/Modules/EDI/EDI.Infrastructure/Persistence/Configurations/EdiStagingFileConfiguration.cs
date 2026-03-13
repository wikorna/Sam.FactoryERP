using EDI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDI.Infrastructure.Persistence.Configurations;

public class EdiStagingFileConfiguration : IEntityTypeConfiguration<EdiStagingFile>
{
    public void Configure(EntityTypeBuilder<EdiStagingFile> builder)
    {
        // Table created in the "edi" schema as per EdiDbContext configuration
        builder.ToTable("EdiStagingFiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientId).HasMaxLength(100);
        builder.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.SchemaKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SchemaVersion).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StorageProvider).HasMaxLength(50).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.UploadedByUserId).HasMaxLength(100);

        builder.Property(x => x.DetectResultJson).HasColumnType("jsonb");
        builder.Property(x => x.ValidationResultJson).HasColumnType("jsonb");

        builder.Property(x => x.ErrorCode).HasMaxLength(100);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);

        //builder.Property(x => x.RowVersion).IsRowVersion();
        // PostgreSQL concurrency token
        builder.Property(x => x.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasMany(x => x.Errors)
               .WithOne(e => e.StagingFile)
               .HasForeignKey(e => e.StagingFileId)
               .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes for dashboard/polling queries ─────────────────────────────
        // Polling by status (most common dashboard query)
        builder.HasIndex(x => x.Status)
               .HasDatabaseName("IX_EdiStagingFiles_Status");

        // Filter by file type
        builder.HasIndex(x => x.FileType)
               .HasDatabaseName("IX_EdiStagingFiles_FileType");

        // Filter by client/tenant
        builder.HasIndex(x => x.ClientId)
               .HasDatabaseName("IX_EdiStagingFiles_ClientId");

        // Order by recency (compound: status + creation time for paging)
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc })
               .HasDatabaseName("IX_EdiStagingFiles_Status_CreatedAt");
    }
}
