using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace MovieSelector
{
    class ClickGrid : Grid
    {
/**************************************************************************************************/
        private System.Timers.Timer clickTimer = new System.Timers.Timer(300);
        private bool isAClick = false;
/**************************************************************************************************/
        public ClickGrid()
        {            
            clickTimer.Elapsed += clickTimer_Elapsed;

            this.MouseDown += ClickGrid_InputDown;
            this.MouseUp += ClickGrid_InputUp;
        }

        private void clickTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            isAClick = false;
            clickTimer.Stop();
        }
/**************************************************************************************************/
        private void ClickGrid_InputDown(object sender, InputEventArgs e)
        {
            isAClick = true;
            e.Handled = true;

            clickTimer.Start();            
        }

        private void ClickGrid_InputUp(object sender, InputEventArgs e)
        {
            clickTimer.Stop();

            if (isAClick == true)
            {
                isAClick = false;
                OnClick();                
            }
        }
/**************************************************************************************************/
        public event EventHandler Click;

        protected virtual void OnClick()
        {
            EventHandler handler = Click;
            if (handler != null)
            {
                handler(this, null);
            }
        }
/**************************************************************************************************/
    }
}
