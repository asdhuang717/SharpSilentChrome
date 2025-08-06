using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpSilentChrome
{
    static class ProcessUtils
    {
        // Win32 API declarations for SID to username conversion
        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool LookupAccountSid(
            string lpSystemName,
            IntPtr Sid,
            StringBuilder lpName,
            ref int cchName,
            StringBuilder lpReferencedDomainName,
            ref int cchReferencedDomainName,
            out int peUse);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool ConvertStringSidToSid(
            string StringSid,
            out IntPtr Sid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern void FreeSid(IntPtr pSid);

        private static string GetUsernameFromSid(string sid)
        {
            try
            {
                if (string.IsNullOrEmpty(sid))
                    return null;

                IntPtr pSid;
                if (!ConvertStringSidToSid(sid, out pSid))
                {
                    Utils.WriteLine($"[-] Failed to convert SID to binary format: {sid}");
                    return null;
                }

                try
                {
                    int nameSize = 256;
                    int domainSize = 256;
                    StringBuilder name = new StringBuilder(nameSize);
                    StringBuilder domain = new StringBuilder(domainSize);
                    int sidType;

                    if (LookupAccountSid(null, pSid, name, ref nameSize, domain, ref domainSize, out sidType))
                    {
                        string username = name.ToString();
                        Utils.WriteLine($"[+] Resolved SID {sid} to username: {username}");
                        return username;
                    }
                    else
                    {
                        int error = Marshal.GetLastWin32Error();
                        Utils.WriteLine($"[-] Failed to lookup account SID {sid}, error: {error}");
                        return null;
                    }
                }
                finally
                {
                    FreeSid(pSid);
                }
            }
            catch (Exception ex)
            {
                Utils.WriteLine($"[-] Error resolving SID {sid} to username: {ex.Message}");
                return null;
            }
        }

        private static string GetUsernameFromProfilePath(string profilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(profilePath))
                    return null;

                // Extract username from profile path
                var username = Path.GetFileName(profilePath);
                Utils.WriteLine($"[+] Extracted username from profile path: {username}");
                return username;
            }
            catch (Exception ex)
            {
                Utils.WriteLine($"[-] Error extracting username from profile path {profilePath}: {ex.Message}");
                return null;
            }
        }
        public static bool IsProcessRunning(string processName, string sid = null, string profilePath = null)
        {
            try
            {
                // Determine username from SID first, then fall back to profile path
                string username = null;
                
                if (!string.IsNullOrEmpty(sid))
                {
                    username = GetUsernameFromSid(sid);
                }
                
                if (string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(profilePath))
                {
                    username = GetUsernameFromProfilePath(profilePath);
                }
                
                if (string.IsNullOrEmpty(username))
                {
                    Utils.WriteLine($"[-] Failed to get username from SID or profile path");
                    return false; 
                }

                // Use WMI to get processes by name and owner
                var query = $"SELECT ProcessId, Name FROM Win32_Process WHERE Name = '{processName}.exe'";
                var searcher = new ManagementObjectSearcher(query);
                var processes = searcher.Get();
                
                foreach (ManagementObject process in processes)
                {
                    try
                    {
                        var processId = Convert.ToInt32(process["ProcessId"]);
                        
                        // Get the owner of this process
                        var ownerQuery = $"SELECT * FROM Win32_Process WHERE ProcessId = {processId}";
                        var ownerSearcher = new ManagementObjectSearcher(ownerQuery);
                        var ownerProcesses = ownerSearcher.Get();
                        
                        foreach (ManagementObject ownerProcess in ownerProcesses)
                        {
                            string[] ownerInfo = new string[2];
                            int result = Convert.ToInt32(ownerProcess.InvokeMethod("GetOwner", ownerInfo));
                            
                            if (result == 0 && ownerInfo[0] != null)
                            {
                                var processOwner = ownerInfo[0];
                                if (string.Equals(processOwner, username, StringComparison.OrdinalIgnoreCase))
                                {
                                    Utils.WriteLine($"[+] Found {processName} process (PID: {processId}) owned by {processOwner}");
                                    return true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.WriteLine($"[-] Error getting owner for process: {ex.Message}");
                    }
                }
                
                Utils.WriteLine($"[+] No {processName} processes found for user '{username}'");
                return false;
            }
            catch (Exception ex)
            {
                Utils.WriteLine($"[-] Error checking if {processName} is running: {ex.Message}");
                return false;
            }
        }
        
        public static void CloseProcesses(string processName, string sid = null, string profilePath = null)
        {
            try
            {
                // Determine username from SID first, then fall back to profile path
                string username = null;
                
                if (!string.IsNullOrEmpty(sid))
                {
                    username = GetUsernameFromSid(sid);
                }
                
                if (string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(profilePath))
                {
                    username = GetUsernameFromProfilePath(profilePath);
                }
                
                if (string.IsNullOrEmpty(username))
                {   
                    Utils.WriteLine($"[-] Failed to get username from SID or profile path");
                    return;
                }

                // Use WMI to get processes by name and owner
                var query = $"SELECT ProcessId, Name FROM Win32_Process WHERE Name = '{processName}.exe'";
                var searcher = new ManagementObjectSearcher(query);
                var processes = searcher.Get();
                
                var userProcesses = new List<int>();
                
                foreach (ManagementObject process in processes)
                {
                    try
                    {
                        var processId = Convert.ToInt32(process["ProcessId"]);
                        
                        // Get the owner of this process
                        var ownerQuery = $"SELECT * FROM Win32_Process WHERE ProcessId = {processId}";
                        var ownerSearcher = new ManagementObjectSearcher(ownerQuery);
                        var ownerProcesses = ownerSearcher.Get();
                        
                        foreach (ManagementObject ownerProcess in ownerProcesses)
                        {
                            string[] ownerInfo = new string[2];
                            int result = Convert.ToInt32(ownerProcess.InvokeMethod("GetOwner", ownerInfo));
                            
                            if (result == 0 && ownerInfo[0] != null)
                            {
                                var processOwner = ownerInfo[0];
                                if (string.Equals(processOwner, username, StringComparison.OrdinalIgnoreCase))
                                {
                                    userProcesses.Add(processId);
                                    Utils.WriteLine($"[+] Found {processName} process (PID: {processId}) owned by {processOwner}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.WriteLine($"[-] Error getting owner for process: {ex.Message}");
                    }
                }
                
                if (userProcesses.Count == 0)
                {
                    Utils.WriteLine($"[+] No {processName} processes found for user '{username}' - continuing to installation");
                    return;
                }
                
                Utils.WriteLine($"[+] Found {userProcesses.Count} {processName} processes to close for user '{username}'");
                
                foreach (var processId in userProcesses)
                {
                    try
                    {
                        var process = Process.GetProcessById(processId);
                        Utils.WriteLine($"[+] Closing {processName} process (PID: {processId}) for user '{username}'");
                        process.Kill();
                        process.WaitForExit(100);
                    }
                    catch (Exception ex)
                    {
                        Utils.WriteLine($"[-] Failed to close {processName} process {processId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.WriteLine($"[-] Error closing {processName} processes: {ex.Message}");
            }
        }

        public static string FindBrowserExecutablePath(string browser)
        {
            var driveLetter = Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');
            string[] exePaths = null;

            if (browser.ToLower() == "chrome")
            {
                exePaths = new[]
                {
                    $"{driveLetter}\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe",
                    $"{driveLetter}\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
                    //$"{driveLetter}\\Users\\{username}\\AppData\\Local\\Google\\Chrome\\Application\\chrome.exe",
                    $"{driveLetter}\\Program Files (x86)\\Google\\Application\\chrome.exe"
                };
            }
            else if (browser.ToLower() == "msedge")
            {
                exePaths = new[]
                {
                    $"{driveLetter}\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
                    $"{driveLetter}\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe",
                    //$"{driveLetter}\\Users\\{username}\\AppData\\Local\\Microsoft\\Edge\\Application\\msedge.exe"
                };
            }
            else
            {
                Utils.WriteLine($"[-] Browser '{browser}' not supported");
                return null;
            }

            foreach (var exePath in exePaths)
            {
                if (File.Exists(exePath))
                {
                    Utils.WriteLine($"[+] Found {browser} executable: {exePath}");
                    return exePath;
                }
            }

            Utils.WriteLine($"[-] {browser} executable not found in common locations");
            return null;
        }

        public static string FindBrowserUserDataDir(string profilePath, string browser)
        {
            var driveLetter = Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');
            
            string userDataDir = null;
            if (browser.ToLower() == "chrome")
            {
                userDataDir = Path.Combine(profilePath, @"AppData\Local\Google\Chrome\User Data");
            }
            else if (browser.ToLower() == "msedge")
            {
                userDataDir = Path.Combine(profilePath, @"AppData\Local\Microsoft\Edge\User Data");
            }
            else
            {
                Utils.WriteLine($"[-] Cannot find user data for {browser}. Not supported.");
            }

            if (Directory.Exists(userDataDir))
            {
                Utils.WriteLine($"[+] Found Browser User Data directory: {userDataDir}");
                return userDataDir;
            }

            Utils.WriteLine($"[-] Browser User Data directory not found: {userDataDir}");
            return null;
        }

        public static void RestartBrowser(string exePath, string userDataDir)
        {
            try
            {
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    Utils.WriteLine("[-] Browser executable path is invalid");
                    return;
                }

                // 06/30: Removed --keep-alive-for-test for now 
                var arguments = $"--user-data-dir=\"{userDataDir}\" --restore-last-session";
                
                Utils.WriteLine($"[+] Starting Browser with arguments: {arguments}");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                var process = Process.Start(startInfo);
                Utils.WriteLine($"[+] Browser started with PID: {process?.Id}");
            }
            catch (Exception ex)
            {
                Utils.WriteLine($"[-] Failed to start Browser: {ex.Message}");
            }
        }
    }
}