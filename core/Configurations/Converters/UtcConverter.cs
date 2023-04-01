using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Configurations.Converters;

public static class UtcConverters
{
    public static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    public static readonly ValueConverter<DateTime?, DateTime?> UtcNullableConverter =
        new(v => v, v => v == null ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));
}