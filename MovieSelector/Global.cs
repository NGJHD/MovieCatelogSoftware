using System.Collections.Generic;
using System.Xml;
using System.IO;

namespace MovieSelector
{
/**************************************************************************************************/
    public static class GlobalPath
    {
        public static string MOVIE_DATABASE_PATH = "Database\\Movie_Database.xml";
        public static string GUI_OPTIONS_PATH = "Database\\Gui_Options.xml";
        public static string MOVIE_POSTER_FOLDER_PATH = "Posters\\";
        public static string LOG_PATH = "Log\\";
        public static string LOG_FILENAME = "";

        //Media Location
        public static List<string> MOVIE_LOCATION_LIST = new List<string>();

        public static void CheckDirectory()
        {
            //Check Error Log Directory
            if (Directory.Exists(GlobalPath.LOG_PATH) == false)
            {
                Directory.CreateDirectory(GlobalPath.LOG_PATH);
            }

            //Check UI Options Directory
            if (Directory.Exists(System.IO.Path.GetDirectoryName(GUI_OPTIONS_PATH)) == false)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(GUI_OPTIONS_PATH));
            }

            //Check Movie Poster Directory
            if (Directory.Exists(MOVIE_POSTER_FOLDER_PATH) == false)
            {
                Directory.CreateDirectory(MOVIE_POSTER_FOLDER_PATH);
            }
        }
    }
/**************************************************************************************************/
    public static class GlobalVariables
    {
        public static XmlDocument XmlMovieDoc = new XmlDocument();
        public static Dictionary<string, MovieDataClass> MemoryDatabase = new Dictionary<string, MovieDataClass>();
        public static MainWindow MainWindow;
        public static List<string> FailedToGetFromIMDBLIST = new List<string>();

        public static List<System.Threading.Thread> ListOfRunningThreads = new List<System.Threading.Thread>();

        public static string ErrorIMDB = "Error fetching data from imdb";
    }
/**************************************************************************************************/
}

