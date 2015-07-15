using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;

namespace Microsoft.Azure.BatchExplorer.Models
{
    /// <summary>
    /// Model representing the heatmap
    /// </summary>
    public class HeatMapModel : EntityBase
    {
        public string VirtualMachineSize
        {
            get { return this.Pool.VirtualMachineSize; }
        }

        public int? CurrentDedicated
        {
            get { return this.Pool.CurrentDedicated; }
        }

        public int? TargetDedicated
        {
            get { return this.Pool.TargetDedicated; }
        }

        public string AllocationState
        {
            get { return this.Pool.AllocationState.ToString(); }
        }

        public string PoolId
        {
            get { return this.Pool.Id; }
        }

        public int? MaxTasksPerComputeNode
        {
            get { return this.Pool.MaxTasksPerComputeNode; }
        }

        public int SchedulableComputeNodes { get; private set; }

        public int RunningTasks { get; private set; }
        
        public List<ComputeNode> ComputeNodes
        {
            get { return this.computeNodes; }
            private set { this.computeNodes = value; }
        }

        private List<ComputeNode> computeNodes;
        private CloudPool Pool { get; set; }

        public HeatMapModel(CloudPool pool)
        {
            this.Pool = pool;
            this.computeNodes = new List<ComputeNode>();
        }

        /// <summary>
        /// Refreshes the state of the HeatMap model from the Batch service.
        /// </summary>
        /// <returns></returns>
        public async Task RefreshAsync()
        {
            try
            {
                //Refresh the pool
                Task refreshPoolTask = this.Pool.RefreshAsync();

                AsyncOperationTracker.Instance.AddTrackedOperation(
                    new AsyncOperationModel(refreshPoolTask, new PoolOperation(PoolOperation.GetPool, this.PoolId)));

                //Refresh the VM list
                Task refreshComputeNodesTask = this.RefreshComputeNodesAsync();

                AsyncOperationTracker.Instance.AddTrackedOperation(
                    new AsyncOperationModel(refreshComputeNodesTask, new PoolOperation(PoolOperation.ListComputeNodes, this.PoolId)));

                await refreshComputeNodesTask;

                this.UpdateFinished();
            }
            catch (Exception e)
            {
                this.HandleException(e);
            }
        }

        #region Private methods

        /// <summary>
        /// Refreshes the status of the ComputeNodes from the Batch service.
        /// </summary>
        /// <returns></returns>
        private async Task RefreshComputeNodesAsync()
        {
            // Get the list of pool compute nodess - can't use this because ComputeNode does not support RecentTasks property yet
            DetailLevel detailLevel = new ODATADetailLevel()
                {
                    SelectClause = "recentTasks,state,id"
                };

            IPagedEnumerable<ComputeNode> computeNodeEnumerableAsync = this.Pool.ListComputeNodes(detailLevel);
            List<ComputeNode> computeNodeList = await computeNodeEnumerableAsync.ToListAsync();
            
            this.RunningTasks = 0;
            this.SchedulableComputeNodes = 0;
            foreach (ComputeNode computeNode in computeNodeList)
            {
                if (computeNode.State == ComputeNodeState.Idle || computeNode.State == ComputeNodeState.Running)
                {
                    this.SchedulableComputeNodes++;
                }

                if (computeNode.State == ComputeNodeState.Running && this.Pool.MaxTasksPerComputeNode == 1)
                {
                    this.RunningTasks++;
                }
                else if (this.Pool.MaxTasksPerComputeNode > 1)
                {
                    IEnumerable<TaskInformation> taskInfoList = computeNode.RecentTasks;
                    if (taskInfoList != null)
                    {
                        foreach (TaskInformation ti in taskInfoList)
                        {
                            if (ti.TaskState == TaskState.Running)
                            {
                                this.RunningTasks++;
                            }
                        }
                    }
                }
            }

            Interlocked.Exchange(ref this.computeNodes, computeNodeList); //Threadsafe swap
        }

        /// <summary>
        /// Fires the associated set of property changes when the update is finished.
        /// </summary>
        private void UpdateFinished()
        {
            //Fire set of property changed events
            this.FirePropertyChangedEvent("SchedulableComputeNodes");
            this.FirePropertyChangedEvent("RunningTasks");
            this.FirePropertyChangedEvent("ComputeNodes");
            this.FirePropertyChangedEvent("Pool");
            this.FirePropertyChangedEvent("PoolId");
            this.FirePropertyChangedEvent("TargetDedicated");
            this.FirePropertyChangedEvent("CurrentDedicated");
            this.FirePropertyChangedEvent("AllocationState");
            this.FirePropertyChangedEvent("VirtualMachineSize");
        }
        
        private void HandleException(Exception e)
        {
            //Swallow 404's
            if (Microsoft.Azure.BatchExplorer.Helpers.Common.IsExceptionNotFound(e))
            {
                Messenger.Default.Send(new GenericDialogMessage(string.Format("Pool {0} was deleted. The heatmap has stopped updating", this.PoolId)));
                //Swallow it and terminate the heat map
                throw new HeatMapTerminatedException();
            }
            else
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        #endregion
    }
}
