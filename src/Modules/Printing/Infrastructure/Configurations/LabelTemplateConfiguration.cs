using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Printing.Domain;

namespace Printing.Infrastructure.Configurations;

public class LabelTemplateConfiguration : IEntityTypeConfiguration<LabelTemplate>
{
    public void Configure(EntityTypeBuilder<LabelTemplate> builder)
    {
        builder.ToTable("LabelTemplates");

        builder.HasKey(lt => lt.Id);

        builder.Property(lt => lt.Id)
            .ValueGeneratedOnAdd();

        builder.Property(lt => lt.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(lt => lt.Template)
            .IsRequired();
    }
}

