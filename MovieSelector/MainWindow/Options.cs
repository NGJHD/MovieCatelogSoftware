using System;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;

namespace MovieSelector
{    
    public partial class MainWindow : System.Windows.Window
    {
/*************************************************************************************************************************************/
        private static Duration optionsAnimationDuration = new Duration(TimeSpan.FromMilliseconds(250));
        private System.Windows.Media.Animation.DoubleAnimation showOptionsGridBGAnimation = new System.Windows.Media.Animation.DoubleAnimation(0, 0.7, optionsAnimationDuration);
        private System.Windows.Media.Animation.DoubleAnimation showOptionsGridAnimation = new System.Windows.Media.Animation.DoubleAnimation(-600, 0, optionsAnimationDuration);

        private System.Windows.Media.Animation.DoubleAnimation hideOptionsGridBGAnimation = new System.Windows.Media.Animation.DoubleAnimation(0, optionsAnimationDuration);
        private System.Windows.Media.Animation.DoubleAnimation hideOptionsGridAnimation = new System.Windows.Media.Animation.DoubleAnimation(-600, optionsAnimationDuration);
/*************************************************************************************************************************************/
        private void optionBtn_Click(object sender, EventArgs e)
        {
            try
            {
                showOptions();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        //The OMDb key lives in Database\Gui_Options.xml next to the movie locations, so it
        //travels with the install folder and survives a version bump. Never in the repo.
        //Empty means fall back to the shared default key built into the app.
        private string storedApiKey = "";

        private void applyApiKey()
        {
            IMDB_Scraper.IMDB.ApiKey = String.IsNullOrWhiteSpace(storedApiKey)
                                       ? IMDB_Scraper.IMDB.DefaultApiKey
                                       : storedApiKey;
        }

        private void saveApiKeyGrid_Click(object sender, EventArgs e)
        {
            try
            {
                storedApiKey = apiKeyTB.Text.Trim();

                applyApiKey();
                recreateConfigFile();

                AddToNotification(String.IsNullOrWhiteSpace(storedApiKey)
                                  ? "Using the shared default OMDb key. It is shared by everyone who has not set their own, so it runs out - get a free key at omdbapi.com."
                                  : "OMDb API key saved.");
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void showOptions()
        {
            try
            {
                optionsGridBG.Visibility = System.Windows.Visibility.Visible;
                optionsGridBG.BeginAnimation(Grid.OpacityProperty, showOptionsGridBGAnimation);

                optionsGrid.Visibility = System.Windows.Visibility.Visible;
                optionsGridTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, showOptionsGridAnimation);
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void optionsGridBG_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                hideOptions();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void hideOptions()
        {
            try
            {
                optionsGridBG.BeginAnimation(Grid.OpacityProperty, hideOptionsGridBGAnimation);
                optionsGridTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, hideOptionsGridAnimation);

            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }        
/*************************************************************************************************************************************/
        private void addBtn_Click(object sender, EventArgs e)
        {
            try
            {
                System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();

                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string dirPath = fbd.SelectedPath;

                    System.IO.DirectoryInfo d = new System.IO.DirectoryInfo(fbd.SelectedPath);
                    if (d.Parent != null)
                    {
                        dirPath += "\\";
                    }

                    if (movieLocLB.Items.IsEmpty == true) // Clear the default
                    {
                        GlobalPath.MOVIE_LOCATION_LIST.Clear();
                    }

                    GlobalPath.MOVIE_LOCATION_LIST.Add(dirPath);

                    updateMovieLocations();
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void removeBtn_Click(object sender, EventArgs e)
        {
            try
            {
                int index = movieLocLB.SelectedIndex;

                GlobalPath.MOVIE_LOCATION_LIST.RemoveAt(movieLocLB.SelectedIndex);
                movieLocLB.Items.RemoveAt(movieLocLB.SelectedIndex);

                if (index < movieLocLB.Items.Count)
                {
                    movieLocLB.SelectedIndex = index;
                }
                else if (movieLocLB.Items.Count != 0)
                {
                    movieLocLB.SelectedIndex = movieLocLB.Items.Count - 1;
                }

                updateMovieLocations();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void updateMovieLocations()
        {
            try
            {
                //Write the new location into the config file
                recreateConfigFile();

                //Load the config file again
                loadSettings();

                //Refresh the UI
                refreshList();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }
/*************************************************************************************************************************************/
    }
}
