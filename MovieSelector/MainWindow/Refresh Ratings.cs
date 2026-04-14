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
        private List<System.Threading.Thread> refreshRatingThreadList = new List<System.Threading.Thread>();
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
                foreach (System.Threading.Thread th in refreshRatingThreadList)
                {
                    if (th != null && th.IsAlive == true)
                    {
                        th.Abort();
                    }
                }
                refreshRatingThreadList.Clear();

                System.Threading.Thread refreshRatingThread = new System.Threading.Thread(() => refreshRatingFn(totalCount, onlyWithUnknownRating));
                refreshRatingThread.IsBackground = true;
                refreshRatingThread.Start();
                GlobalVariables.ListOfRunningThreads.Add(refreshRatingThread);
                refreshRatingThreadList.Add(refreshRatingThread);

                hideOptions();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }
/*************************************************************************************************************************************/
        private void refreshRatingFn(int totalCount, bool onlyWithUnknownRating)
        {
            try
            {
                refreshThreadCount = 0;

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
                    //Max of 5 threads for stability
                    while (refreshThreadCount > 5)
                    {
                        System.Threading.Thread.Sleep(500);
                    }

                    if (onlyWithUnknownRating == false || GlobalVariables.MemoryDatabase[listOfMovieNames[i]].Rating == "?")
                    {
                        //Add to the thread count immediately when entering
                        refreshThreadCount++;

                        int temp = i;

                        System.Threading.Thread fetchRatingThread = new System.Threading.Thread(() => fetchRatingFromIMDB(temp));
                        fetchRatingThread.IsBackground = true;
                        fetchRatingThread.Start();
                        GlobalVariables.ListOfRunningThreads.Add(fetchRatingThread);
                        refreshRatingThreadList.Add(fetchRatingThread);
                    }
                }

                while (refreshThreadCount != 0)
                {
                    System.Threading.Thread.Sleep(500);
                }

                saveXMLTimer.Start();
                //GlobalVariables.XmlMovieDoc.Save(GlobalPath.MOVIE_DATABASE_PATH);
                refreshRatingThreadList.Clear();

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

        private void fetchRatingFromIMDB(int i)
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

                    //Check if parsed successfully. Rating is a field that always exists in IMDB.
                    if (String.IsNullOrWhiteSpace(imdb.Rating) == false )
                    {
                        Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                        {
                            string oldRating = GlobalVariables.MemoryDatabase[movieName].Rating;
                            GlobalVariables.MemoryDatabase[movieName].Rating = imdb.Rating;

                            //Refresh the entry in the list box
                            RefreshEntryInListBox(RefreshType.TEXT, i);

                            //Refresh the movie data if it's currently selected
                            if (i == movieLB.SelectedIndex)
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
            catch (Exception)
            {
            }
            finally
            {
                refreshThreadCount--;
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
