﻿using Shared.MVVM.Model.Networking;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Client.MVVM.View.Converters
{
    public class PortToStringConverter : IValueConverter
    {
        public PortToStringConverter() { }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var port = value as Port;
            if (port == null)
                throw new ArgumentException("Value is not of type Port.");
            return port.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = Port.TryParse((string)value);
            if (status.Code != 0) return null;
            return (Port)status.Data;
        }
    }
}
