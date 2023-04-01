using System.Data;
using System.Globalization;
using Dapper;
using NodaTime;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Repositories.SqlTypeHandlers;

public class InstantHandler : SqlMapper.TypeHandler<Instant>
{
    public static readonly InstantHandler Default = new();

    public override void SetValue(IDbDataParameter parameter, Instant value)
    {
        parameter.Value = value.ToDateTimeUtc();
    }

    public override Instant Parse(object value)
    {
        switch (value)
        {
            case Instant self:
                return self;
            case null:
                return default;
            case DateTime dateTime:
                var dt = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                return Instant.FromDateTimeUtc(dt);
            case DateTimeOffset dateTimeOffset:
                return Instant.FromDateTimeOffset(dateTimeOffset);
            case string dateTime:
                return Instant.FromDateTimeUtc(DateTime.Parse(dateTime, CultureInfo.InvariantCulture, 
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal) );
            default:
                throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.Instant");
        }
    }
}