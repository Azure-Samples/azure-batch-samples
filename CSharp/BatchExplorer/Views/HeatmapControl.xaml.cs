//Copyright (c) Microsoft Corporation

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.ViewModels;

namespace Microsoft.Azure.BatchExplorer.Views
{
    public partial class HeatMapControl : UserControl
    {
        private CancellationTokenSource CancellationTokenSource { get; set; }
        private CancellationTokenSource SleepCancellationTokenSource { get; set; }

        private readonly HeatMapViewModel viewModel;
        private Task pendingRefresh;

        private enum ComputeNodeUIStates
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

                    //Snapshot the compute node list
                    List<ComputeNode> vms = new List<ComputeNode>(this.viewModel.HeatMap.ComputeNodes);
                    int maxTasksPerVM = this.viewModel.HeatMap.MaxTasksPerComputeNode.Value;

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
        /// Renders the heat map data grid view given the specified list of compute nodes and the maxTasksPerNode
        /// </summary>
        /// <param name="computeNodes"></param>
        /// <param name="maxTasksPerVM"></param>
        private void DrawHeatMap(List<ComputeNode> computeNodes, int maxTasksPerVM)
        {
            try
            {
                // Reset the grid to cater for periodic refresh
                this.GridHeatmap.ColumnDefinitions.Clear();
                this.GridHeatmap.RowDefinitions.Clear();
                this.GridHeatmap.Children.Clear();
                
                // Get pool information required to draw heatmap
                int numVMs = computeNodes.Count;

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

                // Fill in the grid cells for the pool compute nodes
                for (int i = 0; i < numVMs; i++)
                {
                    int row = i / numCols; 
                    int column = i % numCols;

                    if (i < numVMs)
                    {
                        UIElement cell = this.DrawVMCell(maxTasksPerVM, computeNodes.ElementAt(i));
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
        /// Draws an individual compute node cell
        /// </summary>
        /// <param name="maxTasks"></param>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        private UIElement DrawVMCell(int maxTasks, ComputeNode nodeInfo)
        {
            UIElement heatmapCell = null;
            ComputeNodeUIStates uiState;

            // Map all the node states onto few UI states/colors
            switch (nodeInfo.State)
            {
                case ComputeNodeState.Creating:
                case ComputeNodeState.LeavingPool:
                case ComputeNodeState.Rebooting:
                case ComputeNodeState.Reimaging:
                case ComputeNodeState.Starting:
                case ComputeNodeState.WaitingForStartTask:
                    uiState = ComputeNodeUIStates.Transitioning;
                    break;
                case ComputeNodeState.Idle:
                    uiState = ComputeNodeUIStates.Idle;
                    break;
                case ComputeNodeState.Invalid:
                case ComputeNodeState.StartTaskFailed:
                case ComputeNodeState.Unknown:
                case ComputeNodeState.Unmapped:
                case ComputeNodeState.Unusable:
                    uiState = ComputeNodeUIStates.Error;
                    break;
                case ComputeNodeState.Running:
                    uiState = ComputeNodeUIStates.Running;
                    break;
                default:
                    uiState = ComputeNodeUIStates.Unknown;
                    break;
            }

            // Use one rectangle for a node if MaxTasksPerComputeNode is one or node can't be used
            if (maxTasks == 1 ||
                uiState == ComputeNodeUIStates.Transitioning ||
                uiState == ComputeNodeUIStates.Error ||
                uiState == ComputeNodeUIStates.Unknown)
            {
                Brush nodeColor;
                Rectangle nodeRect = new Rectangle();
                nodeRect.Margin = new Thickness(2);

                switch (uiState)
                {
                    case ComputeNodeUIStates.Error:
                        nodeColor = Brushes.Red;
                        break;
                    case ComputeNodeUIStates.Idle:
                        nodeColor = Brushes.White;
                        break;
                    case ComputeNodeUIStates.Running:
                        nodeColor = Brushes.LightGreen;
                        break;
                    case ComputeNodeUIStates.Transitioning:
                        nodeColor = Brushes.Yellow;
                        break;
                    case ComputeNodeUIStates.Unknown:
                        nodeColor = Brushes.Orange;
                        break;
                    default:
                        nodeColor = Brushes.Orange;
                        break;
                }

                nodeRect.Fill = nodeColor;
                nodeRect.StrokeThickness = 0.5;
                nodeRect.Stroke = Brushes.Gray;
                nodeRect.ToolTip = nodeInfo.State.ToString();

                heatmapCell = nodeRect;
            }
            else
            {
                // MaxTasksPerComputeNode > 1 and compute node in state where could have tasks running
                // Have a grid to represent max number of tasks the compute node can run at a time

                // Determine number of running tasks for the compute node
                int numRunningTasks = 0;
                IEnumerable<TaskInformation> taskInfoList = nodeInfo.RecentTasks;
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
                Grid nodeGrid = new Grid();
                nodeGrid.Margin = new Thickness(2);

                // Calculate grid dimensions
                int numRows = (int)Math.Sqrt(maxTasks);
                int numCols = (int)Math.Truncate((maxTasks + 0.5) / numRows);

                // Add grid rows and columns
                for (int i = 0; i < numRows; i++)
                {
                    RowDefinition rowDef = new RowDefinition();
                    nodeGrid.RowDefinitions.Add(rowDef);
                }
                for (int i = 0; i < numCols; i++)
                {
                    ColumnDefinition colDef = new ColumnDefinition();
                    nodeGrid.ColumnDefinitions.Add(colDef);
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
                        nodeGrid.Children.Add(task);

                        cellCount++;
                    }
                }

                heatmapCell = nodeGrid;
            }

            return heatmapCell;
        }
    }
}
