using System.Windows.Controls;
using Microsoft.Azure.BatchExplorer.ViewModels;

namespace Microsoft.Azure.BatchExplorer.Views.CreateControls
{
    /// <summary>
    /// Interaction logic for CreateVMUserControl.xaml
    /// </summary>
    public partial class CreateVMUserControl : UserControl
    {
        private readonly CreateVMUserViewModel viewModel;

        public CreateVMUserControl(CreateVMUserViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            this.DataContext = this.viewModel;
        }
    }
}
