using System;
using System.Windows;
using System.Windows.Controls;

namespace MovieSelector
{
    public partial class NotificationUserControl : UserControl
    {
        private System.Windows.Threading.DispatcherTimer showTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background);

        private static Duration duration = new Duration(TimeSpan.FromMilliseconds(300));
        private static System.Windows.Media.Animation.DoubleAnimation fadeInAnimation = new System.Windows.Media.Animation.DoubleAnimation(1, duration);
        private static System.Windows.Media.Animation.DoubleAnimation fadeOutAnimation = new System.Windows.Media.Animation.DoubleAnimation(0, duration);

        private static System.Windows.Media.Animation.DoubleAnimation translateInAnimation = new System.Windows.Media.Animation.DoubleAnimation(0, duration);
        private static System.Windows.Media.Animation.DoubleAnimation translateOutAnimation = new System.Windows.Media.Animation.DoubleAnimation(-100, duration);

        private static System.Windows.Media.Animation.DoubleAnimation translateBounceAnimation = new System.Windows.Media.Animation.DoubleAnimation(0, -5, new Duration(TimeSpan.FromMilliseconds(100)));

        public NotificationUserControl()
        {
            InitializeComponent();
            showTimer.Interval = TimeSpan.FromSeconds(3);
            showTimer.Tick += ShowTimer_Tick;

            translateTransform.X = Convert.ToDouble(translateOutAnimation.To);
            this.Opacity = Convert.ToDouble(fadeOutAnimation.To);

            fadeInAnimation.Completed += (o, e) =>
            {
                this.BeginAnimation(FrameworkElement.OpacityProperty, null);
                this.Opacity = Convert.ToDouble(fadeInAnimation.To);
            };

            fadeOutAnimation.Completed += (o, e) =>
            {
                this.BeginAnimation(FrameworkElement.OpacityProperty, null);
                this.Opacity = Convert.ToDouble(fadeOutAnimation.To);
                this.Visibility = Visibility.Hidden;
            };

            translateBounceAnimation.AutoReverse = true;
        }

        private void ShowTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                Hide();
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        public void Show(string text)
        {
            try
            {
                showTimer.Stop();
                showTimer.Start();
                notificationTextBlock.Text = text;

                if (this.Visibility != Visibility.Visible)
                {
                    this.Visibility = Visibility.Visible;
                    this.BeginAnimation(FrameworkElement.OpacityProperty, fadeInAnimation);
                    translateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, translateInAnimation);
                }
                else
                {
                    translateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, translateBounceAnimation);
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        public void Hide()
        {
            try
            {
                this.BeginAnimation(FrameworkElement.OpacityProperty, fadeOutAnimation);
                translateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, translateOutAnimation);
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }
    }
}
