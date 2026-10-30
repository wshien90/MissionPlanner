using System.Globalization;
using System.Resources;

namespace MissionPlanner.Utilities
{
    /// <summary>
    /// The FSEMI screens' text in English and Vietnamese, from FsemiStrings.resx and its .vi-VN sibling. These are
    /// not part of the upstream Crowdin translations.
    /// </summary>
    public static class FsemiText
    {
        static readonly ResourceManager resources =
            new ResourceManager("MissionPlanner.FsemiStrings", typeof(FsemiText).Assembly);

        /// <summary>The text in the interface language; the key itself when it is missing.</summary>
        public static string Get(string key)
        {
            return Get(key, CultureInfo.CurrentUICulture);
        }

        public static string Get(string key, CultureInfo culture)
        {
            try
            {
                return resources.GetString(key, culture) ?? key;
            }
            catch (MissingManifestResourceException)
            {
                return key;
            }
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }

        public static string Format(CultureInfo culture, string key, params object[] args)
        {
            return string.Format(Get(key, culture), args);
        }
    }
}
