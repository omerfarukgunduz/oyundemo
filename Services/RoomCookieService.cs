using Microsoft.AspNetCore.Http;

namespace IfsaKlasik.Web.Services;

public interface IRoomCookieService
{
    void SetParticipantCookie(HttpResponse response, string roomCodeNormalized, Guid memberPublicId);

    Guid? TryGetParticipantPublicId(HttpRequest request, string roomCodeNormalized);
}

public sealed class RoomCookieService : IRoomCookieService
{
    public const string CookiePrefix = "ifsak_rm_";

    public void SetParticipantCookie(HttpResponse response, string roomCodeNormalized, Guid memberPublicId)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(2),
            Path = "/",
        };

        response.Cookies.Append(BuildName(roomCodeNormalized), memberPublicId.ToString("N"), options);
    }

    public Guid? TryGetParticipantPublicId(HttpRequest request, string roomCodeNormalized)
    {
        if (!request.Cookies.TryGetValue(BuildName(roomCodeNormalized), out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return Guid.TryParse(raw, out var g) ? g : null;
    }

    private static string BuildName(string roomCodeNormalized) => CookiePrefix + roomCodeNormalized.ToUpperInvariant();
}
