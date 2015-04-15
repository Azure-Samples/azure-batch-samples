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
        public string VMSize
        {
            get { return this.Pool.VMSize; }
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

        public string PoolName
        {
            get { return this.Pool.Name; }
        }

        public int? MaxTasksPerVM
        {
            get { return this.Pool.MaxTasksPerVM; }
        }

        public int SchedulableVMs { get; private set; }
        public int RunningTasks { get; private set; }
        public List<IVM> VMs
        {
            get { return this.vms; }
            private set { this.vms = value; }
        }

        private List<IVM> vms;
        private ICloudPool Pool { get; set; }

        public HeatMapModel(ICloudPool pool)
        {
            this.Pool = pool;
            this.vms = new List<IVM>();
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
                    new AsyncOperationModel(refreshPoolTask, new PoolOperation(PoolOperation.GetPool, this.PoolName)));

                //Refresh the VM list
                Task refreshVMsTask = this.RefreshVMsAsync();

                AsyncOperationTracker.Instance.AddTrackedOperation(
                    new AsyncOperationModel(refreshVMsTask, new PoolOperation(PoolOperation.ListVMs, this.PoolName)));

                await refreshVMsTask;

                this.UpdateFinished();
            }
            catch (Exception e)
            {
                this.HandleException(e);
            }
        }

        #region Private methods

        /// <summary>
        /// Refreshes the status of the VMs from the Batch service.
        /// </summary>
        /// <returns></returns>
        private async Task RefreshVMsAsync()
        {
            // Get the list of pool VM's - can't use this because IVM does not support RecentTasks property yet
            DetailLevel detailLevel = new ODATADetailLevel()
                {
                    SelectClause = "recentTasks,state,name"
                };

            IEnumerableAsyncExtended<IVM> vmEnumerableAsync = this.Pool.ListVMs(detailLevel);
            IAsyncEnumerator<IVM> enumerator = vmEnumerableAsync.GetAsyncEnumerator();
            List<IVM> vmList = new List<IVM>();

            while (await enumerator.MoveNextAsync())
            {
                vmList.Add(enumerator.Current);
            }
            
            this.RunningTasks = 0;
            this.SchedulableVMs = 0;
            foreach (IVM poolVM in vmList)
            {
                if (poolVM.State == TVMState.Idle || poolVM.State == TVMState.Running)
                {
                    this.SchedulableVMs++;
                }

                if (poolVM.State == TVMState.Running && this.Pool.MaxTasksPerVM == 1)
                {
                    this.RunningTasks++;
                }
                else if (this.Pool.MaxTasksPerVM > 1)
                {
                    IEnumerable<TaskInformation> taskInfoList = poolVM.RecentTasks;
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

            Interlocked.Exchange(ref this.vms, vmList); //Threadsafe swap
        }

        /// <summary>
        /// Fires the associated set of property changes when the update is finished.
        /// </summary>
        private void UpdateFinished()
        {
            //Fire set of property changed events
            this.FirePropertyChangedEvent("SchedulableVMs");
            this.FirePropertyChangedEvent("RunningTasks");
            this.FirePropertyChangedEvent("VMs");
            this.FirePropertyChangedEvent("Pool");
            this.FirePropertyChangedEvent("PoolName");
            this.FirePropertyChangedEvent("TargetDedicated");
            this.FirePropertyChangedEvent("CurrentDedicated");
            this.FirePropertyChangedEvent("AllocationState");
            this.FirePropertyChangedEvent("VMSize");
        }
        
        private void HandleException(Exception e)
        {
            //Swallow 404's
            if (Common.IsExceptionNotFound(e))
            {
                Messenger.Default.Send(new GenericDialogMessage(string.Format("Pool {0} was deleted. The heatmap has stopped updating", this.PoolName)));
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
