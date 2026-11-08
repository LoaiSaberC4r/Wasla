namespace Wasla.Domain.Resources;

public static class FamilySelfServiceText
{
    private static readonly System.Resources.ResourceManager Resources = new("Wasla.Domain.Resources.ErrorMessage", typeof(ErrorMessage).Assembly);
    public static string Get(string key) => Resources.GetString(key,
        System.Globalization.CultureInfo.CurrentUICulture) ?? key;
}
