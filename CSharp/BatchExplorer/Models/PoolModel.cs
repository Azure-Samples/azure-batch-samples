﻿using System;
using System.Collections.Generic;
﻿using System.ComponentModel;
﻿using System.Windows.Data;
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
        /// The set of TVMs associated with this pool
        /// </summary>
        public List<TvmModel> Tvms { get; private set; }

        /// <summary>
        /// The name of this pool
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string Name { get { return this.Pool.Name; } }

        /// <summary>
        /// Creation time of the pool
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public DateTime CreationTime { get { return this.Pool.CreationTime; } }

        /// <summary>
        /// State of the pool
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public PoolState State { get { return this.Pool.State; } }

        /// <summary>
        /// The size of VMs in this pool
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public string VMSize { get { return this.Pool.VMSize; } }

        /// <summary>
        /// The current dedicated size of this pool
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public int? CurrentDedicated { get { return this.Pool.CurrentDedicated; } }

        /// <summary>
        /// The pool allocation state
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public AllocationState AllocationState { get { return this.Pool.AllocationState; } }

        /// <summary>
        /// The Tvm collection associated with this pool
        /// </summary>
        [ChangeTracked(ModelRefreshType.Children)]
        public ICollectionView TvmCollection { get; set; }

        /// <summary>
        /// The statistics associated with this pool
        /// </summary>
        [ChangeTracked(ModelRefreshType.Basic)]
        public SortedDictionary<string, object> Statistics
        {
            get
            {
                SortedDictionary<string, object> results = ObjectToSortedDictionary(this.Pool.Statistics);
                return results;
            }
        }

        #endregion

        private ICloudPool Pool { get; set; }

        /// <summary>
        /// Create a pool model from the pool cache entity
        /// </summary>
        public PoolModel(ICloudPool pool)
        {
            this.Pool = pool;
            this.LastUpdatedTime = DateTime.UtcNow;
            this.Tvms = new List<TvmModel>();

            this.TvmCollection = CollectionViewSource.GetDefaultView(this.Tvms);
            this.UpdateTvmView();
        }

        #region ModelBase implementation

        public override SortedDictionary<string, object> PropertyValuePairs
        {
            get
            {
                SortedDictionary<string, object> results = ObjectToSortedDictionary(this.Pool);
                results.Add(LastUpdateFromServerString, this.LastUpdatedTime);
                return results;
            }
        }

        public override async System.Threading.Tasks.Task RefreshAsync(ModelRefreshType refreshType, bool showTrackedOperation = true)
        {
            if (refreshType.HasFlag(ModelRefreshType.Basic))
            {
                try
                {
                    Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.Left, true));
                    System.Threading.Tasks.Task asyncTask = this.Pool.RefreshAsync();
                    if (showTrackedOperation)
                    {
                        AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                            asyncTask,
                            new PoolOperation(PoolOperation.Refresh, this.Pool.Name)));
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

                    System.Threading.Tasks.Task<List<TvmModel>> asyncTask = this.ListVMsAsync();
                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        asyncTask,
                        new PoolOperation(PoolOperation.ListVMs, this.Pool.Name)));
                    this.Tvms = await asyncTask;

                    this.TvmCollection = CollectionViewSource.GetDefaultView(this.Tvms);
                    this.UpdateTvmView();
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
        public async System.Threading.Tasks.Task DeleteAsync()
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.Pool.DeleteAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new PoolOperation(PoolOperation.Delete, this.Pool.Name)));
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
        public async System.Threading.Tasks.Task ResizeAsync(int target, TimeSpan timeout, TVMDeallocationOption deallocationOption)
        {
            try
            {
                System.Threading.Tasks.Task asyncTask = this.Pool.ResizeAsync(target, timeout, deallocationOption);
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new PoolOperation(PoolOperation.Resize, this.Pool.Name)));
                await asyncTask;
                await this.RefreshAsync(ModelRefreshType.Basic, showTrackedOperation: false);
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        private async System.Threading.Tasks.Task<List<TvmModel>> ListVMsAsync()
        {
            List<TvmModel> results = new List<TvmModel>();
            IEnumerableAsyncExtended<IVM> jobList = this.Pool.ListVMs(OptionsModel.Instance.ListDetailLevel);
            IAsyncEnumerator<IVM> asyncEnumerator = jobList.GetAsyncEnumerator();

            while (await asyncEnumerator.MoveNextAsync())
            {
                results.Add(new TvmModel(this, asyncEnumerator.Current));
            }

            return results;
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Updates associated list of TVMs view
        /// </summary>
        public void UpdateTvmView()
        {
            this.FireChangesOnRefresh(ModelRefreshType.Children);
        }

        #endregion

        #region Private methods

        private void HandleException(Exception e)
        {
            //Swallow 404's and fire a message
            if (Common.IsExceptionNotFound(e))
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
