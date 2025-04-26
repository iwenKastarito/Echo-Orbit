using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EchoOrbit.Converters
{
    public class SliderProgressWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 3 &&
                values[0] is double value &&
                values[1] is double maximum &&
                values[2] is double totalWidth)
            {
                if (maximum == 0) return 0.0;
                return (value / maximum) * totalWidth;
            }
            else if (values.Length == 3 &&
                     values[0] is double progressValue &&
                     values[1] is double progressMaximum &&
                     values[2] is int maxAngle)
            {
                if (progressMaximum == 0) return 0.0;
                return (progressValue / progressMaximum) * maxAngle;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BottomBarMarginConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is double chatDrawerWidth && values[1] is double transformX)
            {
                return new Thickness(0, 0, chatDrawerWidth - transformX, 0);
            }
            return new Thickness(0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class OneSixthMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                return new Thickness(width / 6, 0, 0, 0);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}