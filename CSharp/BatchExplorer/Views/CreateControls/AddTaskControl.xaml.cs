//Copyright (c) Microsoft Corporation

using System.Windows.Controls;
using Microsoft.Azure.BatchExplorer.ViewModels;
using Xceed.Wpf.Toolkit;
using System;

namespace Microsoft.Azure.BatchExplorer.Views.CreateControls
{
    /// <summary>
    /// Interaction logic for CreateJobScheduleControl.xaml
    /// </summary>
    public partial class AddTaskControl : UserControl
    {
        private AddTaskViewModel viewModel;

        public AddTaskControl(AddTaskViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            this.DataContext = viewModel;
        }
    }
}
