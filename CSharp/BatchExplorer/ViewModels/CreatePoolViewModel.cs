namespace Microsoft.Azure.BatchExplorer.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using GalaSoft.MvvmLight.Messaging;
    using Microsoft.Azure.BatchExplorer.Helpers;
    using Microsoft.Azure.BatchExplorer.Messages;
    using Microsoft.Azure.BatchExplorer.Models;
    using Common = Helpers.Common;

    public class CreatePoolViewModel : EntityBase
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

        private string selectedVirtualMachineSize;
        public string SelectedVirtualMachineSize
        {
            get
            {
                return this.selectedVirtualMachineSize;
            }
            set
            {
                this.selectedVirtualMachineSize = value;
                this.FirePropertyChangedEvent("selectedVirtualMachineSize");
            }
        }

        private IReadOnlyList<string> availableVirtualMachineSizes;
        public IReadOnlyList<string> AvailableVirtualMachineSizes
        {
            get
            {
                return this.availableVirtualMachineSizes;
            }
            set
            {
                this.availableVirtualMachineSizes = value;
                this.FirePropertyChangedEvent("availableVirtualMachineSizes");
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

        
        private bool interComputeNodeCommunicationEnabled;
        public bool InterComputeNodeCommunicationEnabled
        {
            get
            {
                return this.interComputeNodeCommunicationEnabled;
            }
            set
            {
                this.interComputeNodeCommunicationEnabled = value;
                this.FirePropertyChangedEvent("InterComputeNodeCommunicationEnabled");
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

        private int maxTasksPerComputeNode;
        public int MaxTasksPerComputeNode
        {
            get
            {
                return this.maxTasksPerComputeNode;
            }
            set
            {
                this.maxTasksPerComputeNode = value;
                this.FirePropertyChangedEvent("MaxTasksPerComputeNode");
            }
        }

        private bool hasStartTask;
        public bool HasStartTask
        {
            get
            {
                return this.hasStartTask;
            }
            set
            {
                this.hasStartTask = value;
                this.FirePropertyChangedEvent("HasStartTask");
            }
        }

        private string startTaskCommandLine;
        public string StartTaskCommandLine
        {
            get
            {
                return this.startTaskCommandLine;
            }
            set
            {
                this.startTaskCommandLine = value;
                this.FirePropertyChangedEvent("StartTaskCommandLine");
            }
        }

        private string startTaskResourceFiles;
        public string StartTaskResourceFiles
        {
            get
            {
                return this.startTaskResourceFiles;
            }
            set
            {
                this.startTaskResourceFiles = value;
                this.FirePropertyChangedEvent("StartTaskResourceFiles");
            }
        }

        private bool startTaskRunElevated;
        public bool StartTaskRunElevated
        {
            get
            {
                return this.startTaskRunElevated;
            }
            set
            {
                this.startTaskRunElevated = value;
                this.FirePropertyChangedEvent("StartTaskRunElevated");
            }
        }
        #endregion


        public CreatePoolViewModel(IDataProvider batchService)
        {
            this.batchService = batchService;

            // pre-populate the available VM sizes
            this.AvailableVirtualMachineSizes = Common.SupportedVirtualMachineSizesList;
            this.AvailableOSVersions = new List<string> { "*" };
            this.TargetDedicated = 1;
            this.MaxTasksPerComputeNode = 1;
            this.IsBusy = false;
            
            this.AvailableOSFamilies = new List<string>(Common.SupportedOSFamilyDictionary.Keys);
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
                    if (Common.SupportedOSFamilyDictionary.ContainsKey(this.SelectedOSFamily))
                    {
                        osFamilyString = Common.SupportedOSFamilyDictionary[this.SelectedOSFamily];
                    }
                    else
                    {
                        osFamilyString = this.SelectedOSFamily;
                    }

                    if (!this.UseAutoscale)
                    {
                        asyncTask = this.batchService.CreatePoolAsync(
                            this.PoolId,
                            this.SelectedVirtualMachineSize,
                            this.TargetDedicated,
                            null,
                            this.InterComputeNodeCommunicationEnabled,
                            osFamilyString,
                            this.SelectedOSVersion,
                            this.MaxTasksPerComputeNode,
                            this.Timeout,
                            this.GetStartTaskOptions());
                    }
                    else
                    {
                        asyncTask = this.batchService.CreatePoolAsync(
                            this.PoolId,
                            this.SelectedVirtualMachineSize,
                            null,
                            this.AutoscaleFormula,
                            this.InterComputeNodeCommunicationEnabled,
                            osFamilyString,
                            this.SelectedOSVersion,
                            this.MaxTasksPerComputeNode,
                            this.Timeout,
                            this.GetStartTaskOptions());
                    }

                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        asyncTask,
                        new PoolOperation(PoolOperation.AddPool, this.poolId)));
                    await asyncTask;

                    Messenger.Default.Send<RefreshMessage>(new RefreshMessage(RefreshTarget.Pools));

                    Messenger.Default.Send(new GenericDialogMessage(string.Format("Successfully created pool {0}", this.PoolId)));
                    this.PoolId = string.Empty; //So that the user cannot accidentally try to create the same pool twice
                }
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        private StartTaskOptions GetStartTaskOptions()
        {
            if (HasStartTask)
            {
                return new StartTaskOptions
                {
                    CommandLine = this.StartTaskCommandLine,
                    ResourceFiles = ResourceFileStringParser.Parse(this.StartTaskResourceFiles).Files.ToList(),
                    RunElevated = this.StartTaskRunElevated,
                };
            }

            return null;
        }

        private bool IsInputValid()
        {
            if (string.IsNullOrEmpty(this.PoolId))
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid values for Pool Id"));
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
            else if (this.HasStartTask)
            {
                if (string.IsNullOrEmpty(this.StartTaskCommandLine))
                {
                    Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Start task must have a command line"));
                    return false;
                }
                var resourceFiles = ResourceFileStringParser.Parse(this.StartTaskResourceFiles);
                if (resourceFiles.HasErrors)
                {
                    Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(String.Join(Environment.NewLine, resourceFiles.Errors)));
                    return false;
                }
            }

            return true;
        }
    }
}
