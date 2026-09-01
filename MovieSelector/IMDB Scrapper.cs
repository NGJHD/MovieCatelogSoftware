using System;
using System.Collections.Generic;
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

        //Search Engine URLs. Note the DuckDuckGo one: the main duckduckgo.com page is a
        //JavaScript shell that server renders no results at all, so scraping it always came back
        //empty and every movie Yahoo missed was lost. The lite endpoint is the plain HTML one.
        private const string DuckDuckGoSearch = "https://lite.duckduckgo.com/lite/?q=imdb+";
        private const string YahooSearch = "https://search.yahoo.com/search?p=imdb+";
/*************************************************************************************************************************************/
        //A year in brackets is the reliable marker - "2012 (2009).mkv" is the 2009 film, not the 2012 one.
        private static readonly Regex parenthesisedYearRegex = new Regex(@"\((19|20)\d{2}\)");

        //Fallback: a bare 4 digit year standing on its own as a token.
        private static readonly Regex bareYearRegex = new Regex(@"(?<=^|[\s._\-])(19|20)\d{2}(?=$|[\s._\-])");

        private static readonly Regex whitespaceRegex = new Regex(@"\s+");

        //Everything a release group tacks onto the end of a name. Only needed when there is no
        //year to cut at - "Dune.Part.Two.2160p.WEB-DL.x265-FLUX.mkv" has to be trimmed some other way.
        private static readonly Regex releaseJunkRegex = new Regex(
            @"(?:^|\s)(?:2160p|1080p|1080i|720p|576p|480p|4k|uhd|hdr10?|sdr|bluray|blu\s*ray|brrip|bdrip|bdremux|dvdrip|dvdscr|webrip|web\s*dl|webdl|hdtv|hdrip|remux|proper|repack|internal|extended|unrated|uncut|imax|limited|x264|x265|h\s*264|h\s*265|hevc|avc|xvid|divx|aac\d*|ac3|eac3|dts(?:\s*hd)?|ddp?\d|atmos|truehd|multi|dual\s*audio|subbed|dubbed|yify|yts|rarbg|sparks|amzn|dsnp|hmax|mkv|mp4|avi|m4v|mov|wmv|mpg|mpeg|iso)\b",
            RegexOptions.IgnoreCase);

        private static readonly Regex fileExtensionRegex = new Regex(
            @"\.(mkv|mp4|avi|m4v|mov|wmv|mpg|mpeg|iso|ts)$", RegexOptions.IgnoreCase);

        //Only letters and digits survive, so "Spider-Man: Homecoming" and "Spider Man Homecoming"
        //compare equal.
        private static readonly Regex nonAlphanumericRegex = new Regex(@"[^a-z0-9]");

        private static readonly Regex anyYearRegex = new Regex(@"(19|20)\d{2}");

        //Pull every IMDb title id out of a search page - encoded, because Yahoo wraps its results
        //in a redirect with the target percent encoded, or plain.
        private static readonly Regex imdbIdRegex = new Regex(
            @"imdb\.com(?:%2f|/)title(?:%2f|/)(tt\d{6,10})", RegexOptions.IgnoreCase);

        //A bracketed group is where a Chinese or Japanese named film usually carries its English
        //title - "龙棺古墓 - 西夏狼王 (The Dragon Tomb - Ancient Legend)". Neither OMDb nor the search
        //engines find anything from the original script, so that group has to be searched separately.
        private static readonly Regex bracketedGroupRegex = new Regex(@"\(([^()]*)\)");

        //Anything outside Latin script. OMDb indexes English titles, so a name written in another
        //script searches as nothing at all.
        private static readonly Regex nonLatinRegex = new Regex(@"[^\u0020-\u024F]");

        private static readonly Regex hasAlphanumericRegex = new Regex(@"[a-zA-Z0-9]");

        //Too common to say anything about whether two titles are the same film.
        private static readonly string[] titleStopWords =
            { "the", "a", "an", "of", "and", "in", "on", "to", "for", "part", "movie", "film" };

        //How the things made about a film name themselves. They are typed as films, dated to the
        //same year and named after the film, so the title is the only thing that gives them away.
        private static readonly Regex companionPieceRegex = new Regex(
            @"\b(?:making\s+of|behind\s+the\s+scenes|red\s+carpet|world\s+premiere|movie\s+premiere|film\s+review|movie\s+review|first\s+impressions|shot\s+by\s+shot|mini\s+version|movie\s+special|featurette|promo|teaser|trailer)\b",
            RegexOptions.IgnoreCase);

        //Split a file or folder name into the title and the year, dropping the release junk.
        private static void splitNameIntoTitleAndYear(string movieName, out string title, out string year)
        {
            string cleaned = fileExtensionRegex.Replace(movieName ?? "", "").Replace(".", " ").Replace("_", " ");

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

            if (yearMatch.Success == true)
            {
                //The year sits between the title and the junk, so cutting there does both jobs.
                title = cleaned.Substring(0, yearMatch.Index);
                year = yearMatch.Value.Trim('(', ')');
            }
            else
            {
                //Nothing to cut at - fall back to trimming from the first release tag.
                Match junk = releaseJunkRegex.Match(cleaned);
                title = junk.Success ? cleaned.Substring(0, junk.Index) : cleaned;
                year = "";
            }

            title = whitespaceRegex.Replace(title.Replace("[", " ").Replace("]", " "), " ").Trim().Trim('-').Trim();
        }

        //Constructor
        public IMDB(string MovieName, bool isURL = false)
        {
            if (isURL == true)
            {
                Id = idFromUrl(MovieName);

                //No key means no lookup - the caller reports this as a failed fetch.
                if (IsApiKeyConfigured == false)
                {
                    return;
                }

                dynamic data = omdbLookupById(Id);
                if (data != null)
                {
                    applyOmdbData(data);
                }

                return;
            }

            if (IsApiKeyConfigured == false)
            {
                return;
            }

            string title, year;
            splitNameIntoTitleAndYear(MovieName, out title, out year);

            resolve(title, year);
        }

        private static string idFromUrl(string imdbUrl)
        {
            Match match = imdbIdRegex.Match(imdbUrl ?? "");
            return match.Success ? match.Groups[1].Value : "";
        }

        //The names worth searching for, best first. A folder is often named in more than one way at
        //once - an original title in another script, an English one in brackets, an edition note -
        //and only one of them is the name OMDb or a search engine will recognise.
        private static List<string> buildTitleVariants(string title)
        {
            List<string> variants = new List<string>();

            //The whole name, minus anything not in Latin script.
            addTitleVariant(variants, title);

            //The name with the bracketed groups taken out - drops an edition note like
            //"(Director's Cut)" that no index carries.
            addTitleVariant(variants, bracketedGroupRegex.Replace(title ?? "", " "));

            //Each bracketed group on its own - where the English title usually sits.
            foreach (Match group in bracketedGroupRegex.Matches(title ?? ""))
            {
                addTitleVariant(variants, group.Groups[1].Value);
            }

            //Three is enough to cover the ways a name is written without spending a search on each.
            return variants.Take(3).ToList();
        }

        private static void addTitleVariant(List<string> variants, string candidate)
        {
            //Strip the non-Latin script, then drop the tokens it leaves behind - taking the Chinese
            //out of "龙棺古墓 - 西夏狼王 (The Dragon Tomb)" strands the dash that used to separate them.
            string stripped = nonLatinRegex.Replace(candidate ?? "", " ").Replace("(", " ").Replace(")", " ");

            string cleaned = String.Join(" ", whitespaceRegex.Split(stripped)
                                                             .Where(token => hasAlphanumericRegex.IsMatch(token) == true));

            //A variant that was written entirely in another script has nothing left by now. A
            //title that is only digits - "2012", "1917" - is a real one, so do not ask for letters.
            if (String.IsNullOrWhiteSpace(cleaned) == true)
            {
                return;
            }

            //What survives stripping a non-Latin name is usually not a name at all, just the part
            //of it that happened to be written in Latin: "绣春刀II修罗战场" leaves "II", and OMDb has a
            //Spanish film actually called "II" waiting to match it. A remnant this short is noise.
            if (nonLatinRegex.IsMatch(candidate ?? "") == true && normalise(cleaned).Length < 4)
            {
                return;
            }

            if (variants.Any(existing => String.Equals(existing, cleaned, StringComparison.OrdinalIgnoreCase)) == false)
            {
                variants.Add(cleaned);
            }
        }
/*************************************************************************************************************************************/
        //Work out which IMDb entry a name refers to. OMDb's own title search goes first because
        //it needs no scraping and answers correctly for anything carrying a year; the search
        //engines are only there for the titles its matcher misses.
        private void resolve(string title, string year)
        {
            List<string> variants = buildTitleVariants(title);

            //OMDb's own index first, for every way the name is written.
            foreach (string variant in variants)
            {
                if (resolveFromOmdbIndex(variant, year) == true)
                {
                    return;
                }
            }

            //Then the search engines, which do find films OMDb has filed under another title
            //entirely - a translation, a transliteration, or a name the film has since changed.
            foreach (string searchUrl in new[] { YahooSearch, DuckDuckGoSearch })
            {
                foreach (string variant in variants)
                {
                    if (resolveFromSearchEngine(variant, year, searchUrl) == true)
                    {
                        return;
                    }
                }
            }

            //Nothing better turned up, so an empty entry under the right name beats no entry.
            if (placeholder != null)
            {
                applyOmdbData(placeholder);
            }
        }

        //A record OMDb has the name of and nothing else. It is worth keeping as a last resort but
        //never worth stopping at, because the real entry is often sitting under the adjacent year.
        private dynamic placeholder;

        private void holdPlaceholder(dynamic data)
        {
            if (placeholder == null)
            {
                placeholder = data;
            }
        }

        private static bool isPlaceholder(dynamic data)
        {
            return hasValue(data?.Plot?.ToString()) == false &&
                   hasValue(data?.Director?.ToString()) == false &&
                   hasValue(data?.Country?.ToString()) == false;
        }

        //OMDb writes a field it does not have as the string "N/A", not as an empty one.
        private static bool hasValue(string field)
        {
            return String.IsNullOrWhiteSpace(field) == false &&
                   String.Equals(field.Trim(), "N/A", StringComparison.OrdinalIgnoreCase) == false;
        }

        //Ask OMDb directly - exact title, then exact title without the year, then its fuzzy search.
        private bool resolveFromOmdbIndex(string title, string year)
        {
            string escapedTitle = Uri.EscapeDataString(title);

            //Exact title plus year - the common case.
            if (String.IsNullOrEmpty(year) == false)
            {
                dynamic data = omdbQuery("t=" + escapedTitle + "&y=" + year);
                if (isStrictMatch(data, title, year) == true)
                {
                    if (isPlaceholder(data) == false)
                    {
                        applyOmdbData(data);
                        return true;
                    }

                    holdPlaceholder(data);
                }
            }

            //Exact title on its own, in case the year on the file is the release year rather
            //than the production year OMDb carries. IMDb sometimes carries a film twice - an
            //empty stub under one year and the real entry under the next - and asking with the
            //year pins the stub, so this is the query that finds "Jim Button and Luke the Engine
            //Driver" rather than the blank 2018 duplicate of it.
            dynamic exact = omdbQuery("t=" + escapedTitle);
            if (isStrictMatch(exact, title, year) == true)
            {
                if (isPlaceholder(exact) == false)
                {
                    applyOmdbData(exact);
                    return true;
                }

                holdPlaceholder(exact);
            }

            //OMDb's fuzzy search - catches the subtitles and punctuation its exact matcher trips on.
            dynamic search = omdbQuery("s=" + escapedTitle + (String.IsNullOrEmpty(year) == true ? "" : "&y=" + year));
            string searchId = bestSearchResultId(search, title, year);

            if (String.IsNullOrEmpty(searchId) == false)
            {
                dynamic byId = omdbLookupById(searchId);
                if (byId != null)
                {
                    applyOmdbData(byId);
                    return true;
                }
            }

            return false;
        }

        //Scrape one search engine and take the first candidate id OMDb agrees is this film.
        //Taking the first id on the page - what this used to do - picks up Yahoo's movie
        //knowledge panel, which links a different IMDb entity than the film's own page: for
        //"Spider-Man Homecoming 2017" that is tt2990104, which OMDb has no record of at all, and
        //for "Maleficent 2014" it has handed back a sequel's placeholder entry instead.
        private bool resolveFromSearchEngine(string title, string year, string searchUrl)
        {
            string query = Uri.EscapeDataString(
                               whitespaceRegex.Replace(
                                   (title + " " + year).Replace("&", " ").Replace("(", " ").Replace(")", " ").Replace("-", " "),
                                   " ").Trim())
                              .Replace("%20", "+");

            string html;

            try
            {
                html = getSearchEngineData(searchUrl + query);
            }
            catch (OmdbApiException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }

            List<dynamic> candidates = candidateIds(html).Select(id => omdbLookupById(id))
                                                         .Where(data => data != null)
                                                         .ToList();

            //First choice is a candidate whose title agrees with the name on disk. A search engine
            //has already ranked these by relevance, so a looser reading of "agrees" is safe here in
            //a way it is not for a title OMDb handed back off its own index.
            foreach (dynamic data in candidates)
            {
                if (isCompanionPiece(data) == false && isLooseMatch(data, title, year) == true)
                {
                    applyOmdbData(data);
                    return true;
                }
            }

            //Failing that, take the top result of the right year even though its title differs.
            //OMDb files plenty of films under a translation or a transliteration - "Di Renjie -
            //Red Eyes" for "Detective Dee and The Red Eye" - or under a name the film has since
            //changed, and the year is a strong enough check on its own for a search we made from
            //this exact title. Without a year to check against there is nothing to lean on, so
            //the guess is not worth making.
            //Nothing about the title is being checked here, so the record itself has to carry its
            //weight: a film OMDb actually knows has a plot. Without that this accepts any nearby
            //film of the right year - it answered "The Dragon Tomb - Ancient Legend (2021)" with a
            //different "Dragon Tomb", on nothing more than the year being one out.
            if (String.IsNullOrEmpty(year) == false)
            {
                foreach (dynamic data in candidates)
                {
                    if (isFilm(data) == true && isCompanionPiece(data) == false &&
                        hasValue(data.Plot?.ToString()) == true &&
                        yearsMatch(data.Year?.ToString() ?? "", year) == true)
                    {
                        applyOmdbData(data);
                        return true;
                    }
                }
            }

            return false;
        }

        //A search page links soundtracks, games and episodes alongside the film.
        private static bool isFilm(dynamic data)
        {
            string type = data?.Type?.ToString() ?? "";

            return String.Equals(type, "movie", StringComparison.OrdinalIgnoreCase) == true ||
                   String.Equals(type, "series", StringComparison.OrdinalIgnoreCase) == true;
        }

        //IMDb carries a lot of things that are about a film rather than being it - the making of,
        //a premiere broadcast, a fan review - and they are typed as films and dated to the same
        //year, so nothing else here tells them apart. The giveaway is the title: a real one does
        //not restate its own year, and does not announce itself as coverage of something else.
        private static bool isCompanionPiece(dynamic data)
        {
            string title = data?.Title?.ToString() ?? "";

            return parenthesisedYearRegex.IsMatch(title) == true || companionPieceRegex.IsMatch(title) == true;
        }

        //Every distinct IMDb id on the page, in the order they appear.
        private static IEnumerable<string> candidateIds(string html)
        {
            string decoded = System.Net.WebUtility.UrlDecode(html ?? "");

            List<string> ids = new List<string>();

            foreach (string source in new[] { decoded, html ?? "" })
            {
                foreach (Match match in imdbIdRegex.Matches(source))
                {
                    string id = match.Groups[1].Value.ToLowerInvariant();

                    if (ids.Contains(id) == false)
                    {
                        ids.Add(id);
                    }
                }
            }

            //A page full of unrelated links is not worth a lookup each, and every lookup spends
            //quota - the real result is always near the top.
            return ids.Take(8);
        }
/*************************************************************************************************************************************/
        private static dynamic omdbLookupById(string id)
        {
            if (String.IsNullOrWhiteSpace(id) == true)
            {
                return null;
            }

            return omdbQuery("i=" + Uri.EscapeDataString(id));
        }

        //Run one OMDb query. Returns null when OMDb answers Response=False, which is how it
        //reports both an id it has no record of and a title it cannot match.
        private static dynamic omdbQuery(string query)
        {
            string url = "https://www.omdbapi.com/?" + query + "&plot=full&apikey=" + Uri.EscapeDataString(ApiKey);

            dynamic data;

            try
            {
                data = Newtonsoft.Json.JsonConvert.DeserializeObject(getUrlData(url));
            }
            catch (OmdbApiException)
            {
                throw;
            }
            catch (Exception)
            {
                return null;
            }

            string response = data?.Response?.ToString();

            return String.Equals(response, "False", StringComparison.OrdinalIgnoreCase) == true ? null : data;
        }

        //Pick the entry out of an OMDb "s=" search that actually looks like the film we asked for.
        //This is where the punctuation gets reconciled - "Godzilla vs Kong" against OMDb's
        //"Godzilla vs. Kong", "The Super Mario Bros Movie" against "The Super Mario Bros. Movie" -
        //so the name still has to match, only the punctuation is allowed to differ. Anything looser
        //picks the companion pieces that sit beside the film in these results, and taking the first
        //film of the right year - which this used to do - answers "Frozen 2" with "Frozen 2: Burnt".
        private static string bestSearchResultId(dynamic search, string title, string year)
        {
            if (search == null || search.Search == null)
            {
                return "";
            }

            foreach (dynamic result in search.Search)
            {
                string resultId = result.imdbID?.ToString() ?? "";

                if (String.IsNullOrEmpty(resultId) == true || isFilm(result) == false)
                {
                    continue;
                }

                if (isStrictMatch(result, title, year) == true)
                {
                    return resultId;
                }
            }

            return "";
        }

        //Does this OMDb record describe the film the name named? Without the check a plausible
        //but wrong id gets written to the database, and a complete looking entry is never rescraped.
        //
        //Strict, because this is applied to what OMDb's own title lookup returns, and that lookup
        //is not to be trusted past an exact name: asked for "Good Will Hunting" it answers with
        //"The Making of 'Good Will Hunting'", and asked for "Memento" it answers with a fan entry
        //actually titled "Memento (2000)". Anything short of the same title is a different thing,
        //and the search engines below rank these far better than OMDb does.
        private static bool isStrictMatch(dynamic data, string title, string year)
        {
            if (data == null)
            {
                return false;
            }

            return normalise(data.Title?.ToString() ?? "") == normalise(title) &&
                   normalise(title).Length > 0 &&
                   yearsMatch(data.Year?.ToString() ?? "", year) == true;
        }

        //For candidates a search engine ranked, where a title that merely agrees is good enough.
        //The looser the agreement, the more the record has to stand up on its own: a title that
        //only overlaps has to belong to a film OMDb actually holds something about, or "Dragon
        //Tomb" answers for "The Dragon Tomb - Ancient Legend" on two shared words and a year.
        private static bool isLooseMatch(dynamic data, string title, string year)
        {
            if (data == null || yearsMatch(data.Year?.ToString() ?? "", year) == false)
            {
                return false;
            }

            string candidate = data.Title?.ToString() ?? "";

            return titlesMatchClosely(candidate, title) == true ||
                   (titlesOverlap(candidate, title) == true && hasValue(data.Plot?.ToString()) == true);
        }

        private static bool titlesMatchClosely(string candidate, string wanted)
        {
            string a = normalise(candidate);
            string b = normalise(wanted);

            if (a.Length == 0 || b.Length == 0)
            {
                return false;
            }

            //A subtitle on one side only - "Maleficent" against "Maleficent: Mistress of Evil" -
            //still needs the shorter of the two to be a real prefix, not a substring anywhere.
            return a == b || a.StartsWith(b) == true || b.StartsWith(a) == true;
        }

        private static bool titlesOverlap(string candidate, string wanted)
        {
            //A renamed film shares most of its words without either name being a prefix of the
            //other - "Avatar Aang: The Last Airbender" against "The Legend of Aang - The Last
            //Airbender". Two thirds of the shorter title's telling words has to be shared: a bare
            //half lets one film in a series answer for another, since "Detective Dee: Murder in
            //Chang'an" and "Detective Dee and The Red Eye" have half their words in common and are
            //different films of the same year.
            List<string> candidateWords = significantWords(candidate);
            List<string> wantedWords = significantWords(wanted);

            if (candidateWords.Count == 0 || wantedWords.Count == 0)
            {
                return false;
            }

            int shared = wantedWords.Count(word => candidateWords.Contains(word));
            int needed = (2 * Math.Min(candidateWords.Count, wantedWords.Count) + 2) / 3;

            return shared >= needed;
        }

        private static List<string> significantWords(string title)
        {
            return whitespaceRegex.Split(nonAlphanumericRegex.Replace((title ?? "").ToLowerInvariant(), " "))
                                  .Select(word => word.Trim())
                                  .Where(word => word.Length > 0 && titleStopWords.Contains(word) == false)
                                  .Distinct()
                                  .ToList();
        }

        //OMDb writes series years as "2005-2013"; only the first one matters here.
        private static bool yearsMatch(string candidate, string wanted)
        {
            if (String.IsNullOrEmpty(wanted) == true)
            {
                return true;
            }

            Match candidateYear = anyYearRegex.Match(candidate ?? "");

            if (candidateYear.Success == false)
            {
                return false;
            }

            int found, asked;
            if (int.TryParse(candidateYear.Value, out found) == false || int.TryParse(wanted, out asked) == false)
            {
                return false;
            }

            //A film released either side of new year is filed under either, depending on the source.
            return Math.Abs(found - asked) <= 1;
        }

        private static string normalise(string value)
        {
            return nonAlphanumericRegex.Replace((value ?? "").ToLowerInvariant(), "");
        }
/*************************************************************************************************************************************/
        private void applyOmdbData(dynamic data)
        {
            // Id — take OMDb's own, so a scraped candidate id is never trusted over the record it resolved to
            Id = data.imdbID?.ToString() ?? Id;

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

        //Yahoo and DuckDuckGo throttle by source address, not by connection, so the five scraper
        //threads searching at once are what trip them - and once tripped they stay tripped for a
        //while, which loses whole runs of movies rather than one. One search at a time across the
        //whole process. OMDb is not held by this: it is a paid-for API that answers concurrent
        //requests happily, and there are far more of those to get through.
        private static readonly object searchEngineLock = new object();

        private static string getSearchEngineData(string url)
        {
            //Held across the retries too, so a request that is being throttled does not have four
            //more piling in behind it while it waits out the backoff.
            lock (searchEngineLock)
            {
                return getUrlData(url);
            }
        }

        //Yahoo throttles scraped searches hard - it answers 5xx once a burst gets going. A
        //couple of spaced retries turns most of those from a lost movie into a slow one.
        private const int maxAttempts = 3;

        //Get URL Data
        private static string getUrlData(string url)
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

                        //DuckDuckGo serves its "anomaly" challenge page as 202 Accepted, which
                        //counts as a success - without this the challenge HTML is parsed as results.
                        if ((int)response.StatusCode == 202)
                        {
                            lastError = new HttpRequestException("HTTP 202 (throttled)");
                        }
                        else if (response.IsSuccessStatusCode == true)
                        {
                            return body;
                        }

                        //OMDb reports a bad key or an exhausted quota as 401 with the reason in
                        //the body. Retrying cannot help, so surface it and stop.
                        else if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            throw new OmdbApiException(extractOmdbError(body));
                        }

                        //429 and 5xx are the throttling responses - worth another go.
                        else if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
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
                    //5s then 3s. A second is not long enough for a search engine's throttle
                    //window to lift, so the first retry was mostly being spent to no purpose.
                    System.Threading.Thread.Sleep(attempt == 1 ? 5000 : 3000);
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
