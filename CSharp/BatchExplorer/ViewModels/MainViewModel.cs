//Copyright (c) Microsoft Corporation

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.Models;
using Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.BatchExplorer.ViewModels
{
    /// <summary>
    /// The viewmodel for the main view of the UI
    /// </summary>
    public class MainViewModel : EntityBase
    {
        /// <summary>
        /// The manager for the accounts and the related data
        /// </summary>
        [ImportMany(typeof(IAccountManager))] 
        private IEnumerable<Lazy<IAccountManager, IAccountManagerMetadata>> ManagerCollection { get; set; }

        public List<AccountManagerContainer> AccountManagers { get; private set; }
        
        public Account ActiveAccount { get; private set; }

        #region Public UI Properties

        private bool poolTabIsSelected;
        /// <summary>
        /// True if the Pool Tab is selected
        /// </summary>
        public bool PoolTabIsSelected
        {
            get
            {
                return this.poolTabIsSelected;
            }
            set
            {
                this.poolTabIsSelected = value;
                FirePropertyChangedEvent("PoolTabIsSelected");
            }
        }

        private bool jobScheduleTabIsSelected;
        /// <summary>
        /// True if the Pool Tab is selected
        /// </summary>
        public bool JobScheduleTabIsSelected
        {
            get
            {
                return this.jobScheduleTabIsSelected;
            }
            set
            {
                this.jobScheduleTabIsSelected = value;
                FirePropertyChangedEvent("JobScheduleTabIsSelected");
                if (value)
                {
                    this.JobScheduleDetailTabIsSelected = true;
                }
            }
        }

        private bool jobTabIsSelected;
        /// <summary>
        /// True if the Job Tab is selected
        /// </summary>
        public bool JobTabIsSelected
        {
            get
            {
                return this.jobTabIsSelected;
            }
            set
            {
                this.jobTabIsSelected = value;
                FirePropertyChangedEvent("JobTabIsSelected");
            }
        }

        private ICollectionView jobCollection;

        /// <summary>
        /// The collection of jobs for an account
        /// </summary>
        public ICollectionView Jobs
        {
            get
            {
                return this.jobCollection;
            }
            set
            {
                this.jobCollection = value;
                FirePropertyChangedEvent("Jobs");
            }
        }

        private ICollectionView jobScheduleCollection;
        /// <summary>
        /// The collection of job schedules for an account
        /// </summary>
        public ICollectionView JobSchedules
        {
            get
            {
                return this.jobScheduleCollection;
            }
            set
            {
                this.jobScheduleCollection = value;
                FirePropertyChangedEvent("JobSchedules");
            }
        }

        private bool jobScheduleDetailTabIsSelected;
        /// <summary>
        /// True if the job schedule tab is selected
        /// </summary>
        public bool JobScheduleDetailTabIsSelected
        {
            get
            {
                return this.jobScheduleDetailTabIsSelected;
            }
            set
            {
                this.jobScheduleDetailTabIsSelected = value;
                FirePropertyChangedEvent("JobScheduleDetailTabIsSelected");
            }
        }

        private bool certificatesTabIsSelected;
        /// <summary>
        /// True if the Certificates tab is selected
        /// </summary>
        public bool CertificatesTabIsSelected
        {
            get
            {
                return this.certificatesTabIsSelected;
            }
            set
            {
                this.certificatesTabIsSelected = value;
                FirePropertyChangedEvent("CertificatesTabIsSelected");
            }
        }

        private ICollectionView certificatesCollection;

        /// <summary>
        /// The collection of certificates for an account
        /// </summary>
        public ICollectionView Certificates
        {
            get
            {
                return this.certificatesCollection;
            }
            set
            {
                this.certificatesCollection = value;
                FirePropertyChangedEvent("Certificates");
            }
        }

        private bool leftSpinnerIsVisible;
        /// <summary>
        /// Controls visibility of the wait spinner on the left
        /// </summary>
        public bool LeftSpinnerIsVisible
        {
            get
            {
                return this.leftSpinnerIsVisible;
            }
            set
            {
                this.leftSpinnerIsVisible = value;
                FirePropertyChangedEvent("LeftSpinnerIsVisible");
            }
        }

        private bool upperRightSpinnerIsVisible;
        /// <summary>
        /// Controls visibility of the wait spinner on the upper right
        /// </summary>
        public bool UpperRightSpinnerIsVisible
        {
            get
            {
                return this.upperRightSpinnerIsVisible;
            }
            set
            {
                this.upperRightSpinnerIsVisible = value;
                FirePropertyChangedEvent("UpperRightSpinnerIsVisible");
            }
        }

        private bool _lowerRightSpinnerIsVisible;
        /// <summary>
        /// Controls visibility of the wait spinnner on the lower right
        /// </summary>
        public bool LowerRightSpinnerIsVisible
        {
            get
            {
                return _lowerRightSpinnerIsVisible;
            }
            set
            {
                _lowerRightSpinnerIsVisible = value;
                FirePropertyChangedEvent("LowerRightSpinnerIsVisible");
            }
        }

        private PoolModel selectedPool;

        public PoolModel SelectedPool
        {
            get
            {
                return this.selectedPool;
            }
            set
            {
                this.selectedPool = value;
                if (this.selectedPool != null)
                {
                    //Load the ComputeNodes in this pool just in time if they haven't been loaded yet
                    if (!this.selectedPool.HasLoadedChildren)
                    {
                        this.selectedPool.RefreshAsync(ModelRefreshType.Children).ContinueWith(
                            (t) =>
                            {
                                FirePropertyChangedEvent("SelectedPool");
                                FirePropertyChangedEvent("ComputeNodesTabTitle");
                            },
                            TaskContinuationOptions.NotOnFaulted);
                    }
                    else
                    {
                        this.SelectedComputeNode = this.selectedPool.ComputeNodes.Count > 0 ? this.selectedPool.ComputeNodes[0] : null;
                        FirePropertyChangedEvent("SelectedPool");
                        FirePropertyChangedEvent("ComputeNodesTabTitle");
                    }
                }
            }
        }

        private ComputeNodeModel selectedComputeNode;
        /// <summary>
        /// The computeNode selected from the list of nodes
        /// </summary>
        public ComputeNodeModel SelectedComputeNode
        {
            get
            {
                return this.selectedComputeNode;
            }
            set
            {
                this.selectedComputeNode = value;
                if (this.selectedComputeNode != null)
                {
                    if (!this.selectedComputeNode.HasLoadedChildren)
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(
                            this.selectedComputeNode.RefreshAsync(ModelRefreshType.Children));
                    }
                }
                FirePropertyChangedEvent("SelectedComputeNode");
            }
        }

        private NodeFile selectedNodeFile;
        public NodeFile SelectedNodeFile
        {
            get
            {
                return this.selectedNodeFile;
            }
            set
            {
                this.selectedNodeFile = value;
                this.FirePropertyChangedEvent("SelectedNodeFile");
            }

        }

        private NodeFile selectedTaskFile;
        public NodeFile SelectedTaskFile
        {
            get
            {
                return this.selectedTaskFile;
            }
            set
            {
                this.selectedTaskFile = value;
                this.FirePropertyChangedEvent("SelectedTaskFile");
            }
        }

        private JobScheduleModel selectedJobSchedule;

        public JobScheduleModel SelectedJobSchedule
        {
            get
            {
                return this.selectedJobSchedule;
            }
            set
            {
                this.selectedJobSchedule = value;
                FirePropertyChangedEvent("SelectedJobSchedule");
            }
        }

        private JobModel selectedJob;

        public JobModel SelectedJob
        {
            get
            {
                return this.selectedJob;
            }
            set
            {
                this.selectedJob = value;
                if (this.selectedJob != null)
                {
                    if (!this.selectedJob.HasLoadedChildren)
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(
                            this.selectedJob.RefreshAsync(ModelRefreshType.Children).ContinueWith(
                            (t) =>
                            {
                                FirePropertyChangedEvent("SelectedJob");
                                FirePropertyChangedEvent("TasksTabTitle");
                            },
                            TaskContinuationOptions.NotOnFaulted));
                    }
                    else
                    {
                        this.SelectedTask = this.selectedJob.Tasks.Count > 0 ? this.selectedJob.Tasks[0] : null;
                        FirePropertyChangedEvent("SelectedJob");
                        FirePropertyChangedEvent("TasksTabTitle");
                    }
                }
            }
        }

        private TaskModel selectedTask;
        public TaskModel SelectedTask
        {
            get
            {
                return this.selectedTask;
            }
            set
            {
                this.selectedTask = value;
                this.FirePropertyChangedEvent("SelectedTask");
            }
        }

        private CertificateModel selectedCertificate;

        public CertificateModel SelectedCertificate
        {
            get
            {
                return this.selectedCertificate;
            }
            set
            {
                this.selectedCertificate = value;
                FirePropertyChangedEvent("SelectedCertificate");
            }
        }

        public string TitleString
        {
            get
            {
                if (this.ActiveAccount != null &&
                    this.ActiveAccount.Alias != null &&
                    this.ActiveAccount.Alias.Length > 0)
                {
                    return string.Format(CultureInfo.CurrentCulture, "Batch Explorer - {0}", this.ActiveAccount.Alias);
                }
                else
                {
                    return "Batch Explorer";
                }
            }
        }
        
        public bool IsAccountConnected
        {
            get
            {
                return this.ActiveAccount != null;
            }
        }

        public string JobsSearchFilter { get; set; }

        #endregion

        private ObservableCollection<PoolModel> pools;

        /// <summary>
        /// The collection of pools for this account
        /// </summary>
        public ICollectionView Pools { get; private set; }

        private ObservableCollection<JobScheduleModel> jobSchedules;

        private ObservableCollection<JobModel> jobs;

        private ObservableCollection<CertificateModel> certificates;

        private Task asyncOperationCompletionMonitoringTask;

        private Cached<IList<NodeAgentSku>> cachedNodeAgentSkus;

        #region Tab title binding properties
        public string JobScheduleTabTitle
        {
            get
            {
                const string jobScheduleTabPrefix = "Job Schedules";
                return this.jobSchedules == null ? jobScheduleTabPrefix : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", jobScheduleTabPrefix, this.jobSchedules.Count);
            }
        }

        public string PoolTabTitle
        {
            get
            {
                const string poolTabPrefix = "Pools";
                return this.pools == null ? poolTabPrefix : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", poolTabPrefix, this.pools.Count);
            }
        }

        public string JobTabTitle
        {
            get
            {
                const string jobTabPrefix = "Jobs";
                return this.jobs == null ? jobTabPrefix : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", jobTabPrefix, this.jobs.Count);
            }
        }

        public string CertificatesTabTitle
        {
            get
            {
                const string certsTabPrefix = "Certificates";
                return this.certificates == null ? certsTabPrefix : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", certsTabPrefix, this.certificates.Count);
            }
        }

        public string TasksTabTitle
        {
            get
            {
                const string taskTabPrefix = "Tasks";
                return this.SelectedJob == null || this.SelectedJob.Tasks == null ? taskTabPrefix : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", taskTabPrefix, this.SelectedJob.Tasks.Count);
            }
        }

        public string ComputeNodesTabTitle
        {
            get
            {
                const string computeNodeTabPrefix = "Compute Nodes";
                return SelectedPool == null || SelectedPool.ComputeNodes == null ? computeNodeTabPrefix : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", computeNodeTabPrefix, SelectedPool.ComputeNodes.Count);
            }
        }

        #endregion

        //TODO: This should NOT be static, but for now it is to allow easy access by poor 
        //TODO: models/viewmodels who are missing object model functionality and so must resort to the 
        //TODO: protocol layer.  Once we are fully OM integrated this should become a private instance variable again
        public static IDataProvider dataProvider;

        /// <summary>
        /// The parameterless constructor for the MainViewModel - currently provides dummy data if USE_DUMMY_DATA is defined
        /// </summary>
        public MainViewModel()
        {
            AddAccount = new CommandBase(this.AddAccountImpl);
            EditAccount = new CommandBase(this.EditAccountImpl);

            this.RegisterMessages();

            //TODO: Should do this all in an "onload" or something to avoid overloaded constructor work?
            Microsoft.Azure.BatchExplorer.Helpers.Common.LoadPlugins(this);
            
            this.AccountManagers = new List<AccountManagerContainer>();

            foreach (Lazy<IAccountManager, IAccountManagerMetadata> manager in this.ManagerCollection)
            {
                manager.Value.InitalizeAsync().Wait(); //TODO: Do this elsewhere and use the async method?
                this.AccountManagers.Add(new AccountManagerContainer(manager.Value, manager.Metadata));
            }
            
            JobTabIsSelected = true;
            
            //Register for async operation updates
            Messenger.Default.Register<AsyncOperationListChangedMessage>(this, (o) => this.FirePropertyChangedEvent("AsyncOperations"));

            //Begin a background thread which monitors the status of internal async operations and observes any exceptions
            asyncOperationCompletionMonitoringTask = AsyncOperationTracker.InternalOperationResultHandler();
        }

        #region Account operations

        /// <summary>
        /// Add an account
        /// </summary>
        public CommandBase AddAccount { get; }

        private void AddAccountImpl(object o)
        {
            try
            {
                AccountManagerContainer managerContainer = (AccountManagerContainer)o;

                //Make sure we are prepared to respond to a confirm AND a cancel message - don't forget to unregister both listeners
                Messenger.Default.Register<ConfirmAccountAddMessage>(this, message =>
                {
                    //We got a confirm, so extract the account from the message and add it
                    try
                    {
                        message.AccountManager.AddAccountAsync(message.AccountToAdd).Wait();
                        Messenger.Default.Send(new CloseGenericPopup()); //Inform the view to close
                    }
                    catch (Exception e)
                    {
                        Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
                    }
                });

                Messenger.Default.Register<CloseGenericPopup>(this, message =>
                {
                    Messenger.Default.Unregister<ConfirmAccountAddMessage>(this);
                    Messenger.Default.Unregister<CloseGenericPopup>(this);
                });


                Messenger.Default.Send<AddAccountMessage>(new AddAccountMessage()
                {
                    AccountDialogViewModel = new AccountDialogViewModel(managerContainer.AccountManager, managerContainer.AccountManager.OperationFactory)
                });
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Connect to an account
        /// </summary>
        public CommandBase ConnectAccount
        {
            get
            {
                return new CommandBase(
                    (selectedAccount) =>
                    {
                        try
                        {
                            dataProvider = new BasicDataProvider(selectedAccount as Account);
                            this.ActiveAccount = (selectedAccount as Account);
                            GetDataAsync(dataProvider, true, true, true, true).ContinueWith(
                                (task) =>
                                {
                                    FirePropertyChangedEvent("TitleString");
                                    FirePropertyChangedEvent("IsAccountConnected");
                                });

                            //Set up the cache functions for this account
                            this.cachedNodeAgentSkus = new Cached<IList<NodeAgentSku>>(() => dataProvider.ListNodeAgentSkusAsync(), TimeSpan.FromMinutes(10));
                        }
                        catch (Exception e)
                        {
                            Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
                        }
                    });
            }
        }

        /// <summary>
        /// Edit an account
        /// </summary>
        public CommandBase EditAccount { get; }

        private void EditAccountImpl(object selectedAccount)
        {
            try
            {
                Account account = selectedAccount as Account;
                IAccountManager accountManager = account.ParentAccountManager;

                //Make sure we are set up to respond to both a confirm AND a cancel message  - don't forget to unregister both listeners
                Messenger.Default.Register<ConfirmAccountEditMessage>(this, (message) =>
                {
                    try
                    {
                        message.AccountManager.CommitEditAsync(message.AccountToEdit).Wait();
                        Messenger.Default.Send(new CloseGenericPopup());
                    }
                    catch (Exception e)
                    {
                        Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
                    }
                });

                Messenger.Default.Register<CloseGenericPopup>(this, message =>
                {
                    Messenger.Default.Unregister<ConfirmAccountEditMessage>(this);
                    Messenger.Default.Unregister<CloseGenericPopup>(this);
                });

                Account tempAccount = accountManager.CloneAccountForEditAsync(account).Result;
                Messenger.Default.Send<EditAccountMessage>(new EditAccountMessage()
                {
                    AccountDialogViewModel = new AccountDialogViewModel(tempAccount, accountManager, accountManager.OperationFactory)
                });
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Delete an account
        /// </summary>
        public CommandBase DeleteAccount
        {
            get
            {
                return new CommandBase(
                    (selectedAccount) =>
                    {
                        try
                        {
                            Account account = selectedAccount as Account;
                            IAccountManager accountManager = account.ParentAccountManager;

                            Messenger.Default.Register<ConfirmAccountDeleteMessage>(this, (message) =>
                            {
                                accountManager.DeleteAccountAsync(account).Wait();
                                Messenger.Default.Send(new CloseGenericPopup());
                            });

                            Messenger.Default.Register<CloseGenericPopup>(this, (message) =>
                            {
                                Messenger.Default.Unregister<ConfirmAccountDeleteMessage>(this);
                                Messenger.Default.Unregister<CloseGenericPopup>(this);
                            });
                            Messenger.Default.Send<ShowDeleteWarningMessage>(new ShowDeleteWarningMessage());
                        }
                        catch (Exception e)
                        {
                            Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
                        }

                    });
            }
        }

        #endregion

        #region Refresh operations

        /// <summary>
        /// Refresh all job schedules, jobs, and pools
        /// </summary>
        public CommandBase RefreshAll
        {
            get
            {
                return new CommandBase(
                    (o) => AsyncOperationTracker.Instance.AddTrackedInternalOperation(GetDataAsync(dataProvider, true, true, true, true)));
            }
        }

        /// <summary>
        /// Refresh all job schedules
        /// </summary>
        public CommandBase RefreshJobSchedules
        {
            get
            {
                return new CommandBase(
                    (o) => AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.GetDataAsync(dataProvider, jobSchedules: true, jobs: false, pools: false, certificates: false)));
            }
        }

        /// <summary>
        /// Refresh all jobs
        /// </summary>
        public CommandBase RefreshJobs
        {
            get
            {
                return new CommandBase(
                    (o) => AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.GetDataAsync(dataProvider, jobSchedules: false, jobs: true, pools: false, certificates: false)));
            }
        }

        /// <summary>
        /// Delete selected jobs
        /// </summary>
        public CommandBase DeleteSelectedJobs
        {
            get
            {
                return new CommandBase( 
                    async (commandArg) =>
                    {
                        if (this.jobs.Any(a => a.IsChecked))
                        {
                            try
                            {
                                IEnumerable<string> deletedIds = this.jobs.Where(a => a.IsChecked).Select(s => s.Id);
                                Messenger.Default.Register<MultibuttonDialogReturnMessage>(this, async (message) =>
                                {
                                    if (message.MessageBoxResult == MessageBoxResult.Yes)
                                    {
                                        this.LeftSpinnerIsVisible = true;
                                        FirePropertyChangedEvent("LeftSpinnerIsVisible");
                                        foreach (var job in this.jobs)
                                        {
                                            if (job.IsChecked)
                                            {
                                                await job.DeleteAsync(false);
                                            }
                                        }
                                    }
                                    Messenger.Default.Unregister<MultibuttonDialogReturnMessage>(this);
                                });
                                Messenger.Default.Send<LaunchMultibuttonDialogMessage>(new LaunchMultibuttonDialogMessage()
                                {
                                    Caption = "Confirm delete",
                                    DialogMessage = deletedIds.Count() > 1 ?
                                                    string.Format("Are you sure you want to delete {0} selected jobs?", deletedIds.Count()) :
                                                    "Are you sure you want to delete the job?",
                                    MessageBoxButton = MessageBoxButton.YesNo
                                });

                                //NOTE: There is an outstanding issue where the tab count does not refresh after awating completion of asynchronous opration.
                                //So keep on refreshing until we see the deleted record inside the list or timeout the retry after 1 min, which ever is earlier.
                                bool isRefreshRequired = true;
                                DateTime startTime = DateTime.Now;
                                while (isRefreshRequired)
                                {
                                    var refreshedJobs = await dataProvider.GetJobCollectionAsync(this.JobsSearchFilter);
                                    isRefreshRequired = refreshedJobs.Any(a => deletedIds.Contains(a.Id)) && (DateTime.Now - startTime).TotalMilliseconds < 60000;
                                    if (!isRefreshRequired)
                                    {
                                        this.jobs = new ObservableCollection<JobModel>(refreshedJobs);
                                        this.jobCollection = CollectionViewSource.GetDefaultView(this.jobs);
                                        this.selectedJob = null;
                                        this.Jobs.Refresh();

                                        //Refresh the job pane
                                        FirePropertyChangedEvent("Jobs");
                                        FirePropertyChangedEvent("JobTabTitle");

                                        //Refresh the task pane
                                        FirePropertyChangedEvent("SelectedJob");
                                        FirePropertyChangedEvent("TasksTabTitle");
                                    }
                                    else
                                    {
                                        await Task.Delay(1000);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(ex.Message));
                            }
                            finally
                            {
                                this.LeftSpinnerIsVisible = false;
                                FirePropertyChangedEvent("LeftSpinnerIsVisible");
                            }
                        }
                    });
            }
        }
        
        /// <summary>
        /// Select all jobs from header checkbox
        /// </summary>
        public CommandBase SelectAllJobs
        {
            get
            {
                return new CommandBase(
                    (commandArg) =>
                    {
                        if (this.jobs != null && this.jobs.Count > 0)
                        {
                            foreach (var job in this.jobs)
                            {
                                job.IsChecked = bool.Parse(commandArg.ToString());
                            }
                        }                      
                    });
            }
        }

        /// <summary>
        /// Refresh all pools
        /// </summary>
        public CommandBase RefreshPools
        {
            get
            {
                return new CommandBase(
                    (o) => AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.GetDataAsync(dataProvider, jobSchedules: false, jobs: false, pools: true, certificates: false)));
            }
        }

        /// <summary>
        /// Refresh all tasks
        /// </summary>
        public CommandBase RefreshTasks
        {
            get
            {
                return new CommandBase(
                    (item) =>
                    {
                        var jobModel = ((JobModel)item);
                        if (jobModel != null)
                        {
                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(jobModel.RefreshAsync(ModelRefreshType.Children));
                        }
                    });
            }
        }

        /// <summary>
        /// Refresh all certificates
        /// </summary>
        public CommandBase RefreshCertificates
        {
            get
            {
                return new CommandBase(
                    (o) => AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.GetDataAsync(dataProvider, jobSchedules: false, jobs: false, pools: false, certificates: true)));
            }
        }

        #endregion

        #region VM operations

        /// <summary>
        /// Reboot the selected Compute Node
        /// </summary>
        public CommandBase RebootComputeNode
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        Messenger.Default.Register<RebootComputeNodeConfirmationMessage>(this, (message) =>
                        {
                            if (message.Confirmation == ComputeNodeRebootConfimation.Confirmed)
                            {
                                AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.SelectedComputeNode.RebootAsync());
                            }

                            Messenger.Default.Unregister<RebootComputeNodeConfirmationMessage>(this);
                        });

                        Messenger.Default.Send<RebootComputeNodeMessage>(new RebootComputeNodeMessage());
                    }
                );
            }
        }

        /// <summary>
        /// Reimage the selected Compute Node
        /// </summary>
        public CommandBase ReimageComputeNode
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        Messenger.Default.Register<ReimageComputeNodeConfirmationMessage>(this, (message) =>
                        {
                            if (message.Confirmation == ComputeNodeReimageConfimation.Confirmed)
                            {
                                AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.SelectedComputeNode.ReimageAsync());
                            }

                            Messenger.Default.Unregister<ReimageComputeNodeConfirmationMessage>(this);
                        });

                        Messenger.Default.Send<ReimageComputeNodeMessage>(new ReimageComputeNodeMessage());
                    }
                );
            }
        }

        /// <summary>
        /// Disables scheduling on the selected Compute Node
        /// </summary>
        public CommandBase DisableSchedulingComputeNode
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        Messenger.Default.Register<DisableSchedulingComputeNodeConfirmationMessage>(this, (message) =>
                        {
                            if (message.Confirmation == ComputeNodeDisableSchedulingConfimation.Confirmed)
                            {
                                AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.SelectedComputeNode.DisableSchedulingAsync());
                            }

                            Messenger.Default.Unregister<DisableSchedulingComputeNodeConfirmationMessage>(this);
                        });

                        Messenger.Default.Send(new DisableSchedulingComputeNodeMessage());
                    }
                );
            }
        }

        /// <summary>
        /// Enables scheduling on the selected Compute Node
        /// </summary>
        public CommandBase EnableSchedulingComputeNode
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.SelectedComputeNode.EnableSchedulingAsync());

                        Messenger.Default.Send(new EnableSchedulingComputeNodeMessage());
                    }
                );
            }
        }

        /// <summary>
        /// Download RDP from the selected ComputeNode and prompt the user for a save file dialog
        /// </summary>
        public CommandBase DownloadRDP
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DownloadRDPFileAsync(this.SelectedComputeNode));
                    }
                );
            }
        }

        /// <summary>
        /// Download RDP from the selected ComputeNode and open it.
        /// </summary>
        public CommandBase OpenRDP
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DownloadRDPFileAsync(this.SelectedComputeNode, Path.GetTempPath()));
                    }
                );
            }
        }

         /// <summary>
        /// Get SSH settngs for the selected ComputeNode
        /// </summary>
        public CommandBase GetSSH
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        var task = this.GetSSHSettingsAsync(this.SelectedComputeNode);
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(task);
                        task.ContinueWith(t =>
                        {
                            var sshSettings = t.Result;
                            var sshCommand = $"ssh -p {sshSettings.Port} <username>@{sshSettings.IPAddress}";
                            Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(sshCommand));
                        });
                    }
                );
            }
        }

        /// <summary>
        /// Add a user to the selected compute node
        /// </summary>
        public CommandBase AddComputeNodeUser
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        ComputeNodeModel selectedComputeNode = (ComputeNodeModel)o;
                        Messenger.Default.Send(new ShowCreateComputeNodeUserWindow(selectedComputeNode.ParentPool.Id, selectedComputeNode.Id));
                    }
                );
            }
        }

        public CommandBase EditUserOnComputeNode
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("TODO: Edit user on Compute Node"));
                    }
                );
            }
        }

        public CommandBase DeleteUserFromComputeNode
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("TODO: Delete user from Compute Node"));
                    }
                );
            }
        }

        #endregion

        #region File operations

        public CommandBase OpenTaskFile
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        try
                        {
                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DownloadFileAsync(
                            this.SelectedTask.SelectedTaskFile.Name, Path.GetTempPath(), false));
                        }
                        catch (Exception e)
                        {
                            Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
                        }
                    }
                );
            }
        }

        public CommandBase DownloadTaskFile
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        try
                        {
                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DownloadFileAsync(
                            this.SelectedTask.SelectedTaskFile.Name, isNodeFile: false));
                        }
                        catch (Exception e)
                        {
                            Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
                        }
                        
                    }
                );
            }
        }

        public CommandBase OpenNodeFile
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DownloadFileAsync(this.SelectedNodeFile.Name, Path.GetTempPath()));
                    }
                );
            }
        }

        public CommandBase DownloadNodeFile
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DownloadFileAsync(this.SelectedNodeFile.Name));
                    }
                );
            }
        }

        #endregion

        #region Task Linked Storage File Operations

        public CommandBase OpenTaskLinkedStorageFile
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        try
                        {
                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DownloadLinkedStorageFileAsync(
                            this.SelectedTask.SelectedTaskLinkedStorageFile, Path.GetTempPath()));
                        }
                        catch (Exception e)
                        {
                            Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
                        }
                    }
                );
            }
        }

        public CommandBase DownloadTaskLinkedStorageFile
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        try
                        {
                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DownloadLinkedStorageFileAsync(
                            this.SelectedTask.SelectedTaskLinkedStorageFile));
                        }
                        catch (Exception e)
                        {
                            Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
                        }

                    }
                );
            }
        }

        #endregion

        #region item-specific commands
        /// <summary>
        /// Refresh a selected item that implements IRefreshableObject
        /// </summary>
        public CommandBase RefreshItem
        {
            get
            {
                return new CommandBase(
                    (item) =>
                    {
                        var castItem = (item as ModelBase);
                        if (castItem != null)
                        {
                            castItem.RefreshAsync(ModelRefreshType.Children | ModelRefreshType.Basic).ContinueWith((t) =>
                            {
                                var job = castItem as JobModel;
                                if (job != null)
                                {
                                    FirePropertyChangedEvent("TasksTabTitle");
                                }

                                var pool = castItem as PoolModel;
                                if (pool != null)
                                {
                                    FirePropertyChangedEvent("Pools");
                                    FirePropertyChangedEvent("ComputeNodesTabTitle");
                                }

                                //Nothing to do for compute nodes
                            });
                        }
                    });
            }
        }


        /// <summary>
        /// Terminate the selected item
        /// </summary>
        public CommandBase Terminate
        {
            get
            {
                return new CommandBase(
                    (item) =>
                    {
                        var task = item as TaskModel;
                        if (task != null)
                        {
                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(task.TerminateAsync());
                        }
                    });
            }
        }

        /// <summary>
        /// Delete the selected item
        /// </summary>
        public CommandBase Delete
        {
            get
            {
                return new CommandBase(
                    (item) =>
                    {
                        var castItem = (item as ModelBase);
                        if (castItem != null)
                        {
                            string objectType = string.Empty;
                            string objectName = string.Empty;
                            string diaglogMessageFormat = "Are you sure you want to delete {0} '{1}'?";
                            var itemType = item.GetType();
                            if (itemType == typeof(PoolModel))
                            {
                                PoolModel pool = item as PoolModel;
                                objectType = "Pool";
                                objectName = pool.Id;

                                diaglogMessageFormat += "\n\nThis is a non-reversible operation which will remove all nodes and data on those nodes from your account";
                            }
                            else if (itemType == typeof(TaskModel))
                            {
                                TaskModel taskModel = item as TaskModel;

                                objectType = "Task";
                                objectName = taskModel.Id;
                            }
                            else if (itemType == typeof(JobModel))
                            {
                                JobModel jobModel = item as JobModel;

                                objectType = "Job";
                                objectName = jobModel.Id;
                            }
                            else if (itemType == typeof(JobScheduleModel))
                            {
                                JobScheduleModel jobScheduleModel = item as JobScheduleModel;

                                objectType = "Job Schedule";
                                objectName = jobScheduleModel.Id;
                            }
                            else if (itemType == typeof(CertificateModel))
                            {
                                CertificateModel certificate = item as CertificateModel;

                                objectType = "Certificate";
                                objectName = certificate.Thumbprint;
                            }

                            Messenger.Default.Register<MultibuttonDialogReturnMessage>(this, (message) =>
                                {
                                    if (message.MessageBoxResult == MessageBoxResult.Yes)
                                    {
                                        if (itemType == typeof(PoolModel))
                                        {
                                            PoolModel pool = item as PoolModel;
                                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(pool.DeleteAsync());
                                        }
                                        else if (itemType == typeof(TaskModel))
                                        {
                                            TaskModel taskModel = item as TaskModel;
                                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(taskModel.DeleteAsync());
                                        } 
                                        else if (itemType == typeof(CertificateModel))
                                        {
                                            CertificateModel certificate = item as CertificateModel;
                                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(certificate.DeleteAsync());
                                        }
                                        else if (itemType == typeof(JobScheduleModel))
                                        {
                                            JobScheduleModel jobScedule = item as JobScheduleModel;
                                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(jobScedule.DeleteAsync());
                                        }
                                    }
                                    Messenger.Default.Unregister<MultibuttonDialogReturnMessage>(this);
                                });
                            Messenger.Default.Send<LaunchMultibuttonDialogMessage>(new LaunchMultibuttonDialogMessage()
                                {
                                    Caption = "Confirm delete",
                                    DialogMessage = string.Format(diaglogMessageFormat, objectType, objectName),
                                    MessageBoxButton = MessageBoxButton.YesNo
                                });
                        }
                    });
            }
        }

        public CommandBase ResizePool
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        PoolModel pool = (PoolModel)o;
                        Messenger.Default.Send<ShowResizePoolWindow>(new ShowResizePoolWindow(pool.Id, pool.CurrentDedicated, pool.CurrentAutoScaleFormula));
                    });
            }
        }

        #endregion

        public CommandBase ShowAboutDialog
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        Messenger.Default.Send<ShowAboutWindow>(new ShowAboutWindow());
                    }
                );
            }
        }

        #region Create operations

        public CommandBase CreatePool
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        // Call a new window to show the Create Pool UI
                        Messenger.Default.Send(new ShowCreatePoolWindow(this.cachedNodeAgentSkus));
                    }
                );
            }
        }

        public CommandBase CreateJobSchedule
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        // Call a new window to show the Create Job Schedule UI
                        Messenger.Default.Send<ShowCreateJobScheduleWindow>(new ShowCreateJobScheduleWindow());
                    }
                );
            }
        }

        public CommandBase CreateJob
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        // Call a new window to show the Create Job UI
                        Messenger.Default.Send(new ShowCreateJobWindow());
                    }
                );
            }
        }

        public CommandBase CreateCertificate
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        // Call a new window to show the Create Certificate UI
                        Messenger.Default.Send(new ShowCreateCertificateWindow());
                    }
                );
            }
        }

        #endregion

        public CommandBase ShowHeatMap
        {
            get
            {
                return new CommandBase(
                    (o) =>
                        {
                            PoolModel poolModel = (o as PoolModel);
                            string poolId = poolModel.Id;

                            Task<CloudPool> getPoolTask = dataProvider.Service.GetPoolAsync(poolId);
                            AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                                getPoolTask,
                                new PoolOperation(PoolOperation.GetPool, poolId)));
                            getPoolTask.ContinueWith((asyncTask) => Messenger.Default.Send(new ShowHeatMapMessage(asyncTask.Result)));
                    }
                );
            }
        }

        public CommandBase Exit
        {
            get
            {
                return new CommandBase(x => Application.Current.Shutdown());
            }
        }

        #region AsyncOperationTracker view commands and properties

        public IEnumerable<AsyncOperationModel> AsyncOperations
        {
            get { return AsyncOperationTracker.Instance.AsyncOperations; }
        }

        public AsyncOperationModel SelectedAsyncOperation { get; set; }

        public CommandBase ViewAsyncOperationDetails
        {
            get
            {
                return new CommandBase(o =>
                    {
                        Messenger.Default.Send(new ShowAsyncOperationDetailWindow(this.SelectedAsyncOperation));
                    });
            }
        }
        public CommandBase ClearAllAsyncOperations
        {
            get
            {
                return new CommandBase(o =>
                {
                    AsyncOperationTracker.Instance.Clear();
                });
            }
        }

        #endregion

        #region Options specific commands and properties

        public CommandBase ViewOptionsDialog
        {
            get
            {
                return new CommandBase(o =>
                {
                    Messenger.Default.Send(new ShowOptionsDialogMessage());
                });
            }
        }

        #endregion

        #region Private helper methods

        private void RegisterMessages()
        {
            Messenger.Default.Register<UpdateWaitSpinnerMessage>(this, ProcessUpdateWaitSpinnerMessage);

            Messenger.Default.Register<RefreshMessage>(this,
                (message) =>
                {
                    switch (message.ItemToRefresh)
                    {
                        case RefreshTarget.Pools:
                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.GetDataAsync(dataProvider, false, false, true, false));
                            break;
                        case RefreshTarget.Jobs:
                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.GetDataAsync(dataProvider, false, true, false, false));
                            break;
                        case RefreshTarget.JobSchedules:
                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.GetDataAsync(dataProvider, true, false, false, false));
                            break;
                        case RefreshTarget.Certificates:
                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.GetDataAsync(dataProvider, false, false, false, true));
                            break;
                    }
                }
            );

            Messenger.Default.Register<ModelNotFoundAfterRefresh>(this, (message) =>
                {
                    ModelBase modelBase = message.Model;

                    JobScheduleModel jobScheduleModel = modelBase as JobScheduleModel;
                    if (jobScheduleModel != null)
                    {
                        this.jobSchedules.Remove(jobScheduleModel);
                        FirePropertyChangedEvent("JobScheduleTabTitle");
                        this.SelectedJobSchedule = null;
                    }

                    PoolModel poolModel = modelBase as PoolModel;
                    if (poolModel != null)
                    {
                        this.pools.Remove(poolModel);
                        FirePropertyChangedEvent("PoolTabTitle");
                        SelectedPool = null;
                        this.SelectedComputeNode = null;
                    }

                    JobModel jobModel = modelBase as JobModel;
                    if (jobModel != null)
                    {
                        this.jobs.Remove(jobModel);
                        this.SelectedJob = null;
                        this.FirePropertyChangedEvent("JobTabTitle");
                        this.FirePropertyChangedEvent("TasksTabTitle");
                    }

                    TaskModel taskModel = modelBase as TaskModel;
                    if (taskModel != null)
                    {
                        taskModel.ParentJob.Tasks.Remove(taskModel);
                        taskModel.ParentJob.UpdateTaskView();
                        FirePropertyChangedEvent("TasksTabTitle");
                    }

                    ComputeNodeModel vmModel = modelBase as ComputeNodeModel;
                    if (vmModel != null)
                    {
                        vmModel.ParentPool.ComputeNodes.Remove(vmModel);
                        vmModel.ParentPool.UpdateNodeView();
                        FirePropertyChangedEvent("ComputeNodesTabTitle");
                        this.selectedComputeNode = null;
                    }
                });
        }

        /// <summary>
        /// Gets a specific set of data from the Batch service
        /// </summary>
        /// <param name="provider">The provider to retrieve the data with</param>
        /// <param name="jobSchedules">True if job schedule data should be retrieved</param>
        /// <param name="jobs">True if job data should be retrieved</param>
        /// <param name="pools">True if pool data should be retrieved</param>
        /// <param name="certificates">True if certificates should be retrieved</param>
        /// <returns></returns>
        private async Task GetDataAsync(IDataProvider provider, bool jobSchedules, bool jobs, bool pools, bool certificates)
        {
            //Turn on the correct wait spinners
            LeftSpinnerIsVisible = true;
            UpperRightSpinnerIsVisible = false;
            LowerRightSpinnerIsVisible = false;

            try
            {
                //
                // Get all the job schedules
                //
                if (jobSchedules)
                {
                    System.Threading.Tasks.Task<IList<JobScheduleModel>> getJobSchedulesTask = provider.GetJobScheduleCollectionAsync();
                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        getJobSchedulesTask, 
                        new AccountOperation(AccountOperation.ListJobSchedules)));

                    this.jobSchedules = new ObservableCollection<JobScheduleModel>(await getJobSchedulesTask);
                    this.jobScheduleCollection = CollectionViewSource.GetDefaultView(this.jobSchedules);
                    
                    this.JobSchedules.Refresh();
                    FirePropertyChangedEvent("JobSchedules");
                    FirePropertyChangedEvent("JobScheduleTabTitle");
                }

                this.SelectedJobSchedule = null;

                //
                // Get all jobs
                //
                if (jobs)
                {
                    System.Threading.Tasks.Task<IList<JobModel>> getJobTask = provider.GetJobCollectionAsync(this.JobsSearchFilter);
                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        getJobTask,
                        new AccountOperation(AccountOperation.ListJobs)));

                    this.jobs = new ObservableCollection<JobModel>(await getJobTask);
                    this.jobCollection = CollectionViewSource.GetDefaultView(this.jobs);

                    this.Jobs.Refresh();
                    FirePropertyChangedEvent("Jobs");
                    FirePropertyChangedEvent("JobTabTitle");
                }

                this.SelectedJob = null;

                //
                // Get all pools
                //
                if (pools)
                {
                    System.Threading.Tasks.Task<IList<PoolModel>> getPoolsTask = provider.GetPoolCollectionAsync();
                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        getPoolsTask,
                        new AccountOperation(AccountOperation.ListPools)));

                    this.pools = new ObservableCollection<PoolModel>(await getPoolsTask);

                    Pools = CollectionViewSource.GetDefaultView(this.pools);
                    Pools.Refresh();
                    FirePropertyChangedEvent("Pools");
                    FirePropertyChangedEvent("PoolTabTitle");
                }
                SelectedPool = null;
                this.SelectedComputeNode = null;

                //
                // Get all certificates
                //
                if (certificates)
                {
                    System.Threading.Tasks.Task<IList<CertificateModel>> getCertificatesTask = provider.GetCertificatesCollectionAsync();
                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        getCertificatesTask,
                        new AccountOperation(AccountOperation.ListCertificates)));

                    this.certificates = new ObservableCollection<CertificateModel>(await getCertificatesTask);

                    Certificates = CollectionViewSource.GetDefaultView(this.certificates);
                    Certificates.Refresh();
                    FirePropertyChangedEvent("Certificates");
                    FirePropertyChangedEvent("CertificatesTabTitle");
                }
                SelectedCertificate = null;
            }
            catch (Exception e)
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
            finally
            {
                //Turn off the wait spinners
                LowerRightSpinnerIsVisible = false;
                UpperRightSpinnerIsVisible = false;
                LeftSpinnerIsVisible = false;
            }
        }

        private void ProcessUpdateWaitSpinnerMessage(UpdateWaitSpinnerMessage message)
        {
            switch (message.PanelToChange)
            {
                case WaitSpinnerPanel.Left:
                    LeftSpinnerIsVisible = message.MakeSpinnerVisible;
                    break;
                case WaitSpinnerPanel.UpperRight:
                    UpperRightSpinnerIsVisible = message.MakeSpinnerVisible;
                    break;
                case WaitSpinnerPanel.LowerRight:
                    LowerRightSpinnerIsVisible = message.MakeSpinnerVisible;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task DownloadFileAsync(string file, string localDownloadTargetPath = null, bool isNodeFile = true)
        {
            string fileName = null;
            try
            {
                if (string.IsNullOrEmpty(localDownloadTargetPath))
                {
                    fileName = ShowSaveFileDialog(Path.GetFileName(file), ".txt", "Text documents (.txt)|*.txt");
                }
                else
                {
                    fileName = Path.Combine(localDownloadTargetPath, Path.GetFileName(file));
                }

                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    // Save document
                    using (FileStream destStream = new FileStream(fileName, FileMode.Create))
                    {
                        if (isNodeFile)
                        {
                            await this.SelectedComputeNode.DownloadFileAsync(file, destStream);
                        }
                        else
                        {
                            await this.SelectedTask.GetTaskFileAsync(file, destStream);
                        }
                    }

                    // open text files
                    if (fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        Process.Start(fileName);
                    }
                }
            }
            catch (Exception e)
            {
                if (!string.IsNullOrEmpty(fileName))
                {
                    if (File.Exists(fileName))
                    {
                        File.Delete(fileName); //Delete the file if we have hit an exception
                    }
                }
                
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        private async Task DownloadLinkedStorageFileAsync(ICloudBlob blobFile, string localDownloadTargetPath = null)
        {
            string fileName = null;
            try
            {
                if (string.IsNullOrEmpty(localDownloadTargetPath))
                {
                    fileName = ShowSaveFileDialog(Path.GetFileName(blobFile.Name), ".txt", "Text documents (.txt)|*.txt");
                }
                else
                {
                    fileName = Path.Combine(localDownloadTargetPath, Path.GetFileName(blobFile.Name));
                }

                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    // Save document
                    await blobFile.DownloadToFileAsync(fileName, FileMode.Create);

                    // open text files
                    if (fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        Process.Start(fileName);
                    }
                }
            }
            catch (Exception e)
            {
                if (!string.IsNullOrEmpty(fileName))
                {
                    if (File.Exists(fileName))
                    {
                        File.Delete(fileName); //Delete the file if we have hit an exception
                    }
                }

                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        private async Task DownloadRDPFileAsync(ComputeNodeModel computeNode, string localDownloadTargetPath = null)
        {
            string fileName = null;
            if (string.IsNullOrEmpty(localDownloadTargetPath))
            {
                fileName = ShowSaveFileDialog(computeNode.Id, ".rdp", "RDP File (.rdp)|*.rdp");
            }
            else
            {
                fileName = Path.Combine(localDownloadTargetPath, Path.GetFileName(computeNode.Id) + ".rdp");
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                // Save document
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    await computeNode.DownloadRDPAsync(memoryStream);

                    memoryStream.Seek(0, SeekOrigin.Begin);

                    using (FileStream fileStream = new FileStream(fileName, FileMode.Create))
                    {
                        await memoryStream.CopyToAsync(fileStream);
                    }
                }

                // Launch
                // We don't ask for permission to launch here because the process seems
                // to do that for us. The connection is always "untrusted" and,
                // given security settings, it is likely to remain that way.
                Process.Start(fileName);
            }
        }

        /// <summary>
        /// Shows the Save File Dialog
        /// </summary>
        /// <param name="file"></param>
        /// <param name="defaultExt"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        private string ShowSaveFileDialog(string file, string defaultExt, string filter)
        {
            // Configure save file dialog box
            var saveFileDlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = Path.GetFileName(file),  // Default file name
                DefaultExt = defaultExt,            // Default file extension.
                Filter = filter                     // Filter files by extension
            };

            // Show save file dialog box
            var result = saveFileDlg.ShowDialog();
            if (result == true)
            {
                return saveFileDlg.FileName;
            }

            return null;
        }

        private async System.Threading.Tasks.Task<RemoteLoginSettings> GetSSHSettingsAsync(ComputeNodeModel computeNode)
        {
            return await computeNode.GetSSHSettingsAsync();
        }

        #endregion
    }
}
