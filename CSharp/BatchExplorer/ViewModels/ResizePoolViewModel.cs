//Copyright (c) Microsoft Corporation

using System;
using System.Collections.Generic;
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
        private string poolId;
        public string PoolId
        {
            get
            {
                return this.poolId;
            }
            set
            {
                this.poolId = value;
                this.FirePropertyChangedEvent("PoolId");
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

        private string autoScaleFormula;
        public string AutoScaleFormula
        {
            get
            {
                return this.autoScaleFormula;
            }
            set
            {
                this.autoScaleFormula = value;
                this.FirePropertyChangedEvent("AutoScaleFormula");
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

        private bool isAutoScale;
        public bool IsAutoScale
        {
            get
            {
                return this.isAutoScale;
            }
            set
            {
                this.isAutoScale = value;
                this.FirePropertyChangedEvent("IsAutoScale");
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
            deallocationOptionValues.AddRange(Enum.GetNames(typeof (ComputeNodeDeallocationOption)));
        }

        public ResizePoolViewModel(IDataProvider batchService, string poolId, int? currentDedicated, string currentAutoScaleFormula)
        {
            this.batchService = batchService;

            this.PoolId = poolId;
            this.TargetDedicated = currentDedicated ?? 0;
            this.DeallocationOptionString = null;
            if (!string.IsNullOrEmpty(currentAutoScaleFormula))
            {
                this.IsAutoScale = true;
            }

            this.AutoScaleFormula = currentAutoScaleFormula ?? string.Format("$TargetDedicated={0};",currentDedicated ?? 1);
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

        public CommandBase EnableAutoScale
        {
            get
            {
                return new CommandBase(
                    async (o) =>
                    {
                        this.IsBusy = true;
                        try
                        {
                            await this.EnableAutoScaleAsync();
                        }
                        finally
                        {
                            this.IsBusy = false;
                        }
                    }
                );
            }
        }

        public CommandBase Evaluate
        {
            get
            {
                return new CommandBase(
                    async (o) =>
                    {
                        this.IsBusy = true;
                        try
                        {
                            await this.EvaluateAsync();
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
                ComputeNodeDeallocationOption? deallocationOption;
                if (this.IsInputValid(out deallocationOption))
                {
                    Task asyncTask = this.batchService.ResizePoolAsync(this.PoolId, this.TargetDedicated, this.Timeout, deallocationOption);                   

                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        asyncTask,
                        new PoolOperation(PoolOperation.Resize, this.poolId)));
                     
                    Messenger.Default.Send(new CloseGenericPopup());

                    await asyncTask;                                    
                }
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        private async Task EnableAutoScaleAsync()
        {
            try
            {
                if (this.IsAutoScale && IsInputValidForAutoScaling())
                {
                    Task asyncTask = this.batchService.EnableAutoScaleAsync(this.PoolId, this.AutoScaleFormula);

                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        asyncTask,
                        new PoolOperation(PoolOperation.EnableAutoScale, this.poolId)));

                    await asyncTask;
                    Messenger.Default.Send(new CloseGenericPopup());
                }
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        private async Task EvaluateAsync()
        {
            try
            {
                if (this.IsAutoScale && IsInputValidForAutoScaling())
                {
                    Task<string> asyncTask = this.batchService.EvaluateAutoScaleFormulaAsync(this.PoolId, this.AutoScaleFormula);

                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        asyncTask,
                        new PoolOperation(PoolOperation.EvaluateAutoScaleFormula, this.poolId)));
                    
                    string result = await asyncTask;

                    Messenger.Default.Send(new GenericDialogMessage(result));
                }
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        private bool IsInputValidForAutoScaling()
        {         
            if (string.IsNullOrWhiteSpace(this.AutoScaleFormula))
            {
                Messenger.Default.Send(new GenericDialogMessage("Auto scale formula cannot be empty"));
                return false;
            }

            return true;
        }

        private bool IsInputValid(out ComputeNodeDeallocationOption? deallocationOption)
        {
            deallocationOption = null;
            
            if (!string.IsNullOrEmpty(this.DeallocationOptionString))
            {
                ComputeNodeDeallocationOption innerDeallocationOption;
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
            
            if (string.IsNullOrEmpty(this.PoolId))
            {
                Messenger.Default.Send(new GenericDialogMessage("Invalid values for Pool Id"));
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
