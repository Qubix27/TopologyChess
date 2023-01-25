﻿using System;
using System.Globalization;
using System.Windows.Data;

namespace TopologyChess
{
    public class CellColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int v && (v % 2 == 0 ^ v / 8 % 2 == 0);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => null;
    }
}