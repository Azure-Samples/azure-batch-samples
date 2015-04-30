using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.ViewModels
{
    public class CreatePoolViewModel : EntityBase
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

        private string selectedTvmSize;
        public string SelectedTvmSize
        {
            get
            {
                return this.selectedTvmSize;
            }
            set
            {
                this.selectedTvmSize = value;
                this.FirePropertyChangedEvent("SelectedTvmSize");
            }
        }

        private List<string> availableTvmSizes;
        public List<string> AvailableTvmSizes
        {
            get
            {
                return this.availableTvmSizes;
            }
            set
            {
                this.availableTvmSizes = value;
                this.FirePropertyChangedEvent("AvailableTvmSizes");
            }
        }

        private int resizeTimeoutInMinutes;
        public int ResizeTimeoutInMinutes
        {
            get
            {
                return this.resizeTimeoutInMinutes;
            }
            set
            {
                this.resizeTimeoutInMinutes = value;
                this.FirePropertyChangedEvent("ResizeTimeoutInMinutes");
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

        private bool useAutoscale;
        public bool UseAutoscale
        {
            get
            {
                return this.useAutoscale;
            }
            set
            {
                this.useAutoscale = value;
                this.FirePropertyChangedEvent("UseAutoscale");
            }
        }

        private string autoscaleFormula;
        public string AutoscaleFormula
        {
            get
            {
                return this.autoscaleFormula;
            }
            set
            {
                this.autoscaleFormula = value;
                this.FirePropertyChangedEvent("AutoscaleFormula");
            }
        }

        private string selectedOSFamily;
        public string SelectedOSFamily
        {
            get
            {
                return this.selectedOSFamily;
            }
            set
            {
                this.selectedOSFamily = value;
                this.FirePropertyChangedEvent("SelectedOSFamily");
            }
        }

        private List<string> availableOSFamilies;
        public List<string> AvailableOSFamilies
        {
            get
            {
                return this.availableOSFamilies;
            }
            set
            {
                this.availableOSFamilies = value;
                this.FirePropertyChangedEvent("AvailableOSFamilies");
            }
        }

        private string selectedOSVersion;
        public string SelectedOSVersion
        {
            get
            {
                return this.selectedOSVersion;
            }
            set
            {
                this.selectedOSVersion = value;
                this.FirePropertyChangedEvent("SelectedOSVersion");
            }
        }

        private List<string> availableOSVersions;
        public List<string> AvailableOSVersions
        {
            get
            {
                return this.availableOSVersions;
            }
            set
            {
                this.availableOSVersions = value;
                this.FirePropertyChangedEvent("AvailableOSVersions");
            }
        }

        
        private bool useCommunication;
        public bool UseCommunication
        {
            get
            {
                return this.useCommunication;
            }
            set
            {
                this.useCommunication = value;
                this.FirePropertyChangedEvent("UseCommunication");
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

        private int maxTasksPerVM;
        public int MaxTasksPerVM
        {
            get
            {
                return this.maxTasksPerVM;
            }
            set
            {
                this.maxTasksPerVM = value;
                this.FirePropertyChangedEvent("MaxTasksPerVM");
            }
        }

        #endregion

        private static readonly Dictionary<string, string> SupportedOSFamilyDictionary = 
            new Dictionary<string, string>
        {
            {"Windows Server 2008 R2", "2"},
            {"Windows Server 2012", "3"},
            {"Windows Server 2012 R2", "4"},
        };

        private static readonly List<string> TvmSizes = 
            new List<string>
            {
                "Small", 
                "Medium",
                "Large", 
                "ExtraLarge", 
                "A5",
                "A6",
                "A7",
                "A8",
                "A9",
                "STANDARD_D1",
                "STANDARD_D2",
                "STANDARD_D3",
                "STANDARD_D4",
                "STANDARD_D11",
                "STANDARD_D12",
                "STANDARD_D13",
                "STANDARD_D14",
            };  

        public CreatePoolViewModel(IDataProvider batchService)
        {
            this.batchService = batchService;

            // pre-populate the available tvm sizes
            this.AvailableTvmSizes = TvmSizes;
            this.AvailableOSVersions = new List<string> { "*" };
            this.TargetDedicated = 1;
            this.MaxTasksPerVM = 1;
            this.IsBusy = false;
            
            this.AvailableOSFamilies = new List<string>(SupportedOSFamilyDictionary.Keys);
        }

        public CommandBase CreatePool
        {
            get
            {
                return new CommandBase(
                    async (o) =>
                    {
                        this.IsBusy = true;
                        try
                        {
                            await this.CreatePoolAsync();
                        }
                        finally
                        {
                            this.IsBusy = false;
                        }
                    }
                );
            }
        }

        private async Task CreatePoolAsync()
        {
            try
            {
                if (this.IsInputValid())
                {
                    System.Threading.Tasks.Task asyncTask;

                    string osFamilyString;
                    if (SupportedOSFamilyDictionary.ContainsKey(this.SelectedOSFamily))
                    {
                        osFamilyString = SupportedOSFamilyDictionary[this.SelectedOSFamily];
                    }
                    else
                    {
                        osFamilyString = this.SelectedOSFamily;
                    }

                    if (!this.UseAutoscale)
                    {
                        asyncTask = this.batchService.CreatePoolAsync(
                            this.PoolName,
                            this.SelectedTvmSize,
                            this.TargetDedicated,
                            null,
                            this.UseCommunication,
                            osFamilyString,
                            this.SelectedOSVersion,
                            this.MaxTasksPerVM,
                            this.Timeout);
                    }
                    else
                    {
                        asyncTask = this.batchService.CreatePoolAsync(
                            this.PoolName,
                            this.SelectedTvmSize,
                            null,
                            this.AutoscaleFormula,
                            this.UseCommunication,
                            osFamilyString,
                            this.SelectedOSVersion,
                            this.MaxTasksPerVM,
                            this.Timeout);
                    }

                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        asyncTask,
                        new PoolOperation(PoolOperation.AddPool, poolName)));
                    await asyncTask;

                    Messenger.Default.Send<RefreshMessage>(new RefreshMessage(RefreshTarget.Pools));

                    Messenger.Default.Send(new GenericDialogMessage(string.Format("Successfully created pool {0}", this.PoolName)));
                    this.PoolName = string.Empty; //So that the user cannot accidentally try to create the same pool twice
                }
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        private bool IsInputValid()
        {
            if (string.IsNullOrEmpty(this.PoolName))
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid values for Pool Name"));
                return false;
            }
            else if (this.UseAutoscale)
            {
                if (string.IsNullOrEmpty(this.AutoscaleFormula))
                {
                    Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid value for Autoscale Formula"));
                    return false;
                }
            }
            else if (this.TargetDedicated < 0)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid values for Target Dedicated"));
                return false;
            }

            return true;
        }
    }
}
