using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Xml;

namespace MovieSelector
{    
    public partial class MainWindow : System.Windows.Window
    {
        public bool moviesWithNoIDDetected = false;
/*************************************************************************************************************************************/
        private void checkConfigFiles()
        {
            try
            {
                //Check UI Options Path
                if (File.Exists(GlobalPath.GUI_OPTIONS_PATH) == false)
                { 
                    recreateConfigFile();
                }

                //Check Movie Database File
                if (File.Exists(GlobalPath.MOVIE_DATABASE_PATH) == false)
                {
                    recreateMovieDatabaseFile();
                }
             }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void recreateConfigFile()
        {
            try
            {
                using (XmlWriter writer = XmlWriter.Create(GlobalPath.GUI_OPTIONS_PATH))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("MovieSelector");

                    foreach (string dirPath in GlobalPath.MOVIE_LOCATION_LIST)
                    {
                        writer.WriteElementString("DefaultMoviesLoc", dirPath);
                    }

                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void recreateMovieDatabaseFile()
        {
            try
            {
                using (XmlWriter writer = XmlWriter.Create(GlobalPath.MOVIE_DATABASE_PATH))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("IMDB_Database");
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }
/*************************************************************************************************************************************/
        private void loadSettings()
        {
            try
            {
                if (movieLocLB == null) //Have not initialise finish
                {
                    return;
                }

                //Load the config file
                XmlDocument xmlSettingsDoc = loadSettings_LoadConfigFile();

                //Read the config file movie location
                loadSettings_ReadMovieLocations(xmlSettingsDoc);

                //Update the location into the UI 
                loadSettings_UpdateMovieLocationsIntoUI();

                //Empty options
                if (GlobalPath.MOVIE_LOCATION_LIST.Count == 0)
                {
                    string thisFilepath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                    if (thisFilepath.Contains("Movies\\Movie Selector") == true)
                    {
                        GlobalPath.MOVIE_LOCATION_LIST.Add(thisFilepath.Replace("\\Movie Selector", ""));
                    }
                    else
                    {
                        GlobalPath.MOVIE_LOCATION_LIST.Add(thisFilepath);
                    }

                    defaultLocMovieLocation.Text = "Using default location: " + GlobalPath.MOVIE_LOCATION_LIST.ElementAt(0);
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }
        
        private XmlDocument loadSettings_LoadConfigFile()
        {
            XmlDocument xmlSettingsDoc = new XmlDocument();

            try
            {
                xmlSettingsDoc.Load(Pathing.GetUNCPath(GlobalPath.GUI_OPTIONS_PATH));
            }
            catch (Exception)
            {
                recreateConfigFile();
                xmlSettingsDoc.Load(Pathing.GetUNCPath(GlobalPath.GUI_OPTIONS_PATH));
            }

            return xmlSettingsDoc;
        }

        private void loadSettings_ReadMovieLocations(XmlDocument xmlSettingsDoc)
        {
            try
            {
                GlobalPath.MOVIE_LOCATION_LIST.Clear();

                XmlNodeList nodeList = xmlSettingsDoc.SelectNodes("MovieSelector/DefaultMoviesLoc");
                foreach (XmlNode node in nodeList)
                {
                    try
                    {
                        if (String.IsNullOrWhiteSpace(node.InnerText) == false && Directory.Exists(node.InnerText) == true)
                        {
                            GlobalPath.MOVIE_LOCATION_LIST.Add(Pathing.GetUNCPath(node.InnerText));
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }

        private void loadSettings_UpdateMovieLocationsIntoUI()
        {
            try
            {
                movieLocLB.Items.Clear();

                foreach (string dirPath in GlobalPath.MOVIE_LOCATION_LIST)
                {
                    movieLocLB.Items.Add(dirPath);
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
            }
        }
/*************************************************************************************************************************************/
        private void loadDBIntoMemory()
        {
            try
            {
                GlobalVariables.XmlMovieDoc.Load(GlobalPath.MOVIE_DATABASE_PATH);
                XmlNodeList xmlMovieList = GlobalVariables.XmlMovieDoc.SelectNodes("IMDB_Database/Movie");
                GlobalVariables.MemoryDatabase.Clear();

                for (int i = 0; i < xmlMovieList.Count; i++)
                {
                    MovieDataClass movieDataObj = new MovieDataClass();
                    
                    if (xmlMovieList[i].SelectSingleNode("ID") != null)
                    {
                        movieDataObj.ImdbID = xmlMovieList[i].SelectSingleNode("ID").InnerText;
                    }
                    movieDataObj.Tagline = xmlMovieList[i].SelectSingleNode("Tagline").InnerText;
                    //movieDataObj.ImageURL = xmlMovieList[i].SelectSingleNode("ImageURL").InnerText;
                    movieDataObj.Rating = xmlMovieList[i].SelectSingleNode("Rating").InnerText;
                    movieDataObj.Plot = xmlMovieList[i].SelectSingleNode("Plot").InnerText;
                    if (xmlMovieList[i].SelectSingleNode("Genre") != null)
                    {
                        movieDataObj.Genre = xmlMovieList[i].SelectSingleNode("Genre").InnerText;
                    }
                    if (xmlMovieList[i].SelectSingleNode("Director") != null)
                    {
                        movieDataObj.Director = xmlMovieList[i].SelectSingleNode("Director").InnerText;
                    }
                    if (xmlMovieList[i].SelectSingleNode("Cast") != null)
                    {
                        movieDataObj.Cast = xmlMovieList[i].SelectSingleNode("Cast").InnerText;
                    }

                    GlobalVariables.MemoryDatabase.Add(xmlMovieList[i].SelectSingleNode("Name").InnerText, movieDataObj);

                    if (movieDataObj.ImdbID == "")
                    {
                        moviesWithNoIDDetected = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write(Log.LogMsgType.I, ex.Message);
                recreateMovieDatabaseFile();
            }
        }
/*************************************************************************************************************************************/        
    }
}
