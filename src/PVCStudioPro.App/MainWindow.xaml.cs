using System.Windows.Input;
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

        private void Navigation_MouseEnter(object sender, MouseEventArgs e)
        {
            if (RootNavigation != null)
            {
                RootNavigation.IsPaneOpen = true;
            }
        }

        private void Navigation_MouseLeave(object sender, MouseEventArgs e)
        {
            if (RootNavigation != null)
            {
                RootNavigation.IsPaneOpen = false;
            }
        }
    }
}
