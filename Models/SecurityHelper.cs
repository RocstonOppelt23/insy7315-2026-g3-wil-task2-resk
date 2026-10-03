using System.Net;

namespace RESK.WIL.Models
{
    public static class SecurityHelper
    {
        // Rocston's code: small helper for HTML-encoding user-provided text before display.
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
