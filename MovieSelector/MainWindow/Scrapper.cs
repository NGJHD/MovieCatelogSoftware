using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;
using System.Xml;
using IMDB_Scraper;
using System.Threading;

namespace MovieSelector
{
    public partial class MainWindow : System.Windows.Window
    {        
        public enum RefreshType
        {
            TEXT,
            IMAGE,
            TEXT_AND_IMAGE,
        }

        private int threadCount = 0;

        //Only worth saying once per run, not once per movie.
        private bool omdbProblemReported = false;

        //Surface an OMDb refusal - a spent quota or a bad key - which otherwise looks
        //identical to "this movie was not found".
        public void ReportOmdbProblem(Exception ex)
        {
            IMDB_Scraper.OmdbApiException omdbError = ex as IMDB_Scraper.OmdbApiException;

            if (omdbError == null || omdbProblemReported == true)
            {
                return;
            }

            omdbProblemReported = true;

            Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                AddToNotification("OMDb: " + omdbError.Message + " Enter your own key under Options.");
            }));
        }
/*************************************************************************************************************************************/
        private void startScrapper(System.Threading.CancellationToken token)
        {
            try
            {
                GlobalVariables.ClearFailedToGetFromIMDB();
                omdbProblemReported = false;

                //Get the count of total number of movies so we know how many times to loop
                int totalCount = Invoke_GetTotalMovieCount();

                //Loop through all the movies
                for (int i = 0; i < totalCount; i++)
                {
                    if (token.IsCancellationRequested == true)
                    {
                        return;
                    }

                    //Max of 5 threads for stability
                    while (threadCount >= 5)
                    {
                        if (GlobalVariables.SleepUnlessCancelled(token, 500) == false)
                        {
                            return;
                        }
                    }

                    //Add to the thread count immediately when entering
                    System.Threading.Interlocked.Increment(ref threadCount);

                    try
                    {
                        string movieName = "";
                        bool getNameError = false;

                        Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                        {
                            try
                            {
                                movieName = (movieLB.Items[i] as MovieListBoxClass).movieName;
                            }
                            catch (Exception)
                            {
                                getNameError = true;
                            }
                        }));

                        //Get the info from OMDB if it doesn't exist
                        if (getNameError == false && GlobalVariables.MemoryDatabase.ContainsKey(movieName) == false)
                        {
                            //Assign a local variable first, otherwise i will ++ before creating a new thread
                            int currIdx = i;

                            //Start the scrapper details thread
                            System.Threading.Thread grabDetailsFromIMDBThread = new Thread(() => getInfoFromIMDB(movieName, currIdx, token));
                            grabDetailsFromIMDBThread.IsBackground = true;
                            grabDetailsFromIMDBThread.Start();
                        }
                        else
                        {
                            System.Threading.Interlocked.Decrement(ref threadCount);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }

                while (threadCount > 0)
                {
                    if (GlobalVariables.SleepUnlessCancelled(token, 500) == false)
                    {
                        return;
                    }
                }

                //GlobalVariables.XmlMovieDoc.Save(GlobalPath.MOVIE_DATABASE_PATH);

                //Scrapper ended, add to notiifcation
                Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                {
                    AddToNotification("Fetching data from OMDB completed for all movies.");
                }));
            }
            catch (Exception)
            {
            }
        }

        private void getInfoFromIMDB(string movieName, int i, System.Threading.CancellationToken token)
        {
            bool fail = false;

            try
            {
                //Get info from OMDB
                IMDB imdb = new IMDB(movieName);

                if (token.IsCancellationRequested == true)
                {
                    return;
                }

                //If the rating is null, means the movie does not exist in IMDB! Rating is always available
                if (String.IsNullOrWhiteSpace(imdb.Rating) == false)
                {
                    MovieDataClass movieDataObj = new MovieDataClass();
                    movieDataObj.ImdbID = imdb.Id;
                    movieDataObj.Tagline = imdb.Tagline;
                    movieDataObj.ImageURL = imdb.PosterLarge;
                    movieDataObj.Rating = imdb.Rating;
                    movieDataObj.Plot = imdb.Plot;
                    movieDataObj.Genre = imdb.Genre;
                    movieDataObj.Director = imdb.Director;
                    movieDataObj.Cast = imdb.Cast;

                    //Add to dictionary               
                    GlobalVariables.MemoryDatabase.Add(movieName, movieDataObj);

                    //Write to the database
                    AppendEntryToFile(movieName, movieDataObj);

                    //Try to get the poster
                    DownloadPoster(imdb.PosterLarge, movieName);
                }
                else
                {
                    throw new Exception();
                }                
            }
            catch (Exception ex)
            {
                ReportOmdbProblem(ex);
                GlobalVariables.AddFailedToGetFromIMDB(movieName);
                fail = true;
            }
            finally
            {
                if (token.IsCancellationRequested == false)
                {
                    Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                    {
                        //Refresh the movie data if it's currently selected
                        if (IsMovieSelected(movieName) == true)
                        {
                            DisplayMovieData(movieName);
                        }

                        //Refresh the small entry in the listbox
                        RefreshEntryInListBox(RefreshType.TEXT_AND_IMAGE, movieName);

                        //Inform the user
                        AddToNotification("Fetching " + movieName + "'s data from OMDB... " + (fail == false ? "SUCCESS" : "FAILED"));
                    }));
                }

                System.Threading.Interlocked.Decrement(ref threadCount);
            }
        }

        public void DownloadPoster(string url, string movieName)
        {
            try
            {
                var request = System.Net.WebRequest.Create(url);

                using (var response = request.GetResponse())
                using (var stream = response.GetResponseStream())
                {
                    System.Drawing.Image img = System.Drawing.Image.FromStream(stream);
                    img.Save(GlobalPath.MOVIE_POSTER_FOLDER_PATH + movieName + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
                }
            }
            catch (Exception)
            {
            }
        }

        //Index of a movie in the listbox as it is ordered right now, or -1 if it is gone.
        public int IndexOfMovie(string movieName)
        {
            for (int i = 0; i < movieLB.Items.Count; i++)
            {
                MovieListBoxClass entry = movieLB.Items[i] as MovieListBoxClass;

                if (entry != null && entry.movieName == movieName)
                {
                    return i;
                }
            }

            return -1;
        }

        public bool IsMovieSelected(string movieName)
        {
            MovieListBoxClass selected = movieLB.SelectedItem as MovieListBoxClass;
            return selected != null && selected.movieName == movieName;
        }

        //Resolve the position at the moment of the refresh, so a re-sort in between is harmless.
        public void RefreshEntryInListBox(RefreshType type, string movieName)
        {
            int i = IndexOfMovie(movieName);

            if (i >= 0)
            {
                RefreshEntryInListBox(type, i);
            }
        }

        public void RefreshEntryInListBox(RefreshType type, int i)
        {
            try
            {
                if (i < 0 || i >= movieLB.Items.Count)
                {
                    return;
                }

                string movieName = (movieLB.Items[i] as MovieListBoxClass).movieName;

                if (type == RefreshType.TEXT || type == RefreshType.TEXT_AND_IMAGE)
                {
                    (movieLB.Items[i] as MovieListBoxClass).movieName = "";
                    (movieLB.Items[i] as MovieListBoxClass).movieName = movieName;
                }

                if (type == RefreshType.IMAGE || type == RefreshType.TEXT_AND_IMAGE)
                {
                    new System.Threading.Thread(() =>
                    {
                        try
                        {
                            System.Threading.Thread.CurrentThread.IsBackground = true;

                            if (File.Exists(GlobalPath.MOVIE_POSTER_FOLDER_PATH + movieName + ".jpg") == true)
                            {
                                BitmapSource bs = HelperClass.BitmapImageFromFile(GlobalPath.MOVIE_POSTER_FOLDER_PATH + movieName + ".jpg");

                                Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                                {
                                    try
                                    {
                                        (movieLB.Items[i] as MovieListBoxClass).previewImage = bs;
                                    }
                                    catch (Exception)
                                    {
                                    }
                                }));
                            }
                            else
                            {
                                Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                                {
                                    try
                                    {
                                        (movieLB.Items[i] as MovieListBoxClass).previewImage = null;
                                    }
                                    catch (Exception)
                                    {
                                    }
                                }));
                            }
                        }
                        catch (Exception)
                        {
                        }
                        currentImageLoadingThreadCount--;
                    }).Start();
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
