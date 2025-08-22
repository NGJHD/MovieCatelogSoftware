using System;
using System.Windows;
using System.Windows.Controls;
using IMDB_Scraper;

namespace MovieSelector
{
    public partial class EditUserControl : UserControl
    {
        private IMDB imdb = null;
        private MovieDataClass originalMovieStruct;
        private string movieName = "";
        private string newImageFilePath = "";
        private static string tempfilename = "temp";
        private static string tempfilepath = GlobalPath.MOVIE_POSTER_FOLDER_PATH + tempfilename + ".jpg";
        private string refreshedAndIMDBNoImage = "REFRESHED AND IMDB NO IMAGE";

        public EditUserControl()
        {
            InitializeComponent();
        }

        public void Show()
        {
            try
            {
                this.Visibility = Visibility.Visible;
                errMsgTextBlock.Visibility = Visibility.Collapsed;
                imdb = null;
                waitGrid.Visibility = Visibility.Hidden;
                newImageFilePath = "";

                string posterPath = GlobalPath.MOVIE_POSTER_FOLDER_PATH + movieName + ".jpg";
                if (System.IO.File.Exists(posterPath) == true)
                {
                    posterImage.Source = HelperClass.BitmapImageFromFile(GlobalPath.MOVIE_POSTER_FOLDER_PATH + movieName + ".jpg");
                }
                else
                {
                    posterImage.Source = null;
                }

                if (System.IO.File.Exists(tempfilepath) == true)
                {
                    System.IO.File.Delete(tempfilepath);
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        public void SetData(string movieName, MovieDataClass movieDataObj=null)
        {
            try
            {
                this.movieName = movieName;

                if (movieDataObj == null)
                {
                    movieDataObj = new MovieDataClass();
                }

                originalMovieStruct = movieDataObj;

                //Set the fields            
                titleTextBox.Text = movieName;
                imdbIDTextBox.Text = movieDataObj.ImdbID;
                taglineTextBox.Text = movieDataObj.Tagline;
                ratingTextBox.Text = movieDataObj.Rating;
                plotTextBox.Text = movieDataObj.Plot;
                genreTextBox.Text = movieDataObj.Genre;
                directorTextBox.Text = movieDataObj.Director;
                castTextBox.Text = movieDataObj.Cast;
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void RefreshGrid_Click(object sender, EventArgs e)
        {
            try
            {
                waitGrid.Visibility = Visibility.Visible;
                string imdbID = imdbIDTextBox.Text;
                string movieName = this.movieName;

                new System.Threading.Thread(() =>
                {
                    System.Threading.Thread.CurrentThread.IsBackground = true;

                    //Fetch from imdb                        
                    imdb = new IMDB(@"https://www.imdb.com/title/" + imdbID, true);

                    Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                    {
                        if (this.movieName == movieName) //Still at this overlay
                        {
                            //Check if parsed successfully. Rating is a field that always exists in IMDB.
                            if (String.IsNullOrWhiteSpace(imdb.Rating) == true)
                            {
                                errMsgTextBlock.Visibility = Visibility.Visible; //Means parse failed
                            }
                            else // Success. Update the Edit UI.
                            {
                                taglineTextBox.Text = imdb.Tagline;
                                ratingTextBox.Text = imdb.Rating;
                                plotTextBox.Text = imdb.Plot;
                                genreTextBox.Text = imdb.Genre;
                                directorTextBox.Text = imdb.Director;
                                castTextBox.Text = imdb.Cast;
                            }

                            waitGrid.Visibility = Visibility.Hidden;
                        }
                    }));

                    GlobalVariables.MainWindow.DownloadPoster(imdb.PosterLarge, tempfilename);
                    if (System.IO.File.Exists(tempfilepath) == true)
                    {
                        Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                        {
                            if (this.movieName == movieName) //Still at this overlay
                            {
                                posterImage.Source = HelperClass.BitmapImageFromFile(tempfilepath);
                                newImageFilePath = tempfilepath;
                            }
                        }));
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                        {
                            if (this.movieName == movieName) //Still at this overlay
                            {
                                posterImage.Source = null;
                                newImageFilePath = refreshedAndIMDBNoImage;
                            }
                        }));
                    }
                }).Start();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void cancelGrid_Click(object sender, EventArgs e)
        {
            try
            {
                this.Visibility = Visibility.Hidden;

                if (System.IO.File.Exists(tempfilepath) == true)
                {
                    System.IO.File.Delete(tempfilepath);
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private bool changesDetected()
        {
            try
            {
                if (imdbIDTextBox.Text != originalMovieStruct.ImdbID ||
                    taglineTextBox.Text != originalMovieStruct.Tagline ||
                    ratingTextBox.Text != originalMovieStruct.Rating ||
                    plotTextBox.Text != originalMovieStruct.Plot ||
                    genreTextBox.Text != originalMovieStruct.Genre ||
                    directorTextBox.Text != originalMovieStruct.Director ||
                    castTextBox.Text != originalMovieStruct.Cast)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }

            return false;
        }

        private void saveGrid_Click(object sender, EventArgs e)
        {
            try
            {
                if (changesDetected() == true)
                {
                    string movieName = this.movieName;

                    //Update Memory Database or create new entry if it doesn't exist
                    if (GlobalVariables.MemoryDatabase.ContainsKey(movieName) == true)
                    {
                        GlobalVariables.MemoryDatabase[movieName].ImdbID = imdbIDTextBox.Text;
                        GlobalVariables.MemoryDatabase[movieName].Tagline = taglineTextBox.Text;
                        GlobalVariables.MemoryDatabase[movieName].Rating = ratingTextBox.Text;
                        GlobalVariables.MemoryDatabase[movieName].Plot = plotTextBox.Text;
                        GlobalVariables.MemoryDatabase[movieName].Genre = genreTextBox.Text;
                        GlobalVariables.MemoryDatabase[movieName].Director = directorTextBox.Text;
                        GlobalVariables.MemoryDatabase[movieName].Cast = castTextBox.Text;

                        new System.Threading.Thread(() =>
                        {
                            System.Threading.Thread.CurrentThread.IsBackground = true;
                            GlobalVariables.MainWindow.UpdateEntryToFile(movieName, GlobalVariables.MemoryDatabase[movieName]);
                        }).Start();
                    }
                    else
                    {
                        MovieDataClass movieDataObj = new MovieDataClass();
                        movieDataObj.ImdbID = imdbIDTextBox.Text;
                        movieDataObj.Tagline = taglineTextBox.Text;
                        movieDataObj.Rating = ratingTextBox.Text;
                        movieDataObj.Plot = plotTextBox.Text;
                        movieDataObj.Genre = genreTextBox.Text;
                        movieDataObj.Director = directorTextBox.Text;
                        movieDataObj.Cast = castTextBox.Text;

                        new System.Threading.Thread(() =>
                        {
                            System.Threading.Thread.CurrentThread.IsBackground = true;
                            GlobalVariables.MemoryDatabase.Add(movieName, movieDataObj);
                            GlobalVariables.MainWindow.AppendEntryToFile(movieName, movieDataObj);
                            //GlobalVariables.XmlMovieDoc.Save(GlobalPath.MOVIE_DATABASE_PATH);
                        }).Start();
                    }

                    //refresh right side ui using the details from curmovielist                
                    GlobalVariables.MainWindow.DisplayMovieData(movieName);

                    //Update rating at the listbox
                    GlobalVariables.MainWindow.RefreshEntryInListBox(MainWindow.RefreshType.TEXT, GlobalVariables.MainWindow.movieLB.SelectedIndex);
                }

                if (newImageFilePath != "")
                {
                    new System.Threading.Thread(() =>
                    {
                        System.Threading.Thread.CurrentThread.IsBackground = true;
                        if (newImageFilePath == refreshedAndIMDBNoImage)
                        {
                            if (System.IO.File.Exists(GlobalPath.MOVIE_POSTER_FOLDER_PATH + movieName + ".jpg") == true)
                            {
                                System.IO.File.Delete(GlobalPath.MOVIE_POSTER_FOLDER_PATH + movieName + ".jpg");
                            }
                        }
                        else
                        {
                            System.IO.File.Copy(newImageFilePath, GlobalPath.MOVIE_POSTER_FOLDER_PATH + movieName + ".jpg", true);                            
                        }

                        Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                        {
                            GlobalVariables.MainWindow.RefreshEntryInListBox(MainWindow.RefreshType.IMAGE, GlobalVariables.MainWindow.movieLB.SelectedIndex);

                            if (GlobalVariables.MainWindow.titleTextBlock.Text == movieName)
                            {
                                GlobalVariables.MainWindow.DisplayMovieData(movieName);
                            }
                        }));
                    }).Start();
                }

                /*if (refreshTriggered == true)
                {
                    new System.Threading.Thread(() =>
                    {
                        GlobalVariables.MainWindow.DownloadPoster(imdb.PosterLarge, movieName);

                        //Load the image in the listbox
                        Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                        {
                            GlobalVariables.MainWindow.RefreshEntryInListBox(MainWindow.RefreshType.IMAGE, GlobalVariables.MainWindow.movieLB.SelectedIndex);

                            if (GlobalVariables.MainWindow.titleTextBlock.Text == movieName)
                            {
                                GlobalVariables.MainWindow.DisplayMovieData(movieName);
                            }
                        }));
                    }).Start();
                }*/

                //Hide this editing overlay
                this.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void changeImageGrid_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.DefaultExt = "jpg";
            ofd.Filter = "jpg (*.jpg)|";

            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filepath = ofd.FileName;
                posterImage.Source = HelperClass.BitmapImageFromFile(filepath);
                newImageFilePath = filepath;                
            }
        }
    }
}
