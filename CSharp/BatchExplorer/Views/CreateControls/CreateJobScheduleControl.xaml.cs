//Copyright (c) Microsoft Corporation

using System.Windows.Controls;
using Microsoft.Azure.BatchExplorer.ViewModels;

namespace Microsoft.Azure.BatchExplorer.Views.CreateControls
{
    /// <summary>
    /// Interaction logic for CreateJobSchedule.xaml
    /// </summary>
    public partial class CreateJobScheduleControl : UserControl
    {
        private CreateJobScheduleViewModel viewModel;

        public CreateJobScheduleControl(CreateJobScheduleViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            this.DataContext = viewModel;
        }
    }
}
