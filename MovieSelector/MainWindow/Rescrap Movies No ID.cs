using System;
using IMDB_Scraper;
using System.Windows;
using System.Collections.Generic;

namespace MovieSelector
{
    public partial class MainWindow : System.Windows.Window
    {
        //Cancels a previous re-fetch without disturbing the other background work.
        private System.Threading.CancellationTokenSource fetchMovieNoIDCancellationSource = new System.Threading.CancellationTokenSource();
        private int fetchMovieNoIDThreadCount = 0;

        private void RescrapMoviesWithNoIDGrid_Click(object sender, EventArgs e)
        {
            try
            {
                AddToNotification("Starting fetching for movie details for those with no ID");

                //Get the total number of movies in order to iterate without touching the UI thread 
                int totalCount = movieLB.Items.Count;

                //Stop the previous re-fetch before starting another
                fetchMovieNoIDCancellationSource.Cancel();
                fetchMovieNoIDCancellationSource = new System.Threading.CancellationTokenSource();

                //Capture both tokens now: the feature one, and the global one used on close/list refresh
                System.Threading.CancellationToken fetchToken = fetchMovieNoIDCancellationSource.Token;
                System.Threading.CancellationToken workToken = GlobalVariables.WorkToken;

                //Hide the Options Grid and other UI
                hideOptions();
                lockCBGrid.Visibility = Visibility.Visible;
                moviesWithNoIDOptionGrid.Visibility = Visibility.Collapsed;
                moviesWithNoIDOptionBorder.Visibility = Visibility.Collapsed;

                //Start the scrapper
                System.Threading.Thread fetchMovieNoIDThread = new System.Threading.Thread(() => fetchMovieNoIDFn(totalCount, fetchToken, workToken));
                fetchMovieNoIDThread.IsBackground = true;
                fetchMovieNoIDThread.Start();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private static bool isFetchCancelled(System.Threading.CancellationToken fetchToken, System.Threading.CancellationToken workToken)
        {
            return fetchToken.IsCancellationRequested || workToken.IsCancellationRequested;
        }

        //Returns false once the caller should stop.
        private static bool sleepUnlessFetchCancelled(System.Threading.CancellationToken fetchToken, System.Threading.CancellationToken workToken, int milliseconds)
        {
            return System.Threading.WaitHandle.WaitAny(
                       new System.Threading.WaitHandle[] { fetchToken.WaitHandle, workToken.WaitHandle },
                       milliseconds) == System.Threading.WaitHandle.WaitTimeout;
        }

        private void fetchMovieNoIDFn(int totalCount, System.Threading.CancellationToken fetchToken, System.Threading.CancellationToken workToken)
        {
            try
            {
                //Loop through all the movies
                for (int i = 0; i < totalCount; i++)
                {
                    if (isFetchCancelled(fetchToken, workToken) == true)
                    {
                        return;
                    }

                    //Max of 5 threads for stability
                    while (fetchMovieNoIDThreadCount >= 5)
                    {
                        if (sleepUnlessFetchCancelled(fetchToken, workToken, 500) == false)
                        {
                            return;
                        }
                    }

                    //Add to the thread count immediately when entering
                    System.Threading.Interlocked.Increment(ref fetchMovieNoIDThreadCount);

                    string movieName = "";

                    Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                    {
                        movieName = (movieLB.Items[i] as MovieListBoxClass).movieName;
                    }));

                    if (GlobalVariables.MemoryDatabase.ContainsKey(movieName) == true && GlobalVariables.MemoryDatabase[movieName].ImdbID == "")
                    {
                        int currIdx = i;

                        System.Threading.Thread fetchDetailsThread = new System.Threading.Thread(() => fetchDetailsFromIMDB(movieName, currIdx, fetchToken, workToken));
                        fetchDetailsThread.IsBackground = true;
                        fetchDetailsThread.Start();
                    }
                    else
                    {
                        System.Threading.Interlocked.Decrement(ref fetchMovieNoIDThreadCount);
                    }
                }

                while (fetchMovieNoIDThreadCount > 0)
                {
                    if (sleepUnlessFetchCancelled(fetchToken, workToken, 500) == false)
                    {
                        return;
                    }
                }

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

        private void fetchDetailsFromIMDB(string movieName, int i, System.Threading.CancellationToken fetchToken, System.Threading.CancellationToken workToken)
        {
            try
            {
                //Fetch from omdb
                IMDB imdb = new IMDB(movieName);

                if (isFetchCancelled(fetchToken, workToken) == true)
                {
                    return;
                }

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
                        RefreshEntryInListBox(RefreshType.TEXT, movieName);

                        //Redisplay the movie data if it's currently selected
                        if (IsMovieSelected(movieName) == true)
                        {
                            DisplayMovieData(movieName);
                        }

                        //Inform user
                        AddToNotification("Re-fetched " + movieName + "'s details from omdb");
                    }));

                    GlobalVariables.MainWindow.UpdateEntryToFile(movieName, GlobalVariables.MemoryDatabase[movieName]);
                }
            }
            catch (Exception ex)
            {
                ReportOmdbProblem(ex);
            }
            finally
            {
                System.Threading.Interlocked.Decrement(ref fetchMovieNoIDThreadCount);
            }
        }
/*************************************************************************************************************************************/
    }
}
