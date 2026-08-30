using System;
using System.Windows;

namespace MovieSelector
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            //Last resort logging. An exception on a background thread used to take the process
            //down with nothing written anywhere, which made such crashes invisible.
            DispatcherUnhandledException += (s, args) =>
            {
                Log.Write(Log.LogMsgType.I, "Unhandled UI exception: " + args.Exception.ToString());
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Log.Write(Log.LogMsgType.I, "Unhandled background exception: " + (args.ExceptionObject ?? "").ToString());
            };
        }
    }
}
