using System.Windows;
using System.Windows.Controls;
using PVCStudioPro.App.ViewModels;

namespace PVCStudioPro.App.Views
{
    public partial class DocumentDeskView : UserControl
    {
        public DocumentDeskView()
        {
            InitializeComponent();
        }

        private void UserControl_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async void UserControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && DataContext is DocumentDeskViewModel vm)
                {
                    await vm.LoadDocumentAsync(files[0]);
                }
            }
        }
    }
}
