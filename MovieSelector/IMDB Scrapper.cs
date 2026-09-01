using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace IMDB_Scraper
{
    //OMDb refused the request outright - a bad key, or the daily quota is spent. Distinct from
    //a network blip so the caller can tell the user something useful instead of just "FAILED".
    public class OmdbApiException : Exception
    {
        public OmdbApiException(string message) : base(message) { }
    }

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

        //Shared fallback key so the app works out of the box. It is one free 1,000/day
        //allowance across everybody who never sets their own, so it runs out - see README.
        public const string DefaultApiKey = "99d6ab33";

        //The key actually used. Set from Options; falls back to the shared one above.
        public static string ApiKey = DefaultApiKey;

        public static bool IsApiKeyConfigured
        {
            get { return String.IsNullOrWhiteSpace(ApiKey) == false; }
        }

        //Search Engine URLs
        private string DuckDuckGoSearch = "https://duckduckgo.com/?q=imdb+";
        private string YahooSearch = "https://search.yahoo.com/search?p=imdb+";

        private string imdbMatchString = "https://www.imdb.com/title/tt";
/*************************************************************************************************************************************/
        //A year in brackets is the reliable marker - "2012 (2009).mkv" is the 2009 film, not the 2012 one.
        private static readonly Regex parenthesisedYearRegex = new Regex(@"\((19|20)\d{2}\)");

        //Fallback: a bare 4 digit year standing on its own as a token.
        private static readonly Regex bareYearRegex = new Regex(@"(?<=^|[\s._\-])(19|20)\d{2}(?=$|[\s._\-])");

        private static readonly Regex whitespaceRegex = new Regex(@"\s+");

        //Turn a file name into "title year" for the search engines.
        private static string buildSearchTerm(string movieName)
        {
            string cleaned = movieName.Replace(".", " ").Replace("_", " ");

            Match yearMatch = parenthesisedYearRegex.Match(cleaned);
            if (yearMatch.Success == false)
            {
                //Take the LAST bare year, so a title that opens with 4 digits is not mistaken for one.
                MatchCollection bareYears = bareYearRegex.Matches(cleaned);
                if (bareYears.Count > 0)
                {
                    yearMatch = bareYears[bareYears.Count - 1];
                }
            }

            string title = yearMatch.Success ? cleaned.Substring(0, yearMatch.Index) : cleaned;
            string year = yearMatch.Success ? yearMatch.Value.Trim('(', ')') : "";

            return whitespaceRegex.Replace((title + " " + year).Trim(), " ");
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
                string imdbUrl = getIMDbUrl(buildSearchTerm(MovieName));

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
            string searchMovieName = Uri.EscapeDataString(
                                         whitespaceRegex.Replace(
                                             MovieName.Replace("&", " ").Replace("(", " ").Replace(")", " ").Replace("-", " "),
                                             " ").Trim())
                                        .Replace("%20", "+");

            if (searchEngine.ToLower().Equals("yahoo"))
                url = YahooSearch + searchMovieName;
            else if (searchEngine.ToLower().Equals("duckduckgo"))
                url = DuckDuckGoSearch + searchMovieName;

            string html = System.Net.WebUtility.UrlDecode(getUrlData(url));

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

            if (String.IsNullOrWhiteSpace(imdbURL) == false)
            {
                return imdbURL; //return first IMDB result
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
            if (imdbUrl.Contains("https") == true)
            {
                Id = imdbUrl.Replace(imdbMatchString, "tt");
            }
            else
            {
                Id = imdbUrl.Replace(imdbMatchString.Replace("https", "http"), "tt");
            }

            //No key means no lookup - the caller reports this as a failed fetch.
            if (IsApiKeyConfigured == false)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Id))
            {
                string url = $"https://www.omdbapi.com/?i={Id}&plot=full&apikey={Uri.EscapeDataString(ApiKey)}";

                string json = getUrlData(url);
                dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                //OMDb answers a bad key or unknown id with Response=False, not an HTTP error.
                string response = data?.Response?.ToString();
                if (String.Equals(response, "False", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return;
                }

                // Rating
                Rating = data.imdbRating?.ToString() ?? string.Empty;
                if (Rating == "N/A")
                {
                    Rating = "?";
                }

                // Plot
                Plot = System.Net.WebUtility.HtmlDecode(data.Plot?.ToString() ?? string.Empty);

                // Tagline — OMDb does not provide taglines, keep empty or remove
                Tagline = string.Empty;

                // Genres
                Genre = data.Genre?.ToString() ?? string.Empty;  // Already comma-separated e.g. "Action, Drama"

                // Director
                Director = data.Director?.ToString() ?? string.Empty;  // Already comma-separated

                // Cast (OMDb returns top 4 billed as comma-separated string)
                Cast = data.Actors?.ToString() ?? string.Empty;

                // Poster
                string posterUrl = data.Poster?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(posterUrl) && posterUrl != "N/A")
                {
                    Poster = posterUrl;
                    PosterLarge = posterUrl;  // OMDb only gives one size, same URL for both
                }
                else
                {
                    Poster = string.Empty;
                    PosterLarge = string.Empty;
                }
            }
        }
/*************************************************************************************************************************************/
        //One client for the whole process - a client per request leaks sockets into TIME_WAIT.
        private static readonly HttpClient httpClient = createHttpClient();

        private static HttpClient createHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = true,
                AllowAutoRedirect = true
            };

            var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(30);

            // Browser-like headers
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            client.DefaultRequestHeaders.Add("Connection", "keep-alive");
            client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

            return client;
        }

        //Yahoo throttles scraped searches hard - it answers 5xx once a burst gets going. A
        //couple of spaced retries turns most of those from a lost movie into a slow one.
        private const int maxAttempts = 3;

        //Get URL Data
        private string getUrlData(string url)
        {
            // Set the connection limit for this server
            var sp = ServicePointManager.FindServicePoint(new Uri(url));
            sp.ConnectionLimit = 20;

            Exception lastError = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using (HttpResponseMessage response = httpClient.GetAsync(url).GetAwaiter().GetResult())
                    {
                        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        if (response.IsSuccessStatusCode == true)
                        {
                            return body;
                        }

                        //OMDb reports a bad key or an exhausted quota as 401 with the reason in
                        //the body. Retrying cannot help, so surface it and stop.
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            throw new OmdbApiException(extractOmdbError(body));
                        }

                        //429 and 5xx are the throttling responses - worth another go.
                        if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                        {
                            lastError = new HttpRequestException("HTTP " + (int)response.StatusCode);
                        }
                        else
                        {
                            throw new HttpRequestException("HTTP " + (int)response.StatusCode);
                        }
                    }
                }
                catch (OmdbApiException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }

                if (attempt < maxAttempts)
                {
                    //1s then 3s. Long enough for a throttle window to lift, short enough to
                    //not stall the queue.
                    System.Threading.Thread.Sleep(attempt == 1 ? 1000 : 3000);
                }
            }

            throw lastError ?? new HttpRequestException("Request failed: " + url);
        }

        //Pull the human readable reason out of an OMDb refusal body.
        private static string extractOmdbError(string body)
        {
            try
            {
                dynamic parsed = Newtonsoft.Json.JsonConvert.DeserializeObject(body);
                string error = parsed?.Error?.ToString();

                if (String.IsNullOrWhiteSpace(error) == false)
                {
                    return error;
                }
            }
            catch (Exception)
            {
            }

            return "OMDb refused the request.";
        }
        /*************************************************************************************************************************************/
    }
}
