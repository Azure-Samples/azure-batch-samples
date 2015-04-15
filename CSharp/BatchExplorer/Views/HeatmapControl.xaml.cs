using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.ViewModels;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace Microsoft.Azure.BatchExplorer.Views
{
    public partial class HeatMapControl : UserControl
    {
        private CancellationTokenSource CancellationTokenSource { get; set; }
        private CancellationTokenSource SleepCancellationTokenSource { get; set; }

        private readonly HeatMapViewModel viewModel;
        private Task pendingRefresh;

        private enum VMUIStates
        {
            Transitioning, 
            Idle, 
            Running, 
            Error, 
            Unknown
        };

        public HeatMapControl(HeatMapViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;
            this.DataContext = this.viewModel;

            this.CancellationTokenSource = new CancellationTokenSource();
            this.pendingRefresh = this.PerformUpdateAsync();

            //Register a message to inform this control when the poll interval has updated so that 
            //we can immediately respond to the change
            Messenger.Default.Register<HeatMapPollIntervalUpdatedMessage>(this, 
                (o) =>
                {
                    if (this.viewModel == o.UpdatedViewModel)
                    {
                        this.SleepCancellationTokenSource.Cancel();
                    }
                });
        }

        public void Cancel()
        {
            this.CancellationTokenSource.Cancel();
            this.SleepCancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Performs an update of the HeatMapControl.
        /// </summary>
        /// <returns></returns>
        private async Task PerformUpdateAsync()
        {
            if (!this.CancellationTokenSource.Token.IsCancellationRequested)
            {
                //Update the underlying model
                try
                {
                    //Update the sleep cancelation token so that it isn't cancelled anymore
                    this.SleepCancellationTokenSource = new CancellationTokenSource();

                    await this.viewModel.HeatMap.RefreshAsync();

                    //Snapshot the VM list
                    List<IVM> vms = new List<IVM>(this.viewModel.HeatMap.VMs);
                    int maxTasksPerVM = this.viewModel.HeatMap.MaxTasksPerVM.Value;

                    await this.Dispatcher.InvokeAsync(() => this.DrawHeatMap(vms, maxTasksPerVM));

                    //Invoke itself after the refresh interval
                    this.pendingRefresh = Task.Delay(this.viewModel.RefreshInterval, this.SleepCancellationTokenSource.Token).ContinueWith((task) => this.PerformUpdateAsync());
                }
                catch (HeatMapTerminatedException)
                {
                    this.Cancel(); //Cancel the heatmap loop
                }
            }
        }

        /// <summary>
        /// Renders the heat map data grid view given the specified list of VMs and the maxTasksPerVM
        /// </summary>
        /// <param name="vms"></param>
        /// <param name="maxTasksPerVM"></param>
        private void DrawHeatMap(List<IVM> vms, int maxTasksPerVM)
        {
            try
            {
                // Reset the grid to cater for periodic refresh
                this.GridHeatmap.ColumnDefinitions.Clear();
                this.GridHeatmap.RowDefinitions.Clear();
                this.GridHeatmap.Children.Clear();
                
                // Get pool information required to draw heatmap
                int numVMs = vms.Count;

                // Work out number of rows & columns for overall heatmap - keep square
                int numRows = (int)Math.Truncate(Math.Sqrt(numVMs));
                int numCols;
                if (numVMs == 0)
                {
                    numCols = 0;
                }
                else
                {
                    numCols = (int)Math.Truncate((Convert.ToDouble(numVMs) / Convert.ToDouble(numRows)) + 0.99);
                }

                // Add required number of rows and columns to grid
                for (int i = 0; i < numRows; i++)
                {
                    RowDefinition rowDef = new RowDefinition();
                    GridHeatmap.RowDefinitions.Add(rowDef);
                }
                for (int i = 0; i < numCols; i++)
                {
                    ColumnDefinition colDef = new ColumnDefinition();
                    GridHeatmap.ColumnDefinitions.Add(colDef);
                }

                // Fill in the grid cells for the pool vm's
                for (int i = 0; i < numVMs; i++)
                {
                    int row = i / numCols; 
                    int column = i % numCols;

                    if (i < numVMs)
                    {
                        UIElement cell = this.DrawVMCell(maxTasksPerVM, vms.ElementAt(i));
                        Grid.SetRow(cell, row);
                        Grid.SetColumn(cell, column);
                        GridHeatmap.Children.Add(cell);
                    }
                }
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new Messages.GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Draws an individual VM cell
        /// </summary>
        /// <param name="maxTasks"></param>
        /// <param name="vmInfo"></param>
        /// <returns></returns>
        private UIElement DrawVMCell(int maxTasks, IVM vmInfo)
        {
            UIElement heatmapCell = null;
            VMUIStates uiState;

            // Map all the TVM states onto few UI states/colors
            switch (vmInfo.State)
            {
                case TVMState.Creating:
                case TVMState.LeavingPool:
                case TVMState.Rebooting:
                case TVMState.Reimaging:
                case TVMState.Starting:
                case TVMState.WaitingForStartTask:
                    uiState = VMUIStates.Transitioning;
                    break;
                case TVMState.Idle:
                    uiState = VMUIStates.Idle;
                    break;
                case TVMState.Invalid:
                case TVMState.StartTaskFailed:
                case TVMState.Unknown:
                case TVMState.Unmapped:
                case TVMState.Unusable:
                    uiState = VMUIStates.Error;
                    break;
                case TVMState.Running:
                    uiState = VMUIStates.Running;
                    break;
                default:
                    uiState = VMUIStates.Unknown;
                    break;
            }

            // Use one rectangle for a TVM if MaxTasksPerVM is one or TVM can't be used
            if (maxTasks == 1 ||
                uiState == VMUIStates.Transitioning ||
                uiState == VMUIStates.Error ||
                uiState == VMUIStates.Unknown)
            {
                Brush tvmColor;
                Rectangle tvmRect = new Rectangle();
                tvmRect.Margin = new Thickness(2);

                switch (uiState)
                {
                    case VMUIStates.Error:
                        tvmColor = Brushes.Red;
                        break;
                    case VMUIStates.Idle:
                        tvmColor = Brushes.White;
                        break;
                    case VMUIStates.Running:
                        tvmColor = Brushes.LightGreen;
                        break;
                    case VMUIStates.Transitioning:
                        tvmColor = Brushes.Yellow;
                        break;
                    case VMUIStates.Unknown:
                        tvmColor = Brushes.Orange;
                        break;
                    default:
                        tvmColor = Brushes.Orange;
                        break;
                }

                tvmRect.Fill = tvmColor;
                tvmRect.StrokeThickness = 0.5;
                tvmRect.Stroke = Brushes.Gray;
                tvmRect.ToolTip = vmInfo.State.ToString();

                heatmapCell = tvmRect;
            }
            else
            {
                // MaxTasksPerVM > 1 and VM in state where could have tasks running
                // Have a grid to represent max number of tasks the VM can run at a time

                // Determine number of running tasks for the VM
                int numRunningTasks = 0;
                IEnumerable<TaskInformation> taskInfoList = vmInfo.RecentTasks;
                if (taskInfoList != null)
                {
                    foreach (TaskInformation ti in taskInfoList)
                    {
                        if (ti.TaskState == TaskState.Running)
                        {
                            numRunningTasks++;
                        }
                    }
                }

                // Create the Grid control
                Grid tvmGrid = new Grid();
                tvmGrid.Margin = new Thickness(2);

                // Calculate grid dimensions
                int numRows = (int)Math.Sqrt(maxTasks);
                int numCols = (int)Math.Truncate((maxTasks + 0.5) / numRows);

                // Add grid rows and columns
                for (int i = 0; i < numRows; i++)
                {
                    RowDefinition rowDef = new RowDefinition();
                    tvmGrid.RowDefinitions.Add(rowDef);
                }
                for (int i = 0; i < numCols; i++)
                {
                    ColumnDefinition colDef = new ColumnDefinition();
                    tvmGrid.ColumnDefinitions.Add(colDef);
                }

                // Turn appropriate number of cells green according to running number of tasks
                int cellCount = 0;
                for (int row = 0; row < numRows; row++)
                {
                    for (int col = 0; col < numCols; col++)
                    {
                        Rectangle task = new Rectangle();
                        task.Stroke = Brushes.Gray;
                        task.StrokeThickness = 0.5;

                        if (cellCount < numRunningTasks)
                        {
                            task.Fill = Brushes.LightGreen;
                        }
                        else
                        {
                            task.Fill = Brushes.White;
                        }

                        Grid.SetRow(task, row);
                        Grid.SetColumn(task, col);
                        tvmGrid.Children.Add(task);

                        cellCount++;
                    }
                }

                heatmapCell = tvmGrid;
            }

            return heatmapCell;
        }
    }
}
