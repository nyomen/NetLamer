using System.Windows;
using System.Windows.Controls;

namespace NetLamer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            DataContext = new MainViewModel();

            InitializeComponent();
        }

        private void processListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (processListView.SelectedItem is not null) processListView.ScrollIntoView(processListView.SelectedItem);
        }
    }
}
