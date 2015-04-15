using System.Windows.Controls;
using Microsoft.Azure.BatchExplorer.ViewModels;

namespace Microsoft.Azure.BatchExplorer.Views.CreateControls
{
    /// <summary>
    /// Interaction logic for CreateWorkItemControl.xaml
    /// </summary>
    public partial class CreateWorkItemControl : UserControl
    {
        private CreateWorkItemViewModel viewModel;

        public CreateWorkItemControl(CreateWorkItemViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            this.DataContext = viewModel;
        }
    }
}
