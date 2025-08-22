using System;
using System.Xml;

namespace MovieSelector
{
    public partial class MainWindow : System.Windows.Window
    {
        private static readonly object mLock2 = new object();
        private System.Timers.Timer saveXMLTimer = new System.Timers.Timer(3000);
/*************************************************************************************************************************************/
        public void AppendEntryToFile(string movieName, MovieDataClass movieDataObj)
        {
            lock (mLock2)
            {
                try
                {
                    XmlNode xmlMovie = GlobalVariables.XmlMovieDoc.CreateNode(XmlNodeType.Element, "Movie", null);

                    XmlNode xmlIMDBID = GlobalVariables.XmlMovieDoc.CreateNode(XmlNodeType.Element, "ID", null);
                    xmlIMDBID.InnerText = movieDataObj.ImdbID;
                    xmlMovie.AppendChild(xmlIMDBID);

                    XmlNode xmlMovieName = GlobalVariables.XmlMovieDoc.CreateNode(XmlNodeType.Element, "Name", null);
                    xmlMovieName.InnerText = movieName;
                    xmlMovie.AppendChild(xmlMovieName);

                    XmlNode xmlTagline = GlobalVariables.XmlMovieDoc.CreateNode(XmlNodeType.Element, "Tagline", null);
                    xmlTagline.InnerText = movieDataObj.Tagline;
                    xmlMovie.AppendChild(xmlTagline);

                    XmlNode xmlRating = GlobalVariables.XmlMovieDoc.CreateNode(XmlNodeType.Element, "Rating", null);
                    xmlRating.InnerText = movieDataObj.Rating;
                    xmlMovie.AppendChild(xmlRating);

                    XmlNode xmlPlot = GlobalVariables.XmlMovieDoc.CreateNode(XmlNodeType.Element, "Plot", null);
                    xmlPlot.InnerText = movieDataObj.Plot;
                    xmlMovie.AppendChild(xmlPlot);

                    XmlNode xmlGenre = GlobalVariables.XmlMovieDoc.CreateNode(XmlNodeType.Element, "Genre", null);
                    xmlGenre.InnerText = movieDataObj.Genre;
                    xmlMovie.AppendChild(xmlGenre);

                    XmlNode xmlDirector = GlobalVariables.XmlMovieDoc.CreateNode(XmlNodeType.Element, "Director", null);
                    xmlDirector.InnerText = movieDataObj.Director;
                    xmlMovie.AppendChild(xmlDirector);

                    XmlNode xmlCast = GlobalVariables.XmlMovieDoc.CreateNode(XmlNodeType.Element, "Cast", null);
                    xmlCast.InnerText = movieDataObj.Cast;
                    xmlMovie.AppendChild(xmlCast);

                    GlobalVariables.XmlMovieDoc.SelectSingleNode("IMDB_Database").AppendChild(xmlMovie);
                    saveXMLTimer.Start();
                    //GlobalVariables.XmlMovieDoc.Save(GlobalPath.MOVIE_DATABASE_PATH);
                }
                catch (Exception ex)
                {
                    Log.Write(Log.LogMsgType.I, ex.Message);
                }
            }
        }

        private void SaveXMLTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            saveXMLTimer.Stop();
            GlobalVariables.XmlMovieDoc.Save(GlobalPath.MOVIE_DATABASE_PATH);
        }

        public void UpdateEntryToFile(string movieName, MovieDataClass movieDataObj)
        {
            lock (mLock2)
            {
                try
                {
                    XmlNodeList xmlMovieList = GlobalVariables.XmlMovieDoc.SelectNodes("IMDB_Database/Movie");

                    for (int i = xmlMovieList.Count - 1; i >= 0; i--)
                    {
                        if (xmlMovieList[i].SelectSingleNode("Name").InnerText == movieName)
                        {
                            if (xmlMovieList[i].SelectSingleNode("ID") != null)
                            {
                                xmlMovieList[i].SelectSingleNode("ID").InnerText = movieDataObj.ImdbID;
                            }
                            else
                            {
                                XmlNode xmlIMDBID = GlobalVariables.XmlMovieDoc.CreateNode(XmlNodeType.Element, "ID", null);
                                xmlIMDBID.InnerText = movieDataObj.ImdbID;
                                xmlMovieList[i].AppendChild(xmlIMDBID);
                            }
                            xmlMovieList[i].SelectSingleNode("Tagline").InnerText = movieDataObj.Tagline;
                            xmlMovieList[i].SelectSingleNode("Rating").InnerText = movieDataObj.Rating;
                            xmlMovieList[i].SelectSingleNode("Plot").InnerText = movieDataObj.Plot;

                            if (xmlMovieList[i].SelectSingleNode("Genre") != null)
                            {
                                xmlMovieList[i].SelectSingleNode("Genre").InnerText = movieDataObj.Genre;
                            }
                            else
                            {
                                XmlNode xmlGenre = GlobalVariables.XmlMovieDoc.CreateNode(XmlNodeType.Element, "Genre", null);
                                xmlGenre.InnerText = movieDataObj.Genre;
                                xmlMovieList[i].AppendChild(xmlGenre);
                            }

                            if (xmlMovieList[i].SelectSingleNode("Director") != null)
                            {
                                xmlMovieList[i].SelectSingleNode("Director").InnerText = movieDataObj.Director;
                            }
                            else
                            {
                                XmlNode xmlDirector = GlobalVariables.XmlMovieDoc.CreateNode(XmlNodeType.Element, "Director", null);
                                xmlDirector.InnerText = movieDataObj.Director;
                                xmlMovieList[i].AppendChild(xmlDirector);
                            }

                            if (xmlMovieList[i].SelectSingleNode("Cast") != null)
                            {
                                xmlMovieList[i].SelectSingleNode("Cast").InnerText = movieDataObj.Cast;
                            }
                            else
                            {
                                XmlNode xmlCast = GlobalVariables.XmlMovieDoc.CreateNode(XmlNodeType.Element, "Cast", null);
                                xmlCast.InnerText = movieDataObj.Cast;
                                xmlMovieList[i].AppendChild(xmlCast);
                            }

                            //GlobalVariables.XmlMovieDoc.Save(GlobalPath.MOVIE_DATABASE_PATH);
                            saveXMLTimer.Start();
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Write(Log.LogMsgType.I, ex.Message);
                }
            }
        }
/*************************************************************************************************************************************/
    }
}
