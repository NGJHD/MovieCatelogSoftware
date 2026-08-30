using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace MovieSelector
{    
    public partial class MainWindow : System.Windows.Window
    {
        private bool startup = true;
/*************************************************************************************************************************************/        
        public MainWindow()
        {
            GlobalPath.LOG_FILENAME = "MCS_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

            InitializeComponent();
            GlobalPath.CheckDirectory();
            GlobalVariables.MainWindow = this;

            autoSelectSelectBoxFirstItemTimer.Interval = TimeSpan.FromMilliseconds(500);
            autoSelectSelectBoxFirstItemTimer.Tick += new EventHandler(autoSelectSelectBoxFirstItemTimer_Tick);

            hideOptionsGridBGAnimation.Completed += (o, e) => optionsGridBG.Visibility = System.Windows.Visibility.Collapsed;
            hideOptionsGridAnimation.Completed += (o, e) => optionsGrid.Visibility = System.Windows.Visibility.Hidden;

            saveXMLTimer.Elapsed += SaveXMLTimer_Elapsed;
        }

        private void MovieSelectorWindow_ContentRendered(object sender, EventArgs e)
        {
            //Set the correct aspect ratio to the current screen
            applicationGrid.Width = 1920;
            applicationGrid.Height = 1920 / this.ActualWidth * this.ActualHeight - titleBarGrid.Height; //Taskbar already deducted from actual height.

            //Set the minimum height
            this.MinHeight = 800 / this.ActualWidth * this.ActualHeight - titleBarGrid.Height;

            sortByCB.AddItem("Title");
            sortByCB.AddItem("Last Added");
            sortByCB.AddItem("Year Released");
            sortByCB.AddItem("Rating");
            sortByCB.SelectedIndex = 1;

            //Start everything
            Thread loadThread = new Thread(loadSequence);
            loadThread.IsBackground = true;
            loadThread.Start();
        }
/*************************************************************************************************************************************/
        private void MovieSelectorWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            cancelAllRunningWork();
        }

        private void cancelAllRunningWork()
        {
            try
            {
                GlobalVariables.CancelAllWork();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void loadSequence()
        {
            try
            {
                //Check the config files
                checkConfigFiles();

                //Load the current movies from the folder into the data structure (NOT THE UI)
                loadDBIntoMemory();

                System.Threading.Thread.Sleep(400);                

                Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                {
                    try
                    {
                        //Load the movie loc
                        loadSettings();

                        //Update the UI list
                        refreshList();

                        //Show the re-fetch option in option
                        if (moviesWithNoIDDetected == true)
                        {
                            moviesWithNoIDOptionGrid.Visibility = Visibility.Visible;
                            moviesWithNoIDOptionBorder.Visibility = Visibility.Visible;
                        }

                        //Turn off the loading Grid
                        loadingGrid.Visibility = System.Windows.Visibility.Collapsed;

                        //Turn off the flag
                        startup = false;

                        //Add to the notification center
                        AddToNotification("MCS Startup Sequence Completed");                        
                    }
                    catch (Exception ex)
                    {
                        Log.Write(Log.LogMsgType.I, ex.Message);
                    }
                }));
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }        
/*************************************************************************************************************************************/
        private void playBtn_Click(object sender, EventArgs e)
        {
            try
            {
                Process mediaPlayer = new Process();
                mediaPlayer.StartInfo.FileName = (movieLB.SelectedItem as MovieListBoxClass).movieLoc;                
                mediaPlayer.Start();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void SLBI_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ClickCount > 1)
                {
                    playBtn_Click(null, null);
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void SLBI_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                editUserControl.Show();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void movieLB_KeyDown(object sender, KeyEventArgs e)
        {
            ListBox list = sender as ListBox;

            if (e.Key == Key.Right)
            {
                if (!list.Items.MoveCurrentToNext()) 
                {
                    list.Items.MoveCurrentToLast();
                }

                var listBoxItem = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem);
                listBoxItem.Focus();
            }
            else if (e.Key == Key.Left)
            {
                if (!list.Items.MoveCurrentToPrevious())
                {
                    list.Items.MoveCurrentToFirst();
                }

                var listBoxItem = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem);
                listBoxItem.Focus();
            }
            else
            {
                if (e.Key == Key.Delete && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
                {
                    try
                    {
                        string fileLoc = (movieLB.SelectedItem as MovieListBoxClass).movieLoc;

                        list.Items.RemoveAt(list.SelectedIndex);

                        File.Delete(fileLoc);

                        if (!list.Items.MoveCurrentToNext())
                        {
                            list.Items.MoveCurrentToLast();
                        }

                        var listBoxItem = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem);
                        listBoxItem.Focus();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private void EditGrid_Click(object sender, EventArgs e)
        {
            try
            {
                editUserControl.Show();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }
/*************************************************************************************************************************************/
        public void AddToNotification(string text)
        {
            try
            {
                notificationUserControl.Show(text);
                notificationTB.Text = "[" + DateTime.Now.ToString("HH:mm") + "] " + text + "\n" + notificationTB.Text;
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }
/*************************************************************************************************************************************/
    }
}
