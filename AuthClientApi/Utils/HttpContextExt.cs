namespace AuthServiceClientApi.Utils;

public static class HttpContextExt
{
    public static UserClaim? GetUserClaim(this HttpContext? context)
    {
        if (context == null) return null;
        if (context.Items.TryGetValue("UserClaim", out var item))
        {
            return (UserClaim?)item;
        }
        return null;
    }
}