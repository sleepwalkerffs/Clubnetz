using System.Text.Json;
using System.Text.Json.Serialization;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Infrastructure;

public class DateTimeOffsetIntervalTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    [Fact]
    public void IntervalAcrossDaylightSavingChange_CanBeReadInLocalTime()
    {
        // A winter break from 1 November to 31 March: the end is in summer time
        var winterBreak = new DateTimeOffsetInterval(new DateTimeOffset(2026, 10, 31, 23, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 3, 31, 22, 0, 0, TimeSpan.Zero));
        var json = JsonSerializer.Serialize(winterBreak);

        // The client reads every time in its local time (here Vienna), like its DateTimeOffsetJsonConverter
        var options = new JsonSerializerOptions { Converters = { new ViennaTimeConverter() } };
        var read = JsonSerializer.Deserialize<DateTimeOffsetInterval>(json, options)!;

        read.From.Offset.Should().Be(TimeSpan.FromHours(1));
        read.To.Offset.Should().Be(TimeSpan.FromHours(2));
        read.Should().Be(winterBreak);
        (read.To - read.From).Should().Be(winterBreak.To - winterBreak.From);
    }

    [Fact]
    public void ToBeforeFrom_IsRefused()
    {
        var act = () => new DateTimeOffsetInterval(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(-1));
        act.Should().Throw<ArgumentException>();
    }

    private class ViennaTimeConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => TimeZoneInfo.ConvertTime(reader.GetDateTimeOffset(), Vienna);

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToUniversalTime());
    }
}
