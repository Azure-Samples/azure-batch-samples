﻿using System;
using System.Collections.Generic;
﻿using System.ComponentModel;
﻿using System.Windows.Data;
using System.Threading.Tasks;
﻿using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;
﻿using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;

namespace Microsoft.Azure.BatchExplorer.Models
{
    /// <summary>
    /// The data model for the Pool object
    /// </summary>
    public class PoolModel : ModelBase
    {
        #region Public properties

        /// <summary>
        /// The set of compute nodes associated with this pool
        /// </summary>
        public List<ComputeNodeModel> ComputeNodes { get; private set; }

        /// <summary>
        /// The id of this pool
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string Id { get { return this.Pool.Id; } }

        /// <summary>
        /// Creation time of the pool
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public DateTime? CreationTime { get { return this.Pool.CreationTime; } }

        /// <summary>
        /// State of the pool
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public PoolState? State { get { return this.Pool.State; } }

        /// <summary>
        /// The size of ComputeNodes in this pool
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string VirtualMachineSize { get { return this.Pool.VirtualMachineSize; } }

        /// <summary>
        /// The current dedicated size of this pool
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public int? CurrentDedicated { get { return this.Pool.CurrentDedicated; } }

        /// <summary>
        /// The pool allocation state
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public AllocationState? AllocationState { get { return this.Pool.AllocationState; } }

        /// <summary>
        /// The ComputeNode collection associated with this pool
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public ICollectionView ComputeNodeCollection { get; set; }


        #endregion

        private CloudPool Pool { get; set; }

        /// <summary>
        /// Create a pool model from the pool cache entity
        /// </summary>
        public PoolModel(CloudPool pool)
        {
            this.Pool = pool;
            this.LastUpdatedTime = DateTime.UtcNow;
            this.ComputeNodes = new List<ComputeNodeModel>();

            this.ComputeNodeCollection = CollectionViewSource.GetDefaultView(this.ComputeNodes);
            this.UpdateNodeView();
        }

        #region ModelBase implementation

        public override List<PropertyModel> PropertyModel
        {
            get
            {
                return this.ObjectToPropertyModel(this.Pool);
            }
        }

        public override async Task RefreshAsync(ModelRefreshType refreshType, bool showTrackedOperation = true)
        {
            if (refreshType.HasFlag(ModelRefreshType.Basic))
            {
                try
                {
                    Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.Left, true));
                    Task asyncTask = this.Pool.RefreshAsync();
                    if (showTrackedOperation)
                    {
                        AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                            asyncTask,
                            new PoolOperation(PoolOperation.Refresh, this.Pool.Id)));
                    }
                    else
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(asyncTask);
                    }

                    await asyncTask;
                    this.LastUpdatedTime = DateTime.UtcNow;

                    //
                    // Fire property change events for this models properties
                    //
                    this.FireChangesOnRefresh(ModelRefreshType.Basic);
                }
                catch (Exception e)
                {
                    this.HandleException(e);
                }
                finally
                {
                    Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.Left, false));
                }
            }

            if (refreshType.HasFlag(ModelRefreshType.Children))
            {
                try
                {
                    Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.UpperRight, true));

                    //Set this before the children load so that on revisit we know we have loaded the children (or are in the process)
                    this.HasLoadedChildren = true;

                    Task<List<ComputeNodeModel>> asyncTask = this.ListComputeNodesAsync();
                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        asyncTask,
                        new PoolOperation(PoolOperation.ListComputeNodes, this.Pool.Id)));
                    this.ComputeNodes = await asyncTask;

                    this.ComputeNodeCollection = CollectionViewSource.GetDefaultView(this.ComputeNodes);
                    this.UpdateNodeView();
                }
                catch (Exception e)
                {
                    this.HasLoadedChildren = false; //On exception, we failed to load children so try again next time
                    this.HandleException(e);
                }
                finally
                {
                    Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.UpperRight, false));
                }
            }

            Messenger.Default.Send(new PoolUpdateCompleteMessage());
        }

        #endregion

        #region Operations on pool

        /// <summary>
        /// Delete this pool from the server
        /// </summary>
        public async Task DeleteAsync()
        {
            try
            {
                Task asyncTask = this.Pool.DeleteAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new PoolOperation(PoolOperation.Delete, this.Pool.Id)));
                await asyncTask;
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Resize this pool
        /// </summary>
        public async Task ResizeAsync(int target, TimeSpan timeout, ComputeNodeDeallocationOption deallocationOption)
        {
            try
            {
                Task asyncTask = this.Pool.ResizeAsync(target, timeout, deallocationOption);
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new PoolOperation(PoolOperation.Resize, this.Pool.Id)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, showTrackedOperation: false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        private async Task<List<ComputeNodeModel>> ListComputeNodesAsync()
        {
            List<ComputeNodeModel> results = new List<ComputeNodeModel>();
            IPagedEnumerable<ComputeNode> jobList = this.Pool.ListComputeNodes(OptionsModel.Instance.ListDetailLevel);

            await jobList.ForEachAsync(item => results.Add(new ComputeNodeModel(this, item)));

            return results;
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Updates associated list of ComputeNodes view
        /// </summary>
        public void UpdateNodeView()
        {
            this.FireChangesOnRefresh(ModelRefreshType.Children);
        }

        #endregion

        #region Private methods

        private void HandleException(Exception e)
        {
            //Swallow 404's and fire a message
            if (Microsoft.Azure.BatchExplorer.Helpers.Common.IsExceptionNotFound(e))
            {
                Messenger.Default.Send(new ModelNotFoundAfterRefresh(this));
            }
            else
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        #endregion
    }
}
