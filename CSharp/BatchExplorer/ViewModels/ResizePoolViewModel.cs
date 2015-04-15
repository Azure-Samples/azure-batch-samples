using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.ViewModels
{
    public class ResizePoolViewModel : EntityBase
    {
        #region Services
        private readonly IDataProvider batchService;
        #endregion

        #region Public UI Properties
        private string poolName;
        public string PoolName
        {
            get
            {
                return this.poolName;
            }
            set
            {
                this.poolName = value;
                this.FirePropertyChangedEvent("PoolName");
            }
        }
        
        private int targetDedicated;
        public int TargetDedicated
        {
            get
            {
                return this.targetDedicated;
            }
            set
            {
                this.targetDedicated = value;
                this.FirePropertyChangedEvent("TargetDedicated");
            }
        }

        private TimeSpan? timeout;
        public TimeSpan? Timeout
        {
            get
            {
                return this.timeout;
            }
            set
            {
                this.timeout = value;
                this.FirePropertyChangedEvent("Timeout");
            }
        }

        private string deallocationOptionString;
        public string DeallocationOptionString
        {
            get
            {
                return this.deallocationOptionString;
            }
            set
            {
                this.deallocationOptionString = value;
                this.FirePropertyChangedEvent("DeallocationOptionString");
            }
        }

        private static readonly List<string> deallocationOptionValues;
        
        public List<string> DeallocationOptionValues
        {
            get { return deallocationOptionValues; }
        }

        #endregion

        static ResizePoolViewModel()
        {
            deallocationOptionValues = new List<string>();
            deallocationOptionValues.Add(string.Empty);
            deallocationOptionValues.AddRange(Enum.GetNames(typeof (TVMDeallocationOption)));
            deallocationOptionValues.Remove(TVMDeallocationOption.Invalid.ToString());
        }

        public ResizePoolViewModel(IDataProvider batchService, string poolName, int? currentDedicated)
        {
            this.batchService = batchService;

            this.PoolName = poolName;
            this.TargetDedicated = currentDedicated ?? 0;
            this.DeallocationOptionString = null;

            this.IsBusy = false;
        }

        public CommandBase ResizePool
        {
            get
            {
                return new CommandBase(
                    async (o) =>
                    {
                        this.IsBusy = true;
                        try
                        {
                            await this.ResizePoolAsync();
                        }
                        finally
                        {
                            this.IsBusy = false;
                        }
                    }
                );
            }
        }

        private async Task ResizePoolAsync()
        {
            try
            {
                TVMDeallocationOption? deallocationOption;
                if (this.IsInputValid(out deallocationOption))
                {
                    Task asyncTask = this.batchService.ResizePoolAsync(this.PoolName, this.TargetDedicated, this.Timeout, deallocationOption);

                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        asyncTask,
                        new PoolOperation(PoolOperation.Resize, poolName)));
                    await asyncTask;

                    Messenger.Default.Send(new CloseGenericPopup());
                }
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        private bool IsInputValid(out TVMDeallocationOption? deallocationOption)
        {
            deallocationOption = null;
            
            if (!string.IsNullOrEmpty(this.DeallocationOptionString))
            {
                TVMDeallocationOption innerDeallocationOption;
                bool parsedCorrectly = Enum.TryParse(this.DeallocationOptionString, out innerDeallocationOption);
                if (parsedCorrectly)
                {
                    deallocationOption = innerDeallocationOption;
                }
                else
                {
                    Messenger.Default.Send(new GenericDialogMessage("Invalid value for Deallocation Option"));
                    return false;
                }
            }
            
            if (string.IsNullOrEmpty(this.PoolName))
            {
                Messenger.Default.Send(new GenericDialogMessage("Invalid values for Pool Name"));
                return false;
            }
            else if (this.TargetDedicated < 0)
            {
                Messenger.Default.Send(new GenericDialogMessage("Invalid value for TargetDedicated"));
                return false;
            }
            
            return true;
        }
    }
}
