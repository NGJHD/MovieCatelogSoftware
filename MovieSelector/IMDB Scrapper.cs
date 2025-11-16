using System;
using System.Linq;
using System.Collections;
using System.Net;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Threading.Tasks;

namespace IMDB_Scraper
{
    public class IMDB
    {
/*************************************************************************************************************************************/        
        public string Id { get; set; }
        public string Rating { get; set; }
        public string Plot { get; set; }
        public string Poster { get; set; }
        public string PosterLarge { get; set; }        
        public string Tagline { get; set; }

        public string Genre { get; set; }
        public string Director { get; set; }
        public string Cast { get; set; }

        //Search Engine URLs
        //private string GoogleSearch = "https://www.google.com/search?q=imdb+";
        private string DuckDuckGoSearch = "https://duckduckgo.com/?q=imdb+";
        //private string BaiduSearch = "http://www.baidu.com/s?wd=imdb+";
        //private string BingSearch = "http://www.bing.com/search?q=imdb+";
        private string YahooSearch = "https://search.yahoo.com/search?p=imdb+";

        private string imdbMatchString = "https://www.imdb.com/title/tt";        
/*************************************************************************************************************************************/
        private bool checkIfYear(string temp)
        {
            try
            {
                int i = Convert.ToInt32(temp);

                int count = 0;
                do
                {
                    count++;
                } while ((i /= 10) >= 1);

                if (count == 4)
                    return true;
                else
                    return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        //Constructor
        public IMDB(string MovieName, bool isURL=false)
        {
            if (isURL == true)
            {
                parseIMDbPage(MovieName);
            }
            else
            {
                string tempMovieName = MovieName.Replace(".", " ").Replace("_", " ");
                string[] tempArray = tempMovieName.Split(' ');

                MovieName = "";
                foreach (string temp in tempArray)
                {
                    if (checkIfYear(temp) == true)
                    {
                        //MovieName += "(" + temp + ")";
                        MovieName += temp;
                        break;
                    }
                    else
                        MovieName += temp + " ";
                }

                //string imdbUrl = getIMDbUrl(System.Uri.EscapeUriString(MovieName));
                string imdbUrl = getIMDbUrl(System.Uri.EscapeUriString(MovieName.Replace("&", "")).Replace("(", " ").Replace(")", " "));

                if (!string.IsNullOrEmpty(imdbUrl))
                {
                    parseIMDbPage(imdbUrl);
                }
            }
        }

        private string removeLastNonDigit(string str)
        {
            string newStr = str;
            for (int i = str.Length - 1; i >= 0; i--)
            {
                if (char.IsDigit(newStr[i]) == false)
                {
                    newStr = newStr.Remove(i);
                }
                else
                {
                    break;
                }
            }

            return newStr;
        }
 
        //Get IMDB URL from search results
        private string getIMDbUrl(string MovieName, string searchEngine = "yahoo")
        {
            string url = "";
            string searchMovieName = MovieName.Replace('(', '+').Replace(")", "").Replace(' ', '+').Replace('-', '+').Replace('.', '+');
            
            if (searchEngine.ToLower().Equals("yahoo")) 
                url = YahooSearch + searchMovieName;
            else if (searchEngine.ToLower().Equals("duckduckgo"))
                url = DuckDuckGoSearch + searchMovieName;

            string html = System.Net.WebUtility.UrlDecode(getUrlData(url));

            //ArrayList imdbUrls = matchAll(@"<a href=""(http://www.imdb.com/title/tt\d{7}/)"".*?>.*?</a>", html);
            
            string imdbURL = "";            
            if (html.Contains(imdbMatchString) == true)
            {
                imdbURL = removeLastNonDigit(html.Substring(html.IndexOf(imdbMatchString), imdbMatchString.Count() + 10));
                if (imdbURL.Contains("tt000000000") == true)
                {
                    imdbURL = removeLastNonDigit(html.Substring(html.IndexOf(imdbMatchString), imdbMatchString.Count() + 20));
                }
            }
            else if (html.Contains(imdbMatchString.Replace("https", "http")) == true)
            {
                string tempIMDBMatchString = imdbMatchString.Replace("https", "http");
                imdbURL = removeLastNonDigit(html.Substring(html.IndexOf(tempIMDBMatchString), tempIMDBMatchString.Count() + 10));
                if (imdbURL.Contains("tt000000000") == true)
                {
                    imdbURL = removeLastNonDigit(html.Substring(html.IndexOf(tempIMDBMatchString), tempIMDBMatchString.Count() + 20));
                }
            }

            //if (imdbUrls.Count > 0)
            if (String.IsNullOrWhiteSpace(imdbURL) == false)
            {
                return imdbURL;// (string)imdbUrls[0]; //return first IMDB result
            }
            else if (searchEngine == "yahoo") //if Yahoo search fails
            {
                System.Threading.Thread.Sleep(300);
                return getIMDbUrl(MovieName, "duckduckgo"); //search using DuckDuckGo
            }            
            else //search fails
                return string.Empty;
        }
 
        //Parse IMDB page data
        private void parseIMDbPage(string imdbUrl)
        {
            //string html = getUrlData(imdbUrl+"combined");
            string html = getUrlData(imdbUrl + "/reference/");

            //Id = match(@"<link rel=""canonical"" href=""http://www.imdb.com/title/(tt\d{7})/combined"" />", html);
            //Id = match(@"<link rel=""canonical"" href=""https://www.imdb.com/title/(tt\d{7})/reference"" />", html);
            if (imdbUrl.Contains("https") == true)
            {
                Id = imdbUrl.Replace(imdbMatchString, "tt");
            }
            else
            {
                Id = imdbUrl.Replace(imdbMatchString.Replace("https", "http"), "tt");
            }

            if (!string.IsNullOrEmpty(Id))
            {
                Rating = match(@"""ipc-rating-star--rating"">(.*?)</span>", html);                

                string plotSummaryHTML = getUrlData(imdbUrl + "/plotsummary/");
                Plot = System.Net.WebUtility.HtmlDecode(Regex.Replace(match(@"ipc-html-content-inner-div.*?ipc-html-content-inner-div.*?>(.*?)</div>", plotSummaryHTML), "<.*?>", String.Empty)); ;
                Tagline = System.Net.WebUtility.HtmlDecode(match(@"<li[^>]*data-testid=""storyline-taglines""[^>]*>.*?<span[^>]*ipc-metadata-list-item__list-content-item[^>]*>(.*?)</span></li>", html));

                ArrayList genres = matchAll(@"<a[^>]*>(.*?)</a>", match(@">Genres</span>(.*?)</ul>", html));
                for (int j = 0; j < genres.Count; j++)
                {
                    Genre += genres[j] + (j == genres.Count - 1 ? "" : ", ");
                }

                /*ArrayList director = matchAll(@"<a.*?href=""/name/.*?"">(.*?)</a>", match(@"Directed by *</h4>(.*?)</table>", html));                
                for (int j = 0; j < director.Count; j++)
                {
                    Director += director[j] + (j == director.Count - 1 ? "" : ", ");
                }*/

                Director = match(@"Directed by (.*?)\.", html);

                ArrayList cast = matchAll(@"<a[^>]*>(.*?)</a>", match(@">Stars</a>(.*?)</ul>", html));
                for (int j = 0; j < cast.Count; j++)
                {
                    Cast += cast[j] + (j == cast.Count - 1 ? "" : ", ");
                }
                
                Poster = match(@"<div[^>]*class=""[^""]*ipc-poster__poster-image[^""]*""[^>]*>.*?<img[^>]*src=""(https:\/\/m\.media-amazon\.com\/images\/[^""]+)""", html);
                if (!string.IsNullOrEmpty(Poster))
                {
                    Poster = Regex.Replace(Poster, @"_V1.*?.jpg", "_V1._SY200.jpg");
                    PosterLarge = Regex.Replace(Poster, @"_V1.*?.jpg", "_V1._SY500.jpg");                    
                }
                else
                {
                    Poster = string.Empty;
                    PosterLarge = string.Empty;                    
                }                                
            } 
        }
/*************************************************************************************************************************************/  
        //Match single instance
        private string match(string regex, string html, int i = 1)
        {
            return new Regex(regex, RegexOptions.Multiline).Match(html).Groups[i].Value.Trim();
        }
 
        //Match all instances and return as ArrayList
        private ArrayList matchAll(string regex, string html, int i = 1)
        {
            ArrayList list = new ArrayList();
            foreach (Match m in new Regex(regex, RegexOptions.Multiline).Matches(html))
            {
                list.Add(m.Groups[i].Value.Trim());
            }

            return list;
        }

        //Get URL Data
        private string getUrlData(string url)
        {
            // Set global connection limit for the server
            var sp = ServicePointManager.FindServicePoint(new Uri(url));
            sp.ConnectionLimit = 20; 

            // Use HttpClientHandler for automatic decompression
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = true,
                AllowAutoRedirect = true
            };

            using (var client = new HttpClient(handler))
            {
                // Browser-like headers
                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept",
                    "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

                // Synchronous call
                return client.GetStringAsync(url).GetAwaiter().GetResult();
            }
        }
        /*private string getUrlData(string url)
        {
            ExtendedWebClient client = new ExtendedWebClient();

            client.Headers.Add("User-Agent: Lynx/2.9.2 libwww-FM/2.14 SSL-MM/1.4.1 OpenSSL/3.4.0");
            //client.Headers.Add("User-Agent: Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 140.0.0.0 Safari / 537.36");
            
            string html = "";
            using (Stream datastream = client.OpenRead(url))
            {                
                using (StreamReader reader = new StreamReader(datastream))
                {
                    StringBuilder sb = new StringBuilder();
                    while (!reader.EndOfStream)
                        sb.Append(reader.ReadLine());

                    html = sb.ToString();
                }
            }

            return html;
        }*/
        /*************************************************************************************************************************************/
    }

    /*public class ExtendedWebClient : WebClient
    {
        /// <summary>
        /// Gets or sets the maximum number of concurrent connections (default is 2).
        /// </summary>
        public int ConnectionLimit { get; set; }

        /// <summary>
        /// Creates a new instance of ExtendedWebClient.
        /// </summary>
        public ExtendedWebClient()
        {
            this.ConnectionLimit = 20;
        }

        /// <summary>
        /// Creates the request for this client and sets connection defaults.
        /// </summary>
        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address) as HttpWebRequest;

            if (request != null)
            {
                request.ServicePoint.ConnectionLimit = this.ConnectionLimit;
            }

            return request;
        }
    }*/
}
