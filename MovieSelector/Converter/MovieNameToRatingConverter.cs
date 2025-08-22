using System;
using System.Windows.Data;

namespace MovieSelector
{
    public class MovieNameToRatingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if(value == null)
                return "Error";
            
            MovieDataClass movieDataObj = new MovieDataClass();

            if (GlobalVariables.FailedToGetFromIMDBLIST.Contains(value.ToString()) == true)
            {
                return "Failed";
            }

            if (GlobalVariables.MemoryDatabase.TryGetValue(value.ToString(), out movieDataObj) == false)
                return "Retrieving Data...";

            return movieDataObj.Rating + "/10";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
