using System;
using System.Windows.Data;

namespace MovieSelector
{
    public class SLBIVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string filterText = values[0].ToString();

            if(String.IsNullOrWhiteSpace(filterText) == true)
                return System.Windows.Visibility.Visible;
            
            string movieName = values[1].ToString();
            if (movieName.ToLower().Contains(filterText.ToLower()) == true)
                return System.Windows.Visibility.Visible;

            if (GlobalVariables.MemoryDatabase.ContainsKey(movieName) == true)
            {
                if (GlobalVariables.MemoryDatabase[movieName].GetSearchableString().ToLower().Contains(filterText.ToLower()) == true)
                {
                    return System.Windows.Visibility.Visible;
                }
            }
            

            return System.Windows.Visibility.Collapsed;            
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
