using System.Collections.ObjectModel;
using System.Windows;

namespace ZeroElectric.Fenestra.Windows
{
    public partial class Editor : Window
    {
        public Editor()
        {
            InitializeComponent();
        }
    }

    class CompressionTypes : ObservableCollection<string>
    {
        public CompressionTypes()
        {

            Add("None");
            Add("LZ4");
            Add("LZMA");
        }
    }
}
