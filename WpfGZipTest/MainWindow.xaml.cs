using GZipTestLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

namespace WpfGZipTest
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var s = "Data\\1.mp4";
            var t = "Data\\1.mp4.gz";
            var gZipTF = new GZipThreadFactory<BlockThread>(CompressionMode.Compress, new FileInfo(s), new FileInfo(t));
            gZipTF.Start();
            MessageBox.Show("OK");
        }
    }
}
