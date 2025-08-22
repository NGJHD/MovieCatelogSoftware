using System;
using System.Windows.Media.Imaging;
using System.IO;

namespace MovieSelector
{
    public static class HelperClass
    {
        //For bitmap conversion
        private static readonly object mLock1 = new object();

        public static BitmapSource BitmapImageFromFile(string filepath)
        {
            lock (mLock1)
            {
                try
                {
                    var bi = new BitmapImage();

                    using (Stream fs = new FileStream(filepath, FileMode.Open))
                    {
                        bi.BeginInit();
                        bi.StreamSource = fs;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                    }

                    bi.Freeze();

                    return bi;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        private static string[] mediaExtensions = { ".AVI", ".MP4", ".DIVX", ".WMV", ".MKV", ".RMVB", ".RMV", ".AVCHD", ".M4V", ".MOV", ".MPEG", ".MPG" };

        public static bool IsMediaFile(string path)
        {
            return -1 != Array.IndexOf(mediaExtensions, System.IO.Path.GetExtension(path).ToUpperInvariant());
        }
    }
}
