//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Batch;
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

        private IReadOnlyList<NodeAgentSku> availableNodeAgentSkus;
        public IReadOnlyList<NodeAgentSku> AvailableNodeAgentSkus
        {
            get
            {
                return this.availableNodeAgentSkus;
            }
            set
            {
                this.availableNodeAgentSkus = value;
                this.FirePropertyChangedEvent("availableNodeAgentSkus");
                this.FirePropertyChangedEvent("AvailablePublishers");
                this.FirePropertyChangedEvent("AvailableNodeAgentSkuIds");
                this.FirePropertyChangedEvent("AvailableImageReferences");
            }
        }
        
        public IEnumerable<string> AvailableNodeAgentSkuIds
        {
            get
            {
                return this.AvailableNodeAgentSkus.Where(
                    sku => sku.VerifiedImageReferences.Any(imageRef =>
                        imageRef.Publisher == this.Publisher && imageRef.Offer == this.offer && imageRef.Sku == this.Sku)).Select(
                            sku => sku.Id);
            }
        }

        public IEnumerable<ImageReference> AvailableImageReferences
        {
            get
            {
                return this.AvailableNodeAgentSkus.SelectMany(sku => sku.VerifiedImageReferences);
            }
        }

        public IEnumerable<string> AvailablePublishers
        {
            get { return this.AvailableImageReferences.Select(imageRef => imageRef.Publisher).Distinct(); }
        }

        public IEnumerable<string> AvailableOffers
        {
            get
            {
                return this.AvailableImageReferences.Where(
                    imageRef => imageRef.Publisher == this.Publisher).Select(
                    imageRef => imageRef.Offer).Distinct();
            }
        }

        public IEnumerable<string> AvailableSkus
        {
            get
            {
                return this.AvailableImageReferences.Where(
                    imageRef => imageRef.Publisher == this.Publisher && imageRef.Offer == this.Offer).Select(
                    imageRef => imageRef.Sku).Distinct();
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

        private string virtualSubnetId;
        public string VirtualSubnetId
        {
            get
            {
                return this.virtualSubnetId;
            }
            set
            {
                this.virtualSubnetId = value;
                this.FirePropertyChangedEvent("VirtualSubnetId");
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

        private bool hasVirtualMachineConfiguration;
        public bool HasVirtualMachineConfiguration
        {
            get
            {
                return this.hasVirtualMachineConfiguration;
            }
            set
            {
                this.hasVirtualMachineConfiguration = value;
                this.FirePropertyChangedEvent("HasVirtualMachineConfiguration");
            }
        }

        private string offer;
        public string Offer 
        {
            get
            {
                return this.offer;
            }
            set
            {
                this.offer = value;
                this.FirePropertyChangedEvent("Offer");
                this.FirePropertyChangedEvent("AvailableSkus");
                this.FirePropertyChangedEvent("AvailableNodeAgentSkuIds");
            }
        }

        private string publisher;
        public string Publisher
        {
            get
            {
                return this.publisher;
            }
            set
            {
                this.publisher = value;
                this.FirePropertyChangedEvent("Publisher");
                this.FirePropertyChangedEvent("AvailableOffers");
                this.FirePropertyChangedEvent("AvailableSkus");
                this.FirePropertyChangedEvent("AvailableNodeAgentSkuIds");
            }
        }

        private string sku;
        public string Sku
        {
            get
            {
                return this.sku;
            }
            set
            {
                this.sku = value;
                this.FirePropertyChangedEvent("Sku");
                this.FirePropertyChangedEvent("AvailableNodeAgentSkuIds");
            }
        }

        private string version;
        public string Version
        {
            get
            {
                return this.version;
            }
            set
            {
                this.version = value;
                this.FirePropertyChangedEvent("Version");
            }
        }

        private string nodeAgentSkuId;
        public string NodeAgentSkuId
        {
            get
            {
                return this.nodeAgentSkuId;
            }
            set
            {
                this.nodeAgentSkuId = value;
                this.FirePropertyChangedEvent("NodeAgentSkuId");
            }
        }

        private bool? enableWindowsAutomaticUpdates;
        public bool? EnableWindowsAutomaticUpdates
        {
            get
            {
                return this.enableWindowsAutomaticUpdates;
            }
            set
            {
                this.enableWindowsAutomaticUpdates = value;
                this.FirePropertyChangedEvent("EnableWindowsAutomaticUpdates");
            }
        }

        #endregion

        public CreatePoolViewModel(IDataProvider batchService, Cached<IList<NodeAgentSku>> nodeAgentSkus)
        {
            this.batchService = batchService;

            // pre-populate the available VM sizes
            this.AvailableVirtualMachineSizes = Common.SupportedVirtualMachineSizesList;
            this.AvailableNodeAgentSkus = new List<NodeAgentSku>(); //Initially empty

            Task task = nodeAgentSkus.GetDataAsync().ContinueWith(
                (t) => this.AvailableNodeAgentSkus = new ReadOnlyCollection<NodeAgentSku>(t.Result));
            AsyncOperationTracker.Instance.AddTrackedInternalOperation(task);

            this.Version = "latest"; //Default to latest
            this.AvailableOSVersions = new List<string> { "*" };
            this.TargetDedicated = 1;
            this.MaxTasksPerComputeNode = 1;
            this.IsBusy = false;
            this.HasVirtualMachineConfiguration = false;
            
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

                    if (!this.UseAutoscale)
                    {
                        asyncTask = this.batchService.CreatePoolAsync(
                            this.PoolId,
                            this.SelectedVirtualMachineSize,
                            this.TargetDedicated,
                            this.TargetLowPriority,
                            null,
                            this.InterComputeNodeCommunicationEnabled,
                            this.VirtualSubnetId,
                            this.GetCloudServiceConfigurationOptions(),
                            this.GetVirtualMachineConfigurationOptions(),
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
                            null,
                            this.AutoscaleFormula,
                            this.InterComputeNodeCommunicationEnabled,
                            this.VirtualSubnetId,
                            this.GetCloudServiceConfigurationOptions(),
                            this.GetVirtualMachineConfigurationOptions(),
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

        private CloudServiceConfigurationOptions GetCloudServiceConfigurationOptions()
        {
            if (!this.HasVirtualMachineConfiguration)
            {
                string osFamilyString;
                if (Common.SupportedOSFamilyDictionary.ContainsKey(this.SelectedOSFamily))
                {
                    osFamilyString = Common.SupportedOSFamilyDictionary[this.SelectedOSFamily];
                }
                else
                {
                    osFamilyString = this.SelectedOSFamily;
                }

                return new CloudServiceConfigurationOptions()
                {
                    OSFamily = osFamilyString,
                    OSVersion = this.SelectedOSVersion
                };
            }

            return null;
        }

        private VirtualMachineConfigurationOptions GetVirtualMachineConfigurationOptions()
        {
            if (this.HasVirtualMachineConfiguration)
            {
                return new VirtualMachineConfigurationOptions()
                {
                    Version = this.Version,
                    EnableWindowsAutomaticUpdates = this.EnableWindowsAutomaticUpdates,
                    NodeAgentSkuId = this.NodeAgentSkuId,
                    Offer = this.Offer,
                    Publisher = this.Publisher,
                    SkuId = this.Sku
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
