using System.Windows.Controls;
using Microsoft.Azure.BatchExplorer.ViewModels;

namespace Microsoft.Azure.BatchExplorer.Views
{
    /// <summary>
    /// Interaction logic for AccountManagementControl.xaml
    /// </summary>
    public partial class GenericMessageControl : UserControl
    {
        private readonly GenericMessageViewModel viewModel;

        public GenericMessageControl(GenericMessageViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            this.DataContext = this.viewModel;
        }
    }
}
