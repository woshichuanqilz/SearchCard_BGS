using WPFLocalizeExtension.Engine;

namespace CardSearcher
{
    public static class LocalizeTools
    {  /// <summary>
       /// Gets the localized translation of the given string.
       /// </summary>
       /// <param name="key">The key.</param>
       /// <returns>The localized string</returns>
        public static string GetLocalized(string key) => LocalizeDictionary.Instance.GetLocalizedObject("CardSearcher", "StringsResource", key, LocalizeDictionary.Instance.Culture)?.ToString();
    }
}