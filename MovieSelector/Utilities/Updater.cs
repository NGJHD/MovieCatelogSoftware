using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;

namespace MovieSelector
{
    //GitHub answered, but not with something we can act on - no release yet, no zip attached,
    //a rate limit. Distinct from a network failure so the caller can say which it was.
    public class UpdateCheckException : Exception
    {
        public UpdateCheckException(string message) : base(message) { }
    }

    //What the newest published release is. Handed straight back to the UI, so it carries the
    //display strings too rather than making the caller rebuild them.
    public class ReleaseInfo
    {
        public Version Version;       //Parsed from the tag: "v4.1.6" -> 4.1.6
        public string TagName;        //"v4.1.6", as GitHub has it
        public string AssetName;      //"MCS-v4.1.6.zip"
        public string DownloadUrl;
        public long AssetSize;

        public bool IsNewerThanInstalled
        {
            get { return Version > Updater.InstalledVersion; }
        }
    }

    public static class Updater
    {
        //The release published by .github/workflows/release.yml. Anonymous API calls are capped
        //at 60/hour per IP, which a button nobody can press that fast will never reach.
        private const string latestReleaseUrl = "https://api.github.com/repos/NGJHD/MovieCatelogSoftware/releases/latest";

        //The two files a release ships. They are replaced together or not at all - the exe records
        //the exact Newtonsoft version it binds to, and shipping one without the other is what
        //crashed the app on 30 Aug 2026. See RELEASE_GUIDE.md, Part 2.
        private const string exeName = "MCS.exe";
        private const string jsonDllName = "Newtonsoft.Json.dll";

        //Staging folders are left behind if the machine dies mid-update, so they carry a prefix
        //we can recognise and sweep up on the next check.
        private const string stagingPrefix = "MCS-update-";

/*************************************************************************************************************************************/
        //Three components only. The tag carries three ("v4.1.6") while AssemblyVersion carries
        //four ("4.1.6.0"), and comparing those directly makes every release look newer.
        public static Version InstalledVersion
        {
            get
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                return new Version(version.Major, version.Minor, version.Build);
            }
        }

        //Where the running exe actually lives. MainModule rather than Assembly.Location so a
        //shadow-copied or renamed exe still reports the file on disk we have to overwrite.
        public static string InstalledExePath
        {
            get { return Process.GetCurrentProcess().MainModule.FileName; }
        }

        public static string InstallFolder
        {
            get { return Path.GetDirectoryName(InstalledExePath); }
        }

/*************************************************************************************************************************************/
        //One client for the process, same reasoning as the scraper - a client per request leaks
        //sockets into TIME_WAIT. Generous timeout because the same client fetches the zip.
        private static readonly HttpClient httpClient = createHttpClient();

        private static HttpClient createHttpClient()
        {
            enableModernTls();

            var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(5);

            //GitHub rejects requests with no User-Agent outright, with a 403 and no explanation.
            client.DefaultRequestHeaders.Add("User-Agent", "MCS-Updater/" + InstalledVersion);
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

            return client;
        }

        //Left to itself the framework can still hand us the legacy Ssl3 + TLS 1.0 pair, and
        //GitHub answers that with "Could not create SSL/TLS secure channel" - a connection
        //failure that looks exactly like being offline. Ask for 1.2 rather than assume it.
        private static void enableModernTls()
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol &= ~System.Net.SecurityProtocolType.Ssl3;
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }

            //Older Windows builds throw rather than ignore an unknown protocol, so 1.3 goes on
            //its own - losing it is fine, losing 1.2 with it would not be.
            try
            {
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls13;
            }
            catch (Exception)
            {
            }
        }

/*************************************************************************************************************************************/
        //Ask GitHub what the newest release is. Returns it whether or not it is newer than what
        //is installed, so the caller can say "you are on the latest version" and mean it.
        public static ReleaseInfo GetLatestRelease()
        {
            cleanUpAbandonedStaging();

            string body;

            using (HttpResponseMessage response = httpClient.GetAsync(latestReleaseUrl).GetAwaiter().GetResult())
            {
                body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode == false)
                {
                    //404 is the answer for both "no repository" and "no release published yet",
                    //and the second is the one a user is actually likely to hit.
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        throw new UpdateCheckException("No release has been published yet.");
                    }

                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        throw new UpdateCheckException("GitHub is rate limiting this connection. Try again in an hour.");
                    }

                    throw new UpdateCheckException("GitHub returned HTTP " + (int)response.StatusCode + ".");
                }
            }

            JObject release = JObject.Parse(body);

            string tag = (string)release["tag_name"];

            if (String.IsNullOrWhiteSpace(tag) == true)
            {
                throw new UpdateCheckException("The latest release has no tag.");
            }

            Version version = parseTag(tag);

            if (version == null)
            {
                throw new UpdateCheckException("Could not read a version number out of the release tag '" + tag + "'.");
            }

            JToken asset = findZipAsset(release);

            if (asset == null)
            {
                throw new UpdateCheckException("Release " + tag + " has no .zip attached to it.");
            }

            return new ReleaseInfo
            {
                Version = version,
                TagName = tag,
                AssetName = (string)asset["name"],
                DownloadUrl = (string)asset["browser_download_url"],
                AssetSize = (long?)asset["size"] ?? 0
            };
        }

        //"v4.1.6" and "4.1.6" both mean 4.1.6. Anything else is not a version we can compare.
        private static Version parseTag(string tag)
        {
            string trimmed = tag.Trim().TrimStart('v', 'V');

            Version parsed;

            if (Version.TryParse(trimmed, out parsed) == false)
            {
                return null;
            }

            //Build is -1 when the tag was just "4.1". Normalise so the comparison never sees it.
            return new Version(parsed.Major,
                               Math.Max(parsed.Minor, 0),
                               Math.Max(parsed.Build, 0));
        }

        private static JToken findZipAsset(JObject release)
        {
            JArray assets = release["assets"] as JArray;

            if (assets == null)
            {
                return null;
            }

            return assets.FirstOrDefault(a =>
            {
                string name = (string)a["name"];
                return String.IsNullOrEmpty(name) == false &&
                       name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true &&
                       String.IsNullOrEmpty((string)a["browser_download_url"]) == false;
            });
        }

/*************************************************************************************************************************************/
        //Fail here rather than after a download the user waited for. A read-only install folder
        //is the normal case when MCS was put somewhere under Program Files.
        public static bool IsInstallFolderWritable()
        {
            string probe = Path.Combine(InstallFolder, "." + stagingPrefix + "probe");

            try
            {
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, "Install folder is not writable: " + ex.Message);
                return false;
            }
        }

/*************************************************************************************************************************************/
        //Download the release zip, unpack it, and check what came out is actually the version we
        //asked for. Returns the folder holding the verified files, ready to be copied over.
        public static string DownloadAndStage(ReleaseInfo release)
        {
            string staging = Path.Combine(Path.GetTempPath(), stagingPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);

            try
            {
                string zipPath = Path.Combine(staging, release.AssetName);

                downloadTo(release.DownloadUrl, zipPath);

                string unpacked = Path.Combine(staging, "files");
                ZipFile.ExtractToDirectory(zipPath, unpacked);

                //The zip is flat, but a future one might not be - find the files wherever they are.
                string newExe = findFile(unpacked, exeName);
                string newDll = findFile(unpacked, jsonDllName);

                if (newExe == null || newDll == null)
                {
                    throw new UpdateCheckException("The download is missing " +
                                                   (newExe == null ? exeName : jsonDllName) +
                                                   ". Nothing has been changed.");
                }

                verifyVersion(newExe, release);

                //Flatten so the copy step has one predictable layout to work with.
                string ready = Path.Combine(staging, "ready");
                Directory.CreateDirectory(ready);
                File.Copy(newExe, Path.Combine(ready, exeName), true);
                File.Copy(newDll, Path.Combine(ready, jsonDllName), true);

                File.Delete(zipPath);

                return ready;
            }
            catch (Exception)
            {
                tryDelete(staging);
                throw;
            }
        }

        private static void downloadTo(string url, string destination)
        {
            using (HttpResponseMessage response = httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
            {
                if (response.IsSuccessStatusCode == false)
                {
                    throw new UpdateCheckException("The download failed with HTTP " + (int)response.StatusCode + ".");
                }

                using (Stream source = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                using (FileStream target = File.Create(destination))
                {
                    source.CopyTo(target);
                }
            }
        }

        //The release workflow already refuses to publish a zip whose exe disagrees with the tag.
        //Checking again here costs nothing and catches a hand-uploaded asset that skipped it.
        private static void verifyVersion(string exePath, ReleaseInfo release)
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(exePath);

            Version downloaded = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart);

            if (downloaded != release.Version)
            {
                throw new UpdateCheckException("The downloaded " + exeName + " reports version " + downloaded +
                                               ", but release " + release.TagName + " claims " + release.Version +
                                               ". Nothing has been changed.");
            }
        }

        private static string findFile(string root, string fileName)
        {
            return Directory.GetFiles(root, fileName, SearchOption.AllDirectories).FirstOrDefault();
        }

/*************************************************************************************************************************************/
        //The running exe cannot overwrite itself, and neither can it release Newtonsoft.Json.dll
        //while it is loaded. So hand both replacements to a script, quit, and let it restart us.
        public static void LaunchUpdaterAndQuit(string readyFolder)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), stagingPrefix + Guid.NewGuid().ToString("N") + ".cmd");

            File.WriteAllText(scriptPath, buildUpdaterScript(readyFolder), new UTF8Encoding(false));

            var startInfo = new ProcessStartInfo
            {
                FileName = scriptPath,
                WorkingDirectory = Path.GetTempPath(),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);
        }

        private static string buildUpdaterScript(string readyFolder)
        {
            int pid = Process.GetCurrentProcess().Id;
            string exePath = InstalledExePath;
            string installFolder = InstallFolder;
            //Anchored to the install folder, not GlobalPath.LOG_PATH - that one is relative to the
            //working directory, and by the time the script runs there is no MCS process to have one.
            string logFolder = Path.Combine(installFolder, "Log");
            string logPath = Path.Combine(logFolder, "MCS_update.log");
            string staging = Path.GetDirectoryName(readyFolder);

            //The script ends with "rd /s /q" on this folder. Only ever hand it one we made
            //ourselves - pointed at anything else that line would take a real folder with it.
            bool stagingIsOurs = Path.GetFileName(staging)
                                     .StartsWith(stagingPrefix, StringComparison.OrdinalIgnoreCase);

            //Everything is baked in rather than passed as arguments - the install folder can be a
            //mapped network drive with spaces in it, and this way there is no quoting to get wrong.
            var script = new StringBuilder();

            script.AppendLine("@echo off");
            script.AppendLine("rem Written by MCS at update time. Safe to delete.");
            script.AppendLine("setlocal");
            script.AppendLine();
            script.AppendLine("set \"READY=" + readyFolder + "\"");
            script.AppendLine("set \"STAGE=" + staging + "\"");
            script.AppendLine("set \"TARGET=" + installFolder + "\"");
            script.AppendLine("set \"EXE=" + exePath + "\"");
            script.AppendLine("set \"LOG=" + logPath + "\"");
            script.AppendLine();
            script.AppendLine("if not exist \"" + logFolder + "\" md \"" + logFolder + "\"");
            script.AppendLine("echo [%DATE% %TIME%] update starting >>\"%LOG%\"");
            script.AppendLine();
            script.AppendLine("rem Both files stay locked until MCS has actually gone. Give it a minute.");
            script.AppendLine("set /a TRIES=0");
            script.AppendLine(":waitloop");
            script.AppendLine("tasklist /FI \"PID eq " + pid + "\" /NH 2>nul | find /I \"" + Path.GetFileName(exePath) + "\" >nul");
            script.AppendLine("if errorlevel 1 goto exited");
            script.AppendLine("set /a TRIES+=1");
            script.AppendLine("if %TRIES% GEQ 60 (");
            script.AppendLine("  echo [%DATE% %TIME%] MCS is still running after 60s - nothing replaced >>\"%LOG%\"");
            script.AppendLine("  goto restart");
            script.AppendLine(")");
            script.AppendLine("ping -n 2 127.0.0.1 >nul");
            script.AppendLine("goto waitloop");
            script.AppendLine();
            script.AppendLine(":exited");
            script.AppendLine("rem Newtonsoft first. If that copy fails nothing has been touched and the old");
            script.AppendLine("rem install still runs. Doing the exe first would leave a new exe bound to the");
            script.AppendLine("rem old DLL, which is the combination that does not start at all.");
            script.AppendLine("copy /Y \"%READY%\\" + jsonDllName + "\" \"%TARGET%\\" + jsonDllName + "\" >>\"%LOG%\" 2>&1");
            script.AppendLine("if errorlevel 1 (");
            script.AppendLine("  echo [%DATE% %TIME%] could not replace " + jsonDllName + " - install left untouched >>\"%LOG%\"");
            script.AppendLine("  goto restart");
            script.AppendLine(")");
            script.AppendLine();
            script.AppendLine("copy /Y \"%READY%\\" + exeName + "\" \"%EXE%\" >>\"%LOG%\" 2>&1");
            script.AppendLine("if errorlevel 1 (");
            script.AppendLine("  echo [%DATE% %TIME%] could not replace " + exeName + " >>\"%LOG%\"");
            script.AppendLine("  goto restart");
            script.AppendLine(")");
            script.AppendLine();
            script.AppendLine("echo [%DATE% %TIME%] update applied >>\"%LOG%\"");
            script.AppendLine();
            script.AppendLine(":restart");

            if (stagingIsOurs == true)
            {
                script.AppendLine("rd /s /q \"%STAGE%\" 2>nul");
            }

            script.AppendLine("start \"\" /D \"%TARGET%\" \"%EXE%\"");
            script.AppendLine("exit /b 0");

            return script.ToString();
        }

/*************************************************************************************************************************************/
        //A machine that lost power mid-update leaves its staging folder behind. Nobody will ever
        //go looking for it, so sweep the old ones every time we check.
        private static void cleanUpAbandonedStaging()
        {
            try
            {
                string temp = Path.GetTempPath();

                foreach (string folder in Directory.GetDirectories(temp, stagingPrefix + "*"))
                {
                    if (Directory.GetLastWriteTimeUtc(folder) < DateTime.UtcNow.AddDays(-1))
                    {
                        tryDelete(folder);
                    }
                }

                foreach (string script in Directory.GetFiles(temp, stagingPrefix + "*.cmd"))
                {
                    if (File.GetLastWriteTimeUtc(script) < DateTime.UtcNow.AddDays(-1))
                    {
                        try
                        {
                            File.Delete(script);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private static void tryDelete(string folder)
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception)
            {
            }
        }
/*************************************************************************************************************************************/
    }
}
