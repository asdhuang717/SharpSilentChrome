using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SharpSilentChrome
{
    static class Utils
    {
        public static void WriteLine(string message)
        {
#if DEBUG
            Console.WriteLine($"[DEBUG] {message}");
#endif
        }

        public static string GetExtensionId(string path)
        {
            var bytes = Encoding.Unicode.GetBytes(path);
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                var sb = new StringBuilder();
                foreach (var c in hex)
                {
                    int val = Convert.ToInt32(c.ToString(), 16);
                    sb.Append((char)(val + 'a'));
                    if (sb.Length == 32) break;
                }
                return sb.ToString();
            }
        }

        // Not used anymore, but keeping it just in case - peak SWE experience
        public static string GetUsernameFromSID(string sid)
        {
            try
            {
                string regPath = $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\{sid}";
                object profileImagePath = Microsoft.Win32.Registry.GetValue(regPath, "ProfileImagePath", null);

                if (profileImagePath == null)
                {
                    Utils.WriteLine($"[-] Could not find profile path for SID: {sid}");
                    return null;
                }

                string userProfilePath = profileImagePath.ToString();
                string username = Path.GetFileName(userProfilePath);

                Utils.WriteLine($"[+] Resolved username from SID: {username}");
                return username;
            }
            catch (Exception ex)
            {
                Utils.WriteLine($"[-] Error resolving username from SID {sid}: {ex.Message}");
                return null;
            }
        }

        public static (string securePrefs, string prefs) GetPrefsPaths(string profilePath, string browser)
        {
            string securePrefsPath = "";
            string prefsPath = "";

            // Build Secure Preferences and Preferences file paths
            if (browser.ToLower() == "chrome")
            {
                securePrefsPath = Path.Combine(profilePath, @"AppData\Local\Google\Chrome\User Data\Default\Secure Preferences");
                prefsPath = Path.Combine(profilePath, @"AppData\Local\Google\Chrome\User Data\Default\Preferences");
            }
            else if (browser.ToLower() == "msedge")
            {
                securePrefsPath = Path.Combine(profilePath, @"AppData\Local\Microsoft\Edge\User Data\Default\Secure Preferences");
                prefsPath = Path.Combine(profilePath, @"AppData\Local\Microsoft\Edge\User Data\Default\Preferences");
            }
            else
            {
                Utils.WriteLine($"[-] Browser not supported: {browser}");
                return (null, null);
            }

            return (securePrefsPath, prefsPath);
        }

    }
}