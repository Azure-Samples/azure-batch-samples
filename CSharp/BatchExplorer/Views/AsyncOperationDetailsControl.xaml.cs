//Copyright (c) Microsoft Corporation

using System.Windows.Controls;
using Microsoft.Azure.BatchExplorer.ViewModels;

namespace Microsoft.Azure.BatchExplorer.Views
{
    /// <summary>
    /// Interaction logic for AsyncOperationDetailsControl.xaml
    /// </summary>
    public partial class AsyncOperationDetailsControl : UserControl
    {
        private readonly AsyncOperationDetailsViewModel viewModel;

        public AsyncOperationDetailsControl(AsyncOperationDetailsViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            this.DataContext = this.viewModel;
        }
    }
}
