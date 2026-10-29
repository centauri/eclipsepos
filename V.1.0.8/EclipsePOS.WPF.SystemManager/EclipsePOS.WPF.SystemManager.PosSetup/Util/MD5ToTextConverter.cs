using System;
using System.Globalization;
using System.Windows.Data;
namespace EclipsePOS.WPF.SystemManager.PosSetup.Util
{
    public class MD5ToTextConverter : IValueConverter
    {
        public static MD5ToTextConverter Instance = new MD5ToTextConverter();
        public object Convert(object value, Type type, object parameter, CultureInfo culture) { return string.Empty; }
        public object ConvertBack(object value, Type type, object parameter, CultureInfo culture)
        {
            var password = value as string;
            return string.IsNullOrEmpty(password) ? Binding.DoNothing : (object)EclipsePOS.Platform.PasswordHash.Create(password);
        }
    }
}
