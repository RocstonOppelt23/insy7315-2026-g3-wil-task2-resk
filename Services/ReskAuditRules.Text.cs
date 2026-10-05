using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace RESK.WIL.Services
{
    // Second half of ReskAuditRules: wording, IP address and device.
    public static partial class ReskAuditRules
    {
        // =========================================================
        // TEXT HELPERS
        // =========================================================

        // "RequestChanges" -> "Request changes"
        public static string Humanise(string name)
        {
            string spaced = Regex.Replace(name ?? "", "(?<=[a-z0-9])(?=[A-Z])", " ").Replace('_', ' ').Replace('-', ' ').Trim();

            return spaced.Length == 0 ? "Action" : char.ToUpperInvariant(spaced[0]) + spaced.Substring(1).ToLowerInvariant();
        }


        public static string Source(HttpContext context)
        {
            string first = (context.Request.Path.Value ?? "").Trim('/').Split('/')[0].ToLowerInvariant();

            return first switch
            {
                "admin" => "Admin web application",
                "producer" => "Producer web application",
                "reviewer" => "Reviewer web application",
                "account" or "identity" => "Sign-in page",
                _ => "Web application"
            };
        }


        public static string Ip(HttpContext context)
        {
            IPAddress? address = context.Connection.RemoteIpAddress;

            if (address == null)
            {
                return "Unknown";
            }

            if (IPAddress.IsLoopback(address))
            {
                return "127.0.0.1 (this computer)";
            }

            return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
        }


        // "Chrome on Windows"
        public static string Device(HttpContext context)
        {
            string agent = context.Request.Headers["User-Agent"].ToString();

            if (agent.Length == 0)
            {
                return "Unknown device";
            }

            string browser =
                agent.Contains("Edg/") ? "Edge" :
                agent.Contains("OPR/") ? "Opera" :
                agent.Contains("Firefox/") ? "Firefox" :
                agent.Contains("Chrome/") ? "Chrome" :
                agent.Contains("Safari/") ? "Safari" : "Browser";

            string system =
                agent.Contains("Windows") ? "Windows" :
                agent.Contains("Android") ? "Android" :
                agent.Contains("iPhone") || agent.Contains("iPad") ? "iOS" :
                agent.Contains("Mac OS X") ? "macOS" :
                agent.Contains("Linux") ? "Linux" : "unknown system";

            return browser + " on " + system;
        }
    }
}