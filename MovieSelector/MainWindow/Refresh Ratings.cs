using System;
using System.Xml;
using IMDB_Scraper;
using System.Windows;
using System.Collections.Generic;

namespace MovieSelector
{
    public partial class MainWindow : System.Windows.Window
    {
/*************************************************************************************************************************************/
        //Cancels a previous rating refresh without disturbing the other background work.
        private System.Threading.CancellationTokenSource refreshRatingCancellationSource = new System.Threading.CancellationTokenSource();
        private int refreshThreadCount = 0;
/*************************************************************************************************************************************/
        private void RefreshLast10RatingsGrid_Click(object sender, EventArgs e)
        {
            try
            {
                RefreshRatings(10);
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void RefreshLast20RatingsGrid_Click(object sender, EventArgs e)
        {
            try
            {
                RefreshRatings(20);
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void RefreshAllRatingsGrid_Click(object sender, EventArgs e)
        {
            try
            {
                RefreshRatings(-1);
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void RefreshRatings(int last, bool onlyWithUnknownRating=false)
        {
            try
            {
                if (onlyWithUnknownRating == false)
                {
                    AddToNotification("Starting rating refresh of " + (last == -1 ? movieLB.Items.Count : last) + " movies...");
                }
                

                int totalCount = Math.Min(movieLB.Items.Count, (last == -1 ? movieLB.Items.Count : last));

                //Stop the previous refresh before starting another
                refreshRatingCancellationSource.Cancel();
                refreshRatingCancellationSource = new System.Threading.CancellationTokenSource();

                //Capture both tokens now: the feature one, and the global one used on close/list refresh
                System.Threading.CancellationToken refreshToken = refreshRatingCancellationSource.Token;
                System.Threading.CancellationToken workToken = GlobalVariables.WorkToken;

                System.Threading.Thread refreshRatingThread = new System.Threading.Thread(() => refreshRatingFn(totalCount, onlyWithUnknownRating, refreshToken, workToken));
                refreshRatingThread.IsBackground = true;
                refreshRatingThread.Start();

                hideOptions();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }
/*************************************************************************************************************************************/
        private void refreshRatingFn(int totalCount, bool onlyWithUnknownRating, System.Threading.CancellationToken refreshToken, System.Threading.CancellationToken workToken)
        {
            try
            {
                List<string> listOfMovieNames = new List<string>();
                Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                {
                    for (int i = 0; i < totalCount; i++)
                    {
                        listOfMovieNames.Add((movieLB.Items[i] as MovieListBoxClass).movieName);
                    }
                }));

                //Loop through all the movies
                for (int i = 0; i < totalCount; i++)
                {
                    if (isRefreshCancelled(refreshToken, workToken) == true)
                    {
                        return;
                    }

                    //Max of 5 threads for stability
                    while (refreshThreadCount >= 5)
                    {
                        if (sleepUnlessRefreshCancelled(refreshToken, workToken, 500) == false)
                        {
                            return;
                        }
                    }

                    //A movie that has not been scrapped yet simply has no entry - skip it rather than
                    //letting the missing key throw and abandon every remaining movie in the list.
                    MovieDataClass movieData;
                    if (GlobalVariables.MemoryDatabase.TryGetValue(listOfMovieNames[i], out movieData) == false)
                    {
                        continue;
                    }

                    if (onlyWithUnknownRating == false || movieData.Rating == "?")
                    {
                        //Add to the thread count immediately when entering
                        System.Threading.Interlocked.Increment(ref refreshThreadCount);

                        int temp = i;

                        System.Threading.Thread fetchRatingThread = new System.Threading.Thread(() => fetchRatingFromIMDB(temp, refreshToken, workToken));
                        fetchRatingThread.IsBackground = true;
                        fetchRatingThread.Start();
                    }
                }

                while (refreshThreadCount != 0)
                {
                    if (sleepUnlessRefreshCancelled(refreshToken, workToken, 500) == false)
                    {
                        return;
                    }
                }

                saveXMLTimer.Start();

                if (onlyWithUnknownRating == false)
                {
                    Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                    {
                        AddToNotification("Rating refresh completed");
                    }));
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private static bool isRefreshCancelled(System.Threading.CancellationToken refreshToken, System.Threading.CancellationToken workToken)
        {
            return refreshToken.IsCancellationRequested || workToken.IsCancellationRequested;
        }

        //Returns false once the caller should stop.
        private static bool sleepUnlessRefreshCancelled(System.Threading.CancellationToken refreshToken, System.Threading.CancellationToken workToken, int milliseconds)
        {
            return System.Threading.WaitHandle.WaitAny(
                       new System.Threading.WaitHandle[] { refreshToken.WaitHandle, workToken.WaitHandle },
                       milliseconds) == System.Threading.WaitHandle.WaitTimeout;
        }

        private void fetchRatingFromIMDB(int i, System.Threading.CancellationToken refreshToken, System.Threading.CancellationToken workToken)
        {
            try
            {
                string movieName = "";
                Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                {
                    movieName = (movieLB.Items[i] as MovieListBoxClass).movieName;
                }));

                if (GlobalVariables.MemoryDatabase.ContainsKey(movieName) == true && GlobalVariables.MemoryDatabase[movieName].ImdbID != "")
                {
                    //Fetch from imdb
                    IMDB imdb = new IMDB(@"https://www.imdb.com/title/" + GlobalVariables.MemoryDatabase[movieName].ImdbID, true);

                    if (isRefreshCancelled(refreshToken, workToken) == true)
                    {
                        return;
                    }

                    //Check if parsed successfully. Rating is a field that always exists in IMDB.
                    if (String.IsNullOrWhiteSpace(imdb.Rating) == false )
                    {
                        Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                        {
                            string oldRating = GlobalVariables.MemoryDatabase[movieName].Rating;
                            GlobalVariables.MemoryDatabase[movieName].Rating = imdb.Rating;

                            //Refresh the entry in the list box
                            RefreshEntryInListBox(RefreshType.TEXT, movieName);

                            //Refresh the movie data if it's currently selected
                            if (IsMovieSelected(movieName) == true)
                            {
                                DisplayMovieData(movieName);
                            }

                            AddToNotification(oldRating == imdb.Rating ? 
                                              "Refreshed " + movieName + "'s rating... no changes" : 
                                              "Refreshed " + movieName + "'s rating from " + oldRating + " to " + imdb.Rating);
                        }));

                        updateRatingToFile(movieName, imdb.Rating);
                    }
                }
            }
            catch (Exception ex)
            {
                ReportOmdbProblem(ex);
            }
            finally
            {
                System.Threading.Interlocked.Decrement(ref refreshThreadCount);
            }
        }

        private void updateRatingToFile(string name, string rating)
        {
            try
            {
                XmlNodeList xmlMovieList = GlobalVariables.XmlMovieDoc.SelectNodes("IMDB_Database/Movie");

                for (int i = xmlMovieList.Count - 1; i >= 0; i--)
                {
                    if (xmlMovieList[i].SelectSingleNode("Name").InnerText == name)
                    {
                        xmlMovieList[i].SelectSingleNode("Rating").InnerText = rating;
                        break;
                    }
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
