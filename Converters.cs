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
                values[2] is double actualWidth)
            {
                if (maximum <= 0)
                    return 0.0;
                return (value / maximum) * actualWidth;
            }
            return 0.0;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public class BottomBarMarginConverter : IMultiValueConverter
    {
        private const double SlideButtonWidth = 30;
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 &&
                values[0] is double drawerWidth &&
                values[1] is double transformX)
            {
                double visibleWidth = drawerWidth - transformX - SlideButtonWidth;
                if (visibleWidth < 0)
                    visibleWidth = 0;
                return new Thickness(150, 0, visibleWidth, 0);
            }
            return new Thickness(150, 0, 0, 0);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public class OneSixthMarginConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Length > 0 && values[0] is double totalWidth)
            {
                double leftMargin = totalWidth / 6;
                return new Thickness(leftMargin, 0, 0, 0);
            }
            return new Thickness(0);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
