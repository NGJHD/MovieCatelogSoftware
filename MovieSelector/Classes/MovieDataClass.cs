using System.ComponentModel;

namespace MovieSelector
{
    public class MovieDataClass
    {
        public string ImdbID;
        public string ImageURL;
        public string Tagline;
        public string Rating;
        public string Plot;
        public string Genre;
        public string Director;
        public string Cast;

        public string GetSearchableString()
        {
            return Genre + " " + Director + " " + Cast;
        }

        public MovieDataClass()
        {
            ImdbID = "";
            ImageURL = "";
            Tagline = "";
            Rating = "";
            Plot = "";
            Genre = "";
            Director = "";
            Cast = "";
        }
    }
}
