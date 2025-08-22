using System.ComponentModel;

namespace MovieSelector
{
    public class MovieListBoxClass : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        //public string movieName { get; set; }
        public string movieLoc;

        private string _movieName;
        public string movieName
        {
            get
            {
                return _movieName;
            }
            set
            {
                _movieName = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("movieName"));
                }
            }
        }

        private object _previewImage;
        public object previewImage
        {
            get
            {
                return _previewImage;
            }
            set
            {
                _previewImage = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("previewImage"));
                }
            }
        }

        public MovieListBoxClass(string movieName, string movieLoc)
        {
            this.movieName = movieName;
            this.movieLoc = movieLoc;
        }
    }
}
