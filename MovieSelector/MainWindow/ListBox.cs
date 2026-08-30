using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Threading;

namespace MovieSelector
{    
    public partial class MainWindow : System.Windows.Window
    {
/*************************************************************************************************************************************/
        public enum SORT_CONDITION
        {
            NAME,
            LAST_MODIFIED, 
            YEAR_RELEASED,
            RATING
        }

        //Data Structure        
        private List<FileInfo> listOfFiles = new List<FileInfo>();
        private List<string> deniedAccessList = new List<string>();
        private SORT_CONDITION sortBy = SORT_CONDITION.NAME;
        private bool needScrapper = false;
        private bool needRatingScrapper = false;
        private int maxImageLoadingThreadCount = 16;
        private int currentImageLoadingThreadCount = 0;
/*************************************************************************************************************************************/
        private void refreshList()
        {
            try
            {
                cancelAllRunningWork();
                needScrapper = false;
                movieLB.Items.Clear();
                listOfFiles.Clear();

                //Get the files's FileInfo
                foreach (string dirPath in GlobalPath.MOVIE_LOCATION_LIST)
                {
                    getAllFiles(new DirectoryInfo(dirPath));
                }

                //Sort the files according to sort conditions
                List<FileInfo> sortedFiles = refreshList_SortListByCondition();

                //Put the files into the listbox
                refreshList_AddFilesToListBox(sortedFiles);

                //Select the first movie that's visible
                selectFirstVisibleMovie();
                
                //Capture the token here, so a thread that starts after the next cancel still sees it
                System.Threading.CancellationToken workToken = GlobalVariables.WorkToken;

                //Start image loading
                System.Threading.Thread imageLoadingThread = new Thread(() => imageLoadingFn(workToken));
                imageLoadingThread.IsBackground = true;
                imageLoadingThread.Start();

                //If data for a movie cannot be found, start the scrapper
                if ((needScrapper == true || needRatingScrapper == true) && IMDB_Scraper.IMDB.IsApiKeyConfigured == false)
                {
                    //Nothing can be fetched without a key, so ask for one instead of failing every movie
                    AddToNotification("No OMDb API key set. Add one under Options to fetch movie details.");
                    showOptions();
                }
                else
                {
                    if (needScrapper == true)
                    {
                        AddToNotification("Movies w/o data or rating detected. Fetching data from IMDB...");

                        System.Threading.Thread scrapperThread = new Thread(() => startScrapper(workToken));
                        scrapperThread.IsBackground = true;
                        scrapperThread.Start();
                    }
                    if (needRatingScrapper == true)
                    {
                        RefreshRatings(-1, true);
                    }
                }

                movieLB.Focus();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void getAllFiles(DirectoryInfo dir)
        {
            try
            {
                if (deniedAccessList.Contains(dir.FullName))
                {
                    return;
                }

                foreach (FileInfo fi in dir.GetFiles())
                {
                    listOfFiles.Add(fi);
                }

                foreach (DirectoryInfo di in dir.GetDirectories())
                {
                    getAllFiles(di);
                }
            }
            catch (Exception)
            {
                deniedAccessList.Add(dir.FullName);
            }
        }

        private List<FileInfo> refreshList_SortListByCondition()
        {
            List<FileInfo> sortedFiles = null;

            if (sortBy == SORT_CONDITION.NAME)
            {
                sortedFiles = listOfFiles.OrderBy(f => f.Name).ToList();
            }
            else if (sortBy == SORT_CONDITION.LAST_MODIFIED)
            {
                sortedFiles = listOfFiles.OrderByDescending(f => f.LastWriteTime).ToList();
            }
            else if (sortBy == SORT_CONDITION.YEAR_RELEASED)
            {
                sortedFiles = getSortedFilesByYearReleased(listOfFiles);
            }
            else if (sortBy == SORT_CONDITION.RATING)
            {
                sortedFiles = getSortedFilesByRating(listOfFiles);
            }

            return sortedFiles;
        }

        private void refreshList_AddFilesToListBox(List<FileInfo> sortedFiles)
        {
            try
            {
                for (int i = 0; i < sortedFiles.Count(); i++)
                {
                    if (HelperClass.IsMediaFile(sortedFiles[i].Name) == true)
                    {
                        string movieName = Path.GetFileNameWithoutExtension(sortedFiles[i].Name);
                        movieLB.Items.Add(new MovieListBoxClass(movieName, sortedFiles[i].FullName));

                        //Check whether the movie has data
                        if (needScrapper == false)
                        {
                            if (GlobalVariables.MemoryDatabase.ContainsKey(movieName) == false)
                            {
                                needScrapper = true;
                            }
                        }

                        //Check whether the movie has ratings
                        if (needRatingScrapper == false)
                        {
                            if (GlobalVariables.MemoryDatabase.ContainsKey(movieName) == true && GlobalVariables.MemoryDatabase[movieName].Rating == "?")
                            {
                                needRatingScrapper = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void selectFirstVisibleMovie()
        {
            try
            {
                if (movieLB.Items.Count > 0)
                {
                    movieLB.UpdateLayout();

                    int idx = 0;

                    try
                    {
                        for (int i = 0; i < movieLB.Items.Count; i++)
                        {
                            if ((movieLB.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem).Visibility == System.Windows.Visibility.Visible)
                            {
                                idx = i;
                                break;
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }

                    movieLB.SelectedIndex = idx;
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }        
/*************************************************************************************************************************************/        
        private List<FileInfo> getSortedFilesByYearReleased(List<FileInfo> listOfFiles)
        {
            List<FileInfo> sortedFiles = new List<FileInfo>();

            try
            {
                List<Tuple<int, FileInfo>> tempList = new List<Tuple<int, FileInfo>>();

                foreach (FileInfo fi in listOfFiles)
                {
                    tempList.Add(new Tuple<int, FileInfo>(getYear(fi), fi));
                }

                tempList.Sort((x, y) =>
                {
                    int result = y.Item1.CompareTo(x.Item1);
                    return result == 0 ? x.Item2.Name.CompareTo(y.Item2.Name) : result;
                });

                for (int i = 0; i < tempList.Count; i++)
                {
                    sortedFiles.Add(tempList[i].Item2);
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }

            return sortedFiles;
        }

        private int getYear(FileInfo fi)
        {
            try
            {
                if (fi.Name.IndexOf('(') == -1)
                {
                    return 0;
                }

                string[] strArray = fi.Name.Split('(');

                return Convert.ToInt32(strArray.ElementAt(strArray.Count() - 1).Split(')').ElementAt(0));
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }

            return 0;
        }

        private List<FileInfo> getSortedFilesByRating(List<FileInfo> listOfFiles)
        {
            List<FileInfo> sortedFiles = new List<FileInfo>();
            List<Tuple<double, FileInfo>> tempList = new List<Tuple<double, FileInfo>>();

            foreach (FileInfo fi in listOfFiles)
            {
                tempList.Add(new Tuple<double, FileInfo>(getRating(fi), fi));
            }

            tempList.Sort((x, y) =>
            {
                int result = y.Item1.CompareTo(x.Item1);
                return result == 0 ? x.Item2.Name.CompareTo(y.Item2.Name) : result;
            });

            for (int i = 0; i < tempList.Count; i++)
            {
                sortedFiles.Add(tempList[i].Item2);
            }

            return sortedFiles;
        }

        private double getRating(FileInfo fi)
        {
            try
            {
                string movieName = System.IO.Path.GetFileNameWithoutExtension(fi.Name);
                if (GlobalVariables.MemoryDatabase.ContainsKey(movieName) == false)
                {
                    return 0;
                }

                return Convert.ToDouble(GlobalVariables.MemoryDatabase[movieName].Rating);
            }
            catch (Exception)
            {
                return -1;
            }
        }
/*************************************************************************************************************************************/        
        private void imageLoadingFn(System.Threading.CancellationToken token)
        {
            try
            {
                int totalCount = Invoke_GetTotalMovieCount();

                for (int i = 0; i < totalCount; i++)
                {
                    if (token.IsCancellationRequested == true)
                    {
                        return;
                    }

                    currentImageLoadingThreadCount++;
                    while (currentImageLoadingThreadCount > maxImageLoadingThreadCount)
                    {
                        if (GlobalVariables.SleepUnlessCancelled(token, 250) == false)
                        {
                            return;
                        }
                    }

                    Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                    {
                        RefreshEntryInListBox(RefreshType.IMAGE, i);
                    }));
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        public int Invoke_GetTotalMovieCount()
        {
            int totalCount = 0;

            Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    totalCount = movieLB.Items.Count;
                }
                catch (Exception ex)
                {
                    Log.Write(Log.LogMsgType.I, ex.Message);
                }
            }));

            return totalCount;
        }
/*************************************************************************************************************************************/
        private void sortByCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                bool needRefresh = false;

                if (sortByCB.SelectedIndex == 0)
                {
                    if (sortBy != SORT_CONDITION.NAME)
                    {
                        sortBy = SORT_CONDITION.NAME;
                        needRefresh = true;
                    }
                }
                else if (sortByCB.SelectedIndex == 1)
                {
                    if (sortBy != SORT_CONDITION.LAST_MODIFIED)
                    {
                        sortBy = SORT_CONDITION.LAST_MODIFIED;
                        needRefresh = true;
                    }
                }
                else if (sortByCB.SelectedIndex == 2)
                {
                    if (sortBy != SORT_CONDITION.YEAR_RELEASED)
                    {
                        sortBy = SORT_CONDITION.YEAR_RELEASED;
                        needRefresh = true;
                    }
                }
                else if (sortByCB.SelectedIndex == 3)
                {
                    if (sortBy != SORT_CONDITION.RATING)
                    {
                        sortBy = SORT_CONDITION.RATING;
                        needRefresh = true;
                    }
                }

                if (needRefresh == true && startup == false)
                {
                    loadSettings();
                    refreshList();
                }

                movieLB.Focus();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }
/*************************************************************************************************************************************/        
        private void movieLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (movieLB.SelectedIndex == -1)
                {
                    putErrorFromIMDB("-");
                    return;
                }
                 
                string movieName = (movieLB.SelectedItem as MovieListBoxClass).movieName;

                Thread displayMovieDataThread = new Thread(() => DisplayMovieData(movieName));
                displayMovieDataThread.IsBackground = true;
                displayMovieDataThread.Start();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        public void DisplayMovieData(string movieName)
        {            
            try
            {
                //Retrieve movie from data structure
                if (GlobalVariables.MemoryDatabase.ContainsKey(movieName) == true && GlobalVariables.MemoryDatabase[movieName].Plot != GlobalVariables.ErrorIMDB)
                {
                    MovieDataClass movieDataObj = GlobalVariables.MemoryDatabase[movieName];

                    //All good. Movie exists in IMDB and in data structure
                    Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                    {
                        try
                        {
                            titleTextBlock.Text = movieName;
                            ratingTextBlock.Text = movieDataObj.Rating + @"/10";
                            taglineTextBlock.Text = movieDataObj.Tagline;
                            plotTextBlock.Text = movieDataObj.Plot;

                            editUserControl.SetData(movieName, movieDataObj);

                            if (File.Exists(GlobalPath.MOVIE_POSTER_FOLDER_PATH + movieName + ".jpg"))
                            {
                                posterImage.Source = HelperClass.BitmapImageFromFile(GlobalPath.MOVIE_POSTER_FOLDER_PATH + movieName + ".jpg");
                            }
                            else
                            {
                                posterImage.Source = null;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Write(Log.LogMsgType.I, ex.Message);
                        }
                    }));
                }
                else
                {
                    throw new Exception();                    
                }                
            }
            catch (Exception)
            {
                //Movie does not exist in memory database
                Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                {
                    putErrorFromIMDB(movieName);
                    posterImage.Source = null;
                    editUserControl.SetData(movieName);                    
                }));
            }
        }

        private void putErrorFromIMDB(string movieName)
        {
            try
            {
                titleTextBlock.Text = movieName;
                ratingTextBlock.Text = @"-/10";
                taglineTextBlock.Text = "-";
                posterImage.Source = null;
                plotTextBlock.Text = (movieName == "-" ? "-" : (GlobalVariables.FailedToGetFromIMDBLIST.Contains(movieName) == true ? "Failed to retrieve data from IMDB" : "Fetching " + movieName + "'s data from IMDB..."));
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }
/*************************************************************************************************************************************/
    }
}
