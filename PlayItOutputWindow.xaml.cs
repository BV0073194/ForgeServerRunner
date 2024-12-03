using System;
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
using System.Windows.Shapes;

namespace ForgeServer_1._16._5__36._2._42_
{
    public partial class PlayItOutputWindow : Window
    {
        public PlayItOutputWindow()
        {
            InitializeComponent();
        }

        // Method to append output to the TextBox
        public void AppendOutput(string output)
        {
            if (string.IsNullOrEmpty(output)) return;

            Dispatcher.Invoke(() =>
            {
                PlayItOutputTextBox.AppendText(output + "\n");
                PlayItOutputTextBox.ScrollToEnd();
            });
        }
    }
}
