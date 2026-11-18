using Wpf.Ui.Controls;
using PVCStudioPro.App.ViewModels;

namespace PVCStudioPro.App
{
    public partial class MainWindow : FluentWindow
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
