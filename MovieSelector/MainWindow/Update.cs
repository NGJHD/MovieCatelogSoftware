using System;
using System.Threading;
using System.Windows;

namespace MovieSelector
{
    public partial class MainWindow : System.Windows.Window
    {
/*************************************************************************************************************************************/
        //One check at a time. The button stays clickable during the network call, and two checks
        //racing each other would leave two staging folders and two prompts.
        private bool updateCheckRunning = false;

        //Shown under "Software Update" in Options. Set once so reopening Options does not wipe
        //the result of the last check.
        private void initUpdateStatus()
        {
            try
            {
                if (String.IsNullOrEmpty(updateStatusTB.Text) == true)
                {
                    updateStatusTB.Text = "Installed version " + Updater.InstalledVersion;
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void checkForUpdateGrid_Click(object sender, EventArgs e)
        {
            try
            {
                if (updateCheckRunning == true)
                {
                    return;
                }

                updateCheckRunning = true;
                checkForUpdateBtn.IsEnabled = false;
                updateStatusTB.Text = "Checking GitHub for a newer release...";

                Thread updateCheckThread = new Thread(checkForUpdate);
                updateCheckThread.IsBackground = true;
                updateCheckThread.Start();
            }
            catch (Exception ex)
            {
                updateCheckRunning = false;
                checkForUpdateBtn.IsEnabled = true;
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

/*************************************************************************************************************************************/
        //Runs on a worker thread - the API call and the download both block, and a frozen window
        //during either is worse than no update button at all.
        private void checkForUpdate()
        {
            ReleaseInfo release = null;
            string failure = null;

            try
            {
                release = Updater.GetLatestRelease();
            }
            catch (UpdateCheckException ex)
            {
                failure = ex.Message;
                Log.Write(Log.LogMsgType.I, "Update check: " + ex.Message);
            }
            catch (Exception ex)
            {
                failure = "Could not reach GitHub. Check the network connection.";
                Log.Write(Log.LogMsgType.I, "Update check: " + ex.ToString());
            }

            Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    updateCheckRunning = false;
                    checkForUpdateBtn.IsEnabled = true;

                    if (failure != null)
                    {
                        updateStatusTB.Text = failure;
                        AddToNotification("Update check failed. " + failure);
                        return;
                    }

                    if (release.IsNewerThanInstalled == false)
                    {
                        updateStatusTB.Text = "Version " + Updater.InstalledVersion + " is the latest.";
                        AddToNotification("MCS is up to date (version " + Updater.InstalledVersion + ").");
                        return;
                    }

                    updateStatusTB.Text = "Version " + release.Version + " is available.";

                    promptAndInstall(release);
                }
                catch (Exception ex)
                {
                    Log.Write(Log.LogMsgType.I, ex.Message);
                }
            }));
        }

/*************************************************************************************************************************************/
        private void promptAndInstall(ReleaseInfo release)
        {
            try
            {
                MessageBoxResult answer =
                    MessageBox.Show("Version " + release.Version + " is available (you have " + Updater.InstalledVersion + ").\n\n" +
                                    "MCS will download it, close, replace itself and start again.\n\n" +
                                    "Download and install now?",
                                    "Update available",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question);

                if (answer != MessageBoxResult.Yes)
                {
                    AddToNotification("Update to " + release.Version + " postponed.");
                    return;
                }

                //Worth failing before the download rather than after it. An install under
                //Program Files, or a network share mounted read-only, cannot be replaced.
                if (Updater.IsInstallFolderWritable() == false)
                {
                    updateStatusTB.Text = "Cannot write to the install folder.";

                    MessageBox.Show("MCS cannot write to its own folder:\n\n" + Updater.InstallFolder + "\n\n" +
                                    "Nothing has been changed. Run MCS as administrator, or update it by hand " +
                                    "from the GitHub releases page.",
                                    "Update",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                    return;
                }

                updateCheckRunning = true;
                checkForUpdateBtn.IsEnabled = false;
                updateStatusTB.Text = "Downloading version " + release.Version + "...";
                AddToNotification("Downloading MCS " + release.Version + "...");

                Thread downloadThread = new Thread(() => downloadAndInstall(release));
                downloadThread.IsBackground = true;
                downloadThread.Start();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void downloadAndInstall(ReleaseInfo release)
        {
            string ready = null;
            string failure = null;

            try
            {
                ready = Updater.DownloadAndStage(release);
            }
            catch (UpdateCheckException ex)
            {
                failure = ex.Message;
                Log.Write(Log.LogMsgType.I, "Update download: " + ex.Message);
            }
            catch (Exception ex)
            {
                failure = "The download failed. Nothing has been changed.";
                Log.Write(Log.LogMsgType.I, "Update download: " + ex.ToString());
            }

            Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    updateCheckRunning = false;
                    checkForUpdateBtn.IsEnabled = true;

                    if (failure != null)
                    {
                        updateStatusTB.Text = failure;

                        MessageBox.Show(failure, "Update", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    applyUpdateAndRestart(ready);
                }
                catch (Exception ex)
                {
                    Log.Write(Log.LogMsgType.I, ex.Message);
                }
            }));
        }

/*************************************************************************************************************************************/
        private void applyUpdateAndRestart(string readyFolder)
        {
            try
            {
                //The database save is debounced by three seconds. Quitting inside that window
                //would drop whatever the last scrape wrote, so flush it before going anywhere.
                flushPendingDatabaseSave();

                cancelAllRunningWork();

                Updater.LaunchUpdaterAndQuit(readyFolder);

                //The script is waiting on this process to exit before it can touch either file.
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.ToString());

                MessageBox.Show("The update could not be started. Nothing has been changed.",
                                "Update",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }
        }

        private void flushPendingDatabaseSave()
        {
            try
            {
                saveXMLTimer.Stop();

                if (GlobalVariables.XmlMovieDoc != null && GlobalVariables.XmlMovieDoc.DocumentElement != null)
                {
                    GlobalVariables.XmlMovieDoc.Save(GlobalPath.MOVIE_DATABASE_PATH);
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }
/*************************************************************************************************************************************/
    }
}
