using System.Net;

namespace RESK.WIL.Models
{
    public static class SecurityHelper
    {
        public static string Sanitize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            return WebUtility.HtmlEncode(input);
        }
    }
}

