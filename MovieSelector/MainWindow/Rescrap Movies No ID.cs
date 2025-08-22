using System;
using IMDB_Scraper;
using System.Windows;
using System.Collections.Generic;

namespace MovieSelector
{
    public partial class MainWindow : System.Windows.Window
    {
        private List<System.Threading.Thread> fetchMovieNoIDThreadList = new List<System.Threading.Thread>();
        private int fetchMovieNoIDThreadCount = 0;

        private void RescrapMoviesWithNoIDGrid_Click(object sender, EventArgs e)
        {
            try
            {
                AddToNotification("Starting fetching for movie details for those with no ID");

                //Get the total number of movies in order to iterate without touching the UI thread 
                int totalCount = movieLB.Items.Count;

                foreach (System.Threading.Thread th in fetchMovieNoIDThreadList)
                {
                    if (th != null && th.IsAlive == true)
                    {
                        th.Abort();
                    }
                }
                fetchMovieNoIDThreadList.Clear();
                fetchMovieNoIDThreadCount = 0;

                //Hide the Options Grid and other UI
                hideOptions();
                lockCBGrid.Visibility = Visibility.Visible;
                moviesWithNoIDOptionGrid.Visibility = Visibility.Collapsed;
                moviesWithNoIDOptionBorder.Visibility = Visibility.Collapsed;

                //Start the scrapper
                System.Threading.Thread fetchMovieNoIDThread = new System.Threading.Thread(() => fetchMovieNoIDFn(totalCount));
                fetchMovieNoIDThread.IsBackground = true;
                fetchMovieNoIDThread.Start();
                GlobalVariables.ListOfRunningThreads.Add(fetchMovieNoIDThread);
                fetchMovieNoIDThreadList.Add(fetchMovieNoIDThread);
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void fetchMovieNoIDFn(int totalCount)
        {
            try
            {
                //Loop through all the movies
                for (int i = 0; i < totalCount; i++)
                {
                    //Max of 5 threads for stability
                    while (fetchMovieNoIDThreadCount >= 5)
                    {
                        System.Threading.Thread.Sleep(500);
                    }

                    //Add to the thread count immediately when entering
                    fetchMovieNoIDThreadCount++;

                    string movieName = "";

                    Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                    {
                        movieName = (movieLB.Items[i] as MovieListBoxClass).movieName;
                    }));

                    if (GlobalVariables.MemoryDatabase.ContainsKey(movieName) == true && GlobalVariables.MemoryDatabase[movieName].ImdbID == "")
                    {
                        int currIdx = i;

                        System.Threading.Thread fetchDetailsThread = new System.Threading.Thread(() => fetchDetailsFromIMDB(movieName, currIdx));
                        fetchDetailsThread.IsBackground = true;
                        fetchDetailsThread.Start();
                        GlobalVariables.ListOfRunningThreads.Add(fetchDetailsThread);
                        fetchMovieNoIDThreadList.Add(fetchDetailsThread);
                    }
                    else
                    {
                        fetchMovieNoIDThreadCount--;
                    }
                }

                while (fetchMovieNoIDThreadCount > 0)
                {
                    System.Threading.Thread.Sleep(500);
                }

                fetchMovieNoIDThreadList.Clear();

                Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                {
                    lockCBGrid.Visibility = Visibility.Hidden;
                    AddToNotification("Re-fetching of movie details without ID completed");
                }));
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void fetchDetailsFromIMDB(string movieName, int i)
        {
            try
            {
                //Fetch from imdb                        
                IMDB imdb = new IMDB(movieName);

                //Check if parsed successfully. Rating is a field that always exists in IMDB.
                if (String.IsNullOrWhiteSpace(imdb.Rating) == false)
                {
                    GlobalVariables.MemoryDatabase[movieName].ImdbID = imdb.Id;
                    GlobalVariables.MemoryDatabase[movieName].Tagline = imdb.Tagline;
                    GlobalVariables.MemoryDatabase[movieName].Rating = imdb.Rating;
                    GlobalVariables.MemoryDatabase[movieName].Plot = imdb.Plot;
                    GlobalVariables.MemoryDatabase[movieName].Genre = imdb.Genre;
                    GlobalVariables.MemoryDatabase[movieName].Director = imdb.Director;
                    GlobalVariables.MemoryDatabase[movieName].Cast = imdb.Cast;

                    Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                    {
                        //Refresh the small entry in the listbox
                        RefreshEntryInListBox(RefreshType.TEXT, i);

                        //Redisplay the movie data if it's currently selected
                        if (i == movieLB.SelectedIndex)
                        {
                            DisplayMovieData(movieName);
                        }

                        //Inform user
                        AddToNotification("Re-fetched " + movieName + "'s details from imdb");
                    }));

                    GlobalVariables.MainWindow.UpdateEntryToFile(movieName, GlobalVariables.MemoryDatabase[movieName]);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                fetchMovieNoIDThreadCount--;
            }
        }
/*************************************************************************************************************************************/
    }
}
