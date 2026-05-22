using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IfsaKlasik.Web.Infrastructure;

/// <summary>
/// SQL Server datetime2 ile EF’nin sıklıkla <see cref="DateTimeKind.Unspecified"/> döndürmesi yüzünden
/// tarih çıktısında zaman dilimi (Z) olmayabiliyor; bazı tarayıcılar geri sayımı bozuyor.
/// Bu dönüştürücı değeri UTC olarak yorumlayıp her zaman RFC 3339 ile Z yazar (SignalR PayloadSerializer’a eklenir).
/// </summary>
public sealed class SignalRUtcIsoDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => throw new FormatException(),
            JsonTokenType.String => DateTime.Parse(
                reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            _ => reader.GetDateTime(),
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => SignalRDateTimeWriteHelper.WriteUtcIso(writer, value);
}

/// <inheritdoc cref="SignalRUtcIsoDateTimeConverter" />
public sealed class SignalRUtcIsoNullableDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
            return DateTime.Parse(
                reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        return reader.GetDateTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        SignalRDateTimeWriteHelper.WriteUtcIso(writer, value.Value);
    }
}

internal static class SignalRDateTimeWriteHelper
{
    internal static void WriteUtcIso(Utf8JsonWriter writer, DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value,
        };

        var utcFixed = utc.ToUniversalTime();

        writer.WriteStringValue(
            utcFixed.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture) + "Z");
    }
}
