﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TopologyChess
{
    public partial class CellControl : UserControl
    {
        public CellControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty CellProperty =
            DependencyProperty.Register(nameof(Cell), typeof(Cell), typeof(CellControl));

        public Cell Cell
        {
            get => (Cell)GetValue(CellProperty);
            set => SetValue(CellProperty, value);
        }
    }
}
