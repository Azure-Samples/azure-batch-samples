//Copyright (c) Microsoft Corporation

using System.Windows.Controls;
using Microsoft.Azure.BatchExplorer.ViewModels;

namespace Microsoft.Azure.BatchExplorer.Views.CreateControls
{
    /// <summary>
    /// Interaction logic for CreateVMUserControl.xaml
    /// </summary>
    public partial class ResizePoolControl : UserControl
    {
        private readonly ResizePoolViewModel viewModel;

        public ResizePoolControl(ResizePoolViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            this.DataContext = this.viewModel;
        }
    }
}
