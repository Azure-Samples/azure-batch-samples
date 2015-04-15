using System.Windows.Controls;

namespace Microsoft.Azure.BatchExplorer.Plugins.AccountPlugin
{
    /// <summary>
    /// Interaction logic for AccountManagementControl.xaml
    /// </summary>
    public partial class DefaultAccountManagementControl : UserControl
    {
        private readonly DefaultAccountDialogViewModel viewModel;

        public DefaultAccountManagementControl(DefaultAccountDialogViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            this.DataContext = this.viewModel;
        }
    }
}
