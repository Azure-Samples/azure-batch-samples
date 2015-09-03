//Copyright (c) Microsoft Corporation

using System.Windows.Controls;
using Microsoft.Azure.BatchExplorer.ViewModels;

namespace Microsoft.Azure.BatchExplorer.Views.CreateControls
{
    /// <summary>
    /// Interaction logic for CreatePoolControl.xaml
    /// </summary>
    public partial class CreatePoolControl : UserControl
    {
        private readonly CreatePoolViewModel viewModel;

        public CreatePoolControl(CreatePoolViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            this.DataContext = this.viewModel;
        }
    }
}
