//Copyright (c) Microsoft Corporation

using System.Windows.Controls;
using Microsoft.Azure.BatchExplorer.ViewModels;

namespace Microsoft.Azure.BatchExplorer.Views
{
    /// <summary>
    /// Interaction logic for AccountManagementControl.xaml
    /// </summary>
    public partial class AccountManagementControl : UserControl
    {
        private readonly AccountDialogViewModel viewModel;

        public AccountManagementControl(AccountDialogViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            this.DataContext = this.viewModel;

            this.ContentGrid.Children.Add(this.viewModel.PluginControl);
        }
    }
}
