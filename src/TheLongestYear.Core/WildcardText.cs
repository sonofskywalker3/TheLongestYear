namespace TheLongestYear.Core;

/// <summary>Player-facing twist names, <c>wildcard.twist.&lt;id&gt;</c> in i18n/default.json.
/// Falls back to the raw id when a key is missing, like <see cref="ThemeModifiers.DisplayNameFor"/>.</summary>
public static class WildcardText
{
    private const string KeyPrefix = "wildcard.twist.";

    public static string Name(string twistId)
    {
        string key = KeyPrefix + twistId;
        string result = Strings.Get(key);
        return result == key ? twistId : result;
    }
}
