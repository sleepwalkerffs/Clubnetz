using System.Drawing;
using Bookennis.Domain.Clubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bookennis.Api.Data.EntityConfigurations;

public class PlayModeConfiguration : IEntityTypeConfiguration<PlayMode>
{
    public void Configure(EntityTypeBuilder<PlayMode> builder)
        => builder.Property(x => x.Color).HasConversion<ColorToInt32Converter>();
}

public class ColorToInt32Converter : ValueConverter<Color, int>
{
    public ColorToInt32Converter()
        : base(c => c.ToArgb(), v => Color.FromArgb(v)) { }
}
