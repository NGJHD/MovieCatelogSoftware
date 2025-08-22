using System;
using System.Windows.Controls;

namespace MovieSelector
{    
    public partial class MainWindow : System.Windows.Window
    {
/*************************************************************************************************************************************/
        private System.Windows.Threading.DispatcherTimer autoSelectSelectBoxFirstItemTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background);
/*************************************************************************************************************************************/
        private void filterTB_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                autoSelectSelectBoxFirstItemTimer.Start();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void autoSelectSelectBoxFirstItemTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                autoSelectSelectBoxFirstItemTimer.Stop();
                selectFirstVisibleMovie();
                movieLB.ScrollIntoView(movieLB.SelectedItem);
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }
/*************************************************************************************************************************************/
    }
}
