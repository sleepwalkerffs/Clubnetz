using System.Text.Json;
using Bookennis.Domain.Weather;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookennis.Api.Data.EntityConfigurations;

public class WeatherForecastEntityConfiguration : IEntityTypeConfiguration<WeatherForecast>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<WeatherForecast> builder)
    {
        builder.ConfigureTenantRelation();
        builder.HasIndex(x => x.ClubId).IsUnique();
        builder.Property(x => x.Latitude).HasPrecision(7, 4);
        builder.Property(x => x.Longitude).HasPrecision(7, 4);
        builder.Property(x => x.LastModified).HasMaxLength(WeatherForecast.MaxLastModifiedLength);
        builder.Property(x => x.Periods)
               .HasColumnType("jsonb")
               .HasConversion(
                   periods => JsonSerializer.Serialize(periods, JsonOptions),
                   json => JsonSerializer.Deserialize<List<WeatherPeriod>>(json, JsonOptions) ?? new List<WeatherPeriod>(),
                   new ValueComparer<List<WeatherPeriod>>(
                       (a, b) => a != null && b != null && a.SequenceEqual(b),
                       c => c.Aggregate(0, (hash, p) => HashCode.Combine(hash, p.GetHashCode())),
                       c => c.ToList()));
    }
}
