//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using GalaSoft.MvvmLight.Messaging;
    using Microsoft.Azure.BatchExplorer.Helpers;
    using Microsoft.Azure.BatchExplorer.Messages;
    using Microsoft.Azure.BatchExplorer.Models;
    using Common = Helpers.Common;

    public class CreateJobViewModel : EntityBase
    {
        #region Services
        private readonly IDataProvider batchService;
        #endregion

        #region Public UI Properties
        // Basic job schedule details

        private string jobId;
        public string JobId
        {
            get
            {
                return this.jobId;
            }
            set
            {
                this.jobId = value;
                this.FirePropertyChangedEvent("JobId");
                this.FirePropertyChangedEvent("IsCreateJobButtonEnabled");
            }
        }

        private string priority;
        public string Priority
        {
            get
            {
                return this.priority;
            }
            set
            {
                this.priority = value;
                this.FirePropertyChangedEvent("Priority");
            }
        }

        private string maxRetryCount;
        public string MaxRetryCount
        {
            get
            {
                return this.maxRetryCount;
            }
            set
            {
                this.maxRetryCount = value;
                this.FirePropertyChangedEvent("MaxRetryCount");
            }
        }
        
        private bool isCreateJobManagerSelected;
        public bool IsCreateJobManagerSelected
        {
            get
            {
                return this.isCreateJobManagerSelected;
            }
            set
            {
                this.isCreateJobManagerSelected = value;
                this.FirePropertyChangedEvent("IsCreateJobManagerSelected");
            }
        }

        private TimeSpan? maxWallClockTime;
        public TimeSpan? MaxWallClockTime
        {
            get
            {
                return this.maxWallClockTime;
            }
            set
            {
                this.maxWallClockTime = value;
                this.FirePropertyChangedEvent("MaxWallClockTime");
            }
        }

        // Job Manager
        private string jobManagerId;
        public string JobManagerId
        {
            get
            {
                return this.jobManagerId;
            }
            set
            {
                this.jobManagerId = value;
                this.FirePropertyChangedEvent("JobManagerId");
            }
        }

        private string commandLine;
        public string CommandLine
        {
            get
            {
                return this.commandLine;
            }
            set
            {
                this.commandLine = value;
                this.FirePropertyChangedEvent("CommandLine");
            }
        }

        private string maxTaskRetryCount;
        public string MaxTaskRetryCount
        {
            get
            {
                return this.maxTaskRetryCount;
            }
            set
            {
                this.maxTaskRetryCount = value;
                this.FirePropertyChangedEvent("MaxTaskRetryCount");
            }
        }

        private TimeSpan? maxTaskWallClockTime;
        public TimeSpan? MaxTaskWallClockTime
        {
            get
            {
                return this.maxTaskWallClockTime;
            }
            set
            {
                this.maxTaskWallClockTime = value;
                this.FirePropertyChangedEvent("MaxWallClockTime");
            }
        }

        private TimeSpan? retentionTime;
        public TimeSpan? RetentionTime
        {
            get
            {
                return this.retentionTime;
            }
            set
            {
                this.retentionTime = value;
                this.FirePropertyChangedEvent("RetentionTime");
            }
        }

        private List<bool> availableKillOnCompletionOptions;
        public List<bool> AvailableKillOnCompletionOptions
        {
            get
            {
                return this.availableKillOnCompletionOptions;
            }
            set
            {
                this.availableKillOnCompletionOptions = value;
                this.FirePropertyChangedEvent("AvailableKillOnCompletionOptions");
            }
        }

        private bool killOnCompletionSelectedItem;
        public bool KillOnCompletionSelectedItem
        {
            get
            {
                return this.killOnCompletionSelectedItem;
            }
            set
            {
                this.killOnCompletionSelectedItem = value;
                this.FirePropertyChangedEvent("KillOnCompletionSelectedItem");
            }
        }

        // Pool Settings
        private bool useAutoPool;
        public bool UseAutoPool
        {
            get
            {
                return this.useAutoPool;
            }
            set
            {
                this.useAutoPool = value;
                this.FirePropertyChangedEvent("UseAutoPool");
            }
        }

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

        private string autoPoolPrefix;
        public string AutoPoolPrefix
        {
            get
            {
                return this.autoPoolPrefix;
            }
            set
            {
                this.autoPoolPrefix = value;
                this.FirePropertyChangedEvent("AutoPoolPrefix");
            }
        }

        private List<string> availableLifeTimeOptions;
        public List<string> AvailableLifeTimeOptions
        {
            get
            {
                return this.availableLifeTimeOptions;
            }
            set
            {
                this.availableLifeTimeOptions = value;
                this.FirePropertyChangedEvent("AvailableLifeTimeOptions");
            }
        }

        private string selectedLifetimeOption;
        public string SelectedLifetimeOption
        {
            get
            {
                return this.selectedLifetimeOption;
            }
            set
            {
                this.selectedLifetimeOption = value;
                this.FirePropertyChangedEvent("SelectedLifetimeOption");
            }
        }


        private List<bool> availableKeepAliveOptions;
        public List<bool> AvailableKeepAliveOptions
        {
            get
            {
                return this.availableKeepAliveOptions;
            }
            set
            {
                this.availableKeepAliveOptions = value;
                this.FirePropertyChangedEvent("AvailableKeepAliveOptions");
            }
        }

        private bool selectedKeepAliveItem;
        public bool SelectedKeepAliveItem
        {
            get
            {
                return this.selectedKeepAliveItem;
            }
            set
            {
                this.selectedKeepAliveItem = value;
                this.FirePropertyChangedEvent("SelectedKeepAliveItem");
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

        private int targetLowPriority;
        public int TargetLowPriority
        {
            get
            {
                return this.targetLowPriority;
            }
            set
            {
                this.targetLowPriority = value;
                this.FirePropertyChangedEvent("TargetLowPriority");
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

        public bool IsCreateJobButtonEnabled
        {
            get
            {
                return (!string.IsNullOrEmpty(this.JobId));
            }
        }
        #endregion

        #region Commands
        public CommandBase CreateJobSchedule
        {
            get
            {
                return new CommandBase(
                    async (o) =>
                    {
                        try
                        {
                            this.IsBusy = true;
                            await this.CreateJobScheduleAsync();
                        }
                        finally
                        {
                            this.IsBusy = false;
                        }
                    }
                );
            }
        }

        public CommandBase CancelCreateJobSchedule
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        // TODO: Close window
                    }
                );
            }
        }
        #endregion

        public CreateJobViewModel(IDataProvider batchService)
        {
            this.batchService = batchService;
            this.IsBusy = false;

            // pre-populate values
            this.AvailableKeepAliveOptions = new List<bool> { false, true };
            this.AvailableLifeTimeOptions = new List<string> { "Job" };
            this.AvailableKillOnCompletionOptions = new List<bool> { true, false };

            this.SelectedKeepAliveItem = this.AvailableKeepAliveOptions[0];
            this.SelectedLifetimeOption = this.AvailableLifeTimeOptions[0];
            this.KillOnCompletionSelectedItem = this.AvailableKillOnCompletionOptions[0];

            this.AvailableVirtualMachineSizes = Common.SupportedVirtualMachineSizesList;
            this.AvailableOSFamilies = new List<string>(Common.SupportedOSFamilyDictionary.Keys);
        }

        private async Task CreateJobScheduleAsync()
        {
            try
            {
                if (this.IsInputValid())
                {
                    string osFamilyString = null;

                    if (this.UseAutoPool)
                    {
                        if (Common.SupportedOSFamilyDictionary.ContainsKey(this.SelectedOSFamily))
                        {
                            osFamilyString = Common.SupportedOSFamilyDictionary[this.SelectedOSFamily];
                        }
                        else
                        {
                            osFamilyString = this.SelectedOSFamily;
                        }
                    }

                    CreateJobOptions options = new CreateJobOptions()
                    {
                        CreateJobManager = this.IsCreateJobManagerSelected,
                        MaxRetryCount = Common.GetNullableIntValue(this.MaxRetryCount),
                        MaxWallClockTime = this.MaxWallClockTime,
                        Priority = Common.GetNullableIntValue(this.Priority),
                        JobId = this.JobId,
                        PoolId = this.PoolId,
                        AutoPoolOptions = new CreateAutoPoolOptions()
                        {
                            AutoPoolPrefix = this.AutoPoolPrefix,
                            LifeTimeOption = this.SelectedLifetimeOption,
                            KeepAlive = this.SelectedKeepAliveItem,
                            OSFamily = osFamilyString,
                            SelectedLifetimeOption = this.SelectedLifetimeOption,
                            TargetDedicated = this.TargetDedicated,
                            TargetLowPriority = this.TargetLowPriority,
                            UseAutoPool = this.UseAutoPool,
                            VirutalMachineSize = this.SelectedVirtualMachineSize
                        },
                        JobManagerOptions = new CreateJobManagerOptions()
                        {
                            CommandLine = this.CommandLine,
                            JobManagerId = this.JobManagerId,
                            KillOnCompletion = this.KillOnCompletionSelectedItem,
                            MaxTaskRetryCount = Common.GetNullableIntValue(this.MaxRetryCount),
                            MaxTaskWallClockTime = this.MaxTaskWallClockTime,
                            RetentionTime = this.RetentionTime
                        }
                    };

                    await this.batchService.CreateJobAsync(options);

                    Messenger.Default.Send<RefreshMessage>(new RefreshMessage(RefreshTarget.Jobs));

                    Messenger.Default.Send(new GenericDialogMessage(string.Format("Successfully created job {0}", this.JobId)));
                    this.JobId = string.Empty; //So that the user cannot accidentally try to create the same job schedule twice
                }
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        private bool IsInputValid()
        {
            // TODO: Validate string to int inputs. e.g. Retry Count should be an int
            if (string.IsNullOrEmpty(this.JobId))
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid values for job Id"));
                return false;
            }
            
            if (!this.UseAutoPool && string.IsNullOrEmpty(this.PoolId))
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid values for Pool Id"));
                return false;
            }

            if (this.UseAutoPool)
            {
                if (string.IsNullOrEmpty(this.SelectedVirtualMachineSize))
                {
                    Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Auto pool requires virtual machine size to be specified"));
                    return false;
                }
                if (string.IsNullOrEmpty(this.SelectedOSFamily))
                {
                    Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Auto pool requires OS family to be specified"));
                    return false;
                }
            }

            if (this.IsCreateJobManagerSelected)
            {
                if (string.IsNullOrEmpty(this.JobManagerId))
                {
                    Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid values for Job Manager Id"));
                    return false;
                }
                if (string.IsNullOrEmpty(this.CommandLine))
                {
                    Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid values for Command Line"));
                    return false;
                }
            }

            return true;
        }
    }
}
