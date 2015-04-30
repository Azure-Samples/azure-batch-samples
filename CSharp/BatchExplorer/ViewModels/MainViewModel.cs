using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.Models;
using Microsoft.Azure.BatchExplorer.PluginInterfaces.AccountPlugin;

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

        private bool workitemTabIsSelected;
        /// <summary>
        /// True if the Pool Tab is selected
        /// </summary>
        public bool WorkItemTabIsSelected
        {
            get
            {
                return this.workitemTabIsSelected;
            }
            set
            {
                this.workitemTabIsSelected = value;
                FirePropertyChangedEvent("WorkItemTabIsSelected");
                if (value)
                {
                    WorkItemDetailTabIsSelected = true;
                }
            }
        }

        private bool jobTabIsSelected;
        /// <summary>
        /// True if the Pool Tab is selected
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

        private ICollectionView workItemCollection;
        /// <summary>
        /// The collection of work items for an account
        /// </summary>
        public ICollectionView WorkItems
        {
            get
            {
                return this.workItemCollection;
            }
            set
            {
                this.workItemCollection = value;
                FirePropertyChangedEvent("WorkItems");
            }
        }

        private bool workitemDetailTabIsSelected;
        /// <summary>
        /// True if the Pool Tab is selected
        /// </summary>
        public bool WorkItemDetailTabIsSelected
        {
            get
            {
                return this.workitemDetailTabIsSelected;
            }
            set
            {
                this.workitemDetailTabIsSelected = value;
                FirePropertyChangedEvent("WorkItemDetailTabIsSelected");
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
                    //Load the TVMs in this pool just in time if they haven't been loaded yet
                    if (!this.selectedPool.HasLoadedChildren)
                    {
                        this.selectedPool.RefreshAsync(ModelRefreshType.Children).ContinueWith(
                            (t) =>
                            {
                                FirePropertyChangedEvent("SelectedPool");
                                FirePropertyChangedEvent("VMsTabTitle");
                            },
                            TaskContinuationOptions.NotOnFaulted);
                    }
                    else
                    {
                        SelectedTVM = this.selectedPool.Tvms.Count > 0 ? this.selectedPool.Tvms[0] : null;
                    }
                }
                FirePropertyChangedEvent("SelectedPool");
                FirePropertyChangedEvent("VMsTabTitle");
            }
        }

        private TvmModel selectedTVM;
        /// <summary>
        /// The tvm selected from the list of tvms
        /// </summary>
        public TvmModel SelectedTVM
        {
            get
            {
                return this.selectedTVM;
            }
            set
            {
                this.selectedTVM = value;
                if (this.selectedTVM != null)
                {
                    if (!this.selectedTVM.HasLoadedChildren)
                    {
                        this.selectedTVM.RefreshAsync(ModelRefreshType.Children);
                    }
                }
                FirePropertyChangedEvent("SelectedTVM");
            }
        }

        private ITaskFile selectedTvmFile;
        public ITaskFile SelectedTvmFile
        {
            get
            {
                return this.selectedTvmFile;
            }
            set
            {
                this.selectedTvmFile = value;
                this.FirePropertyChangedEvent("SelectedTvmFile");
            }

        }

        private ITaskFile _selectedTaskFile;
        public ITaskFile SelectedTaskFile
        {
            get
            {
                return this._selectedTaskFile;
            }
            set
            {
                this._selectedTaskFile = value;
                this.FirePropertyChangedEvent("SelectedTaskFile");
            }
        }

        private WorkItemModel selectedWorkItem;

        public WorkItemModel SelectedWorkItem
        {
            get
            {
                return this.selectedWorkItem;
            }
            set
            {
                this.selectedWorkItem = value;
                FirePropertyChangedEvent("SelectedWorkItem");
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
        
        #endregion

        private ObservableCollection<PoolModel> pools;

        /// <summary>
        /// The collection of pools for this account
        /// </summary>
        public ICollectionView Pools { get; private set; }

        private ObservableCollection<WorkItemModel> workItems;

        private Task asyncOperationCompletionMonitoringTask; 

        #region Tab title binding properties
        public string WorkItemTabTitle
        {
            get
            {
                const string workItemsTabPrefix = "Work Items";
                return this.workItems == null ? workItemsTabPrefix : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", workItemsTabPrefix, this.workItems.Count);
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
                // May not make sense to add the count here since we load incrementally. May be confusing to the user.
                return jobTabPrefix;
            }
        }

        public string VMsTabTitle
        {
            get
            {
                const string vmTabPrefix = "VMs";
                return SelectedPool == null || SelectedPool.Tvms == null ? vmTabPrefix : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", vmTabPrefix, SelectedPool.Tvms.Count);
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
            this.RegisterMessages();

            //TODO: Should do this all in an "onload" or something to avoid overloaded constructor work?
            Common.LoadPlugins(this);
            
            this.AccountManagers = new List<AccountManagerContainer>();

            foreach (Lazy<IAccountManager, IAccountManagerMetadata> manager in this.ManagerCollection)
            {
                manager.Value.InitalizeAsync().Wait(); //TODO: Do this elsewhere and use the async method?
                this.AccountManagers.Add(new AccountManagerContainer(manager.Value, manager.Metadata));
            }
            
            JobTabIsSelected = true;
            
            FirePropertyChangedEvent("WorkItemTabTitle");

            //Register for async operation updates
            Messenger.Default.Register<AsyncOperationListChangedMessage>(this, (o) => this.FirePropertyChangedEvent("AsyncOperations"));

            //Begin a background thread which monitors the status of internal async operations and observes any exceptions
            asyncOperationCompletionMonitoringTask = AsyncOperationTracker.InternalOperationResultHandler();
        }
        
        #region Account operations

        /// <summary>
        /// Add an account
        /// </summary>
        public CommandBase AddAccount
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        try
                        {
                            AccountManagerContainer managerContainer = (AccountManagerContainer)o;
                        
                            //Make sure we are prepared to respond to a confirm AND a cancel message - don't forget to unregister both listeners
                            Messenger.Default.Register<ConfirmAccountAddMessage>(this, (message) =>
                                {
                                    //We got a confirm, so extract the account from the message and add it
                                    try
                                    {
                                        managerContainer.AccountManager.AddAccountAsync(message.AccountToAdd).Wait();
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
                                AccountDialogViewModel = new AccountDialogViewModel(managerContainer.AccountManager.OperationFactory)
                            });
                        }
                        catch (Exception e)
                        {
                            Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
                        }
                    });
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
                            GetDataAsync(dataProvider, true, true, true).ContinueWith(
                                (task) =>
                                {
                                    FirePropertyChangedEvent("TitleString");
                                    FirePropertyChangedEvent("IsAccountConnected");
                                });
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
        public CommandBase EditAccount
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

                            //Make sure we are set up to respond to both a confirm AND a cancel message  - don't forget to unregister both listeners
                            Messenger.Default.Register<ConfirmAccountEditMessage>(this, (message) =>
                            {
                                try
                                {
                                    accountManager.CommitEditAsync(message.AccountToEdit).Wait();
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
                                AccountDialogViewModel = new AccountDialogViewModel(tempAccount, accountManager.OperationFactory)
                            });
                        }
                        catch (Exception e)
                        {
                            Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
                        }
                    });
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
        /// Refresh all workitems, jobs, and pools
        /// </summary>
        public CommandBase RefreshAll
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        //Listen for the user response to the warning we are about to send
                        Messenger.Default.Register<MultibuttonDialogReturnMessage>(this, (message) =>
                            {
                                if (message.MessageBoxResult == MessageBoxResult.Yes)
                                {
                                    AsyncOperationTracker.Instance.AddTrackedInternalOperation(GetDataAsync(dataProvider, true, true, true));
                                }
                                Messenger.Default.Unregister<MultibuttonDialogReturnMessage>(this);
                            });
                        //send the warning
                        Messenger.Default.Send<LaunchMultibuttonDialogMessage>(new LaunchMultibuttonDialogMessage()
                        {
                            Caption="Confirm Refresh",
                            DialogMessage = "Warning: Refresh All will cause a large amount of data to be transfered and may entail an extended wait time - do you want to proceed?",
                            MessageBoxButton = MessageBoxButton.YesNo,
                            MessageBoxImage=MessageBoxImage.Warning
                        });
                    }
                    );
            }
        }

        /// <summary>
        /// Refresh all work items
        /// </summary>
        public CommandBase RefreshWorkItems
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(GetDataAsync(dataProvider, true, false, false));
                    }
                );
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
                    (o) =>
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(GetDataAsync(dataProvider, false, false, true));
                    }
                );
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

        #endregion

        #region VM operations

        /// <summary>
        /// Reboot the selected TVM
        /// </summary>
        public CommandBase RebootTVM
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        Messenger.Default.Register<RebootTvmConfirmationMessage>(this, (message) =>
                        {
                            if (message.Confirmation == TvmRebootConfimation.Confirmed)
                            {
                                AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.SelectedTVM.RebootAsync());
                            }

                            Messenger.Default.Unregister<RebootTvmConfirmationMessage>(this);
                        });

                        Messenger.Default.Send<RebootTvmMessage>(new RebootTvmMessage());
                    }
                );
            }
        }

        /// <summary>
        /// Reimage the selected TVM
        /// </summary>
        public CommandBase ReimageTVM
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        Messenger.Default.Register<ReimageTvmConfirmationMessage>(this, (message) =>
                        {
                            if (message.Confirmation == TvmReimageConfimation.Confirmed)
                            {
                                AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.SelectedTVM.ReimageAsync());
                            }

                            Messenger.Default.Unregister<ReimageTvmConfirmationMessage>(this);
                        });

                        Messenger.Default.Send<ReimageTvmMessage>(new ReimageTvmMessage());
                    }
                );
            }
        }

        /// <summary>
        /// Download RDP from the selected TVM and prompt the user for a save file dialog
        /// </summary>
        public CommandBase DownloadRDP
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DownloadRDPFileAsync(this.SelectedTVM));
                    }
                );
            }
        }

        /// <summary>
        /// Download RDP from the selected TVM and open it.
        /// </summary>
        public CommandBase OpenRDP
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DownloadRDPFileAsync(this.SelectedTVM, Path.GetTempPath()));
                    }
                );
            }
        }

        /// <summary>
        /// Add a user to the selected VM
        /// </summary>
        public CommandBase AddVMUser
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        TvmModel selectedTVM = (TvmModel)o;
                        Messenger.Default.Send(new ShowCreateVMUserWindow(selectedTVM.ParentPool.Name, selectedTVM.Name));
                    }
                );
            }
        }

        public CommandBase EditUserOnTVM
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("TODO: Edit user on TVM"));
                    }
                );
            }
        }

        public CommandBase DeleteUserFromTVM
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("TODO: Delete user from TVM"));
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
                            this.SelectedWorkItem.SelectedJob.SelectedTask.SelectedTaskFile.Name, Path.GetTempPath(), false));
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
                            this.SelectedWorkItem.SelectedJob.SelectedTask.SelectedTaskFile.Name, isTvmFile: false));
                        }
                        catch (Exception e)
                        {
                            Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
                        }
                        
                    }
                );
            }
        }

        public CommandBase OpenTvmFile
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DownloadFileAsync(this.SelectedTvmFile.Name, Path.GetTempPath()));
                    }
                );
            }
        }

        public CommandBase DownloadTvmFile
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.DownloadFileAsync(this.SelectedTvmFile.Name));
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
                                //Nothing to do for tasks

                                var pool = castItem as PoolModel;
                                if (pool != null)
                                {
                                    FirePropertyChangedEvent("Pools");
                                    FirePropertyChangedEvent("VMsTabTitle");
                                }

                                //Nothing to do for VMs
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
                            Messenger.Default.Register<MultibuttonDialogReturnMessage>(this, (message) =>
                                {
                                    if (message.MessageBoxResult == MessageBoxResult.Yes)
                                    {
                                        var itemType = item.GetType();
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
                                        else if (itemType == typeof (TvmModel))
                                        {
                                            throw new NotImplementedException("Not implemented");
                                        }
                                    }
                                    Messenger.Default.Unregister<MultibuttonDialogReturnMessage>(this);
                                });
                            Messenger.Default.Send<LaunchMultibuttonDialogMessage>(new LaunchMultibuttonDialogMessage()
                                {
                                    Caption = "Confirm delete",
                                    DialogMessage = "Do you want to delete this item?",
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
                        Messenger.Default.Send<ShowResizePoolWindow>(new ShowResizePoolWindow(pool.Name, pool.CurrentDedicated));
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
                        Messenger.Default.Send(new ShowCreatePoolWindow());
                    }
                );
            }
        }

        public CommandBase CreateWorkItem
        {
            get
            {
                return new CommandBase(
                    (o) =>
                    {
                        // Call a new window to show the Create Work Item UI
                        Messenger.Default.Send<ShowCreateWorkItemWindow>(new ShowCreateWorkItemWindow());
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
                            string poolName = poolModel.Name;

                            Task<ICloudPool> getPoolTask = dataProvider.Service.GetPoolAsync(poolName);
                            AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                                getPoolTask,
                                new PoolOperation(PoolOperation.GetPool, poolName)));
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
                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.GetDataAsync(dataProvider, false, false, true));
                            break;
                        case RefreshTarget.WorkItems:
                            AsyncOperationTracker.Instance.AddTrackedInternalOperation(this.GetDataAsync(dataProvider, true, false, false));
                            break;
                    }
                }
            );

            Messenger.Default.Register<ModelNotFoundAfterRefresh>(this, (message) =>
                {
                    ModelBase modelBase = message.Model;

                    WorkItemModel workItemModel = modelBase as WorkItemModel;
                    if (workItemModel != null)
                    {
                        this.workItems.Remove(workItemModel);
                        FirePropertyChangedEvent("WorkItemTabTitle");
                        SelectedWorkItem = null;
                    }

                    PoolModel poolModel = modelBase as PoolModel;
                    if (poolModel != null)
                    {
                        this.pools.Remove(poolModel);
                        FirePropertyChangedEvent("PoolTabTitle");
                        SelectedPool = null;
                        SelectedTVM = null;
                    }

                    JobModel jobModel = modelBase as JobModel;
                    if (jobModel != null)
                    {
                        jobModel.ParentWorkItem.Jobs.Remove(jobModel);
                        FirePropertyChangedEvent("JobTabTitle");
                    }

                    TaskModel taskModel = modelBase as TaskModel;
                    if (taskModel != null)
                    {
                        taskModel.ParentJob.Tasks.Remove(taskModel);
                        taskModel.ParentJob.UpdateTaskView();
                        FirePropertyChangedEvent("TasksTabTitle");
                    }

                    TvmModel vmModel = modelBase as TvmModel;
                    if (vmModel != null)
                    {
                        vmModel.ParentPool.Tvms.Remove(vmModel);
                        vmModel.ParentPool.UpdateTvmView();
                        FirePropertyChangedEvent("VMsTabTitle");
                        selectedTVM = null;
                    }
                });
        }

        /// <summary>
        /// Gets a specific set of data from the Batch service
        /// </summary>
        /// <param name="provider">The provider to retrieve the data with</param>
        /// <param name="workItems">True if work item data should be retrieved</param>
        /// <param name="jobs">True if job data should be retrieved</param>
        /// <param name="pools">True if pool data should be retrieved</param>
        /// <returns></returns>
        private async Task GetDataAsync(IDataProvider provider, bool workItems, bool jobs, bool pools)
        {
            //Turn on the correct wait spinners
            LeftSpinnerIsVisible = true;
            UpperRightSpinnerIsVisible = false;
            LowerRightSpinnerIsVisible = false;

            try
            {
                //
                // Get all the work items
                //
                if (workItems)
                {
                    System.Threading.Tasks.Task getWorkItems = System.Threading.Tasks.Task.Factory.StartNew(() =>
                    {
                        this.workItems = new ObservableCollection<WorkItemModel>(provider.GetWorkItemCollection());
                    });
                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        getWorkItems, 
                        new AccountOperation(AccountOperation.ListWorkItems)));

                    await getWorkItems;
                    this.workItemCollection = CollectionViewSource.GetDefaultView(this.workItems);
                    
                    WorkItems.Refresh();
                    FirePropertyChangedEvent("WorkItems");
                    FirePropertyChangedEvent("WorkItemTabTitle");
                }

                SelectedWorkItem = null;

                //
                // Get all pools
                //
                if (pools)
                {
                    System.Threading.Tasks.Task getPools = System.Threading.Tasks.Task.Factory.StartNew(() =>
                    {
                        this.pools = new ObservableCollection<PoolModel>(provider.GetPoolCollection());
                    });
                    AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                        getPools,
                        new AccountOperation(AccountOperation.ListPools)));

                    await getPools;

                    Pools = CollectionViewSource.GetDefaultView(this.pools);
                    Pools.Refresh();
                    FirePropertyChangedEvent("Pools");
                    FirePropertyChangedEvent("PoolTabTitle");
                }
                SelectedPool = null;
                SelectedTVM = null;
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

        private async System.Threading.Tasks.Task DownloadFileAsync(string file, string localDownloadTargetPath = null, bool isTvmFile = true)
        {
            string fileName = null;
            try
            {
                bool? result;
                
                if (string.IsNullOrEmpty(localDownloadTargetPath))
                {
                    // Configure save file dialog box
                    Microsoft.Win32.SaveFileDialog saveFileDlg = new Microsoft.Win32.SaveFileDialog();
                    saveFileDlg.FileName = Path.GetFileName(file);     // Default file name
                    saveFileDlg.DefaultExt = ".txt"; // Default file extension.
                    saveFileDlg.Filter = "Text documents (.txt)|*.txt"; // Filter files by extension

                    // Show save file dialog box
                    result = saveFileDlg.ShowDialog();
                    if (result == true)
                    {
                        fileName = saveFileDlg.FileName;
                    }
                }
                else
                {
                    fileName = Path.Combine(localDownloadTargetPath, Path.GetFileName(file));
                    result = true;
                }

                if (result == true)
                {
                    // Save document
                    using (FileStream destStream = new FileStream(fileName, FileMode.Create))
                    {
                        if (isTvmFile)
                        {
                            await this.SelectedTVM.DownloadFileAsync(file, destStream);
                        }
                        else
                        {
                            await this.SelectedWorkItem.SelectedJob.SelectedTask.GetTaskFileAsync(file, destStream);
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

        private async System.Threading.Tasks.Task DownloadRDPFileAsync(TvmModel tvm, string localDownloadTargetPath = null)
        {
            string fileName = null;
            bool? result;
            if (string.IsNullOrEmpty(localDownloadTargetPath))
            {
                // Configure save file dialog box
                Microsoft.Win32.SaveFileDialog saveFileDlg = new Microsoft.Win32.SaveFileDialog();
                saveFileDlg.FileName = tvm.Name; // Default file name
                saveFileDlg.DefaultExt = ".rdp"; // Default file extension.
                saveFileDlg.Filter = "RDP File (.rdp)|*.rdp"; // Filter files by extension

                // Show save file dialog box
                result = saveFileDlg.ShowDialog();
                if (result == true)
                {
                    fileName = saveFileDlg.FileName;
                }
            }
            else
            {
                fileName = Path.Combine(localDownloadTargetPath, Path.GetFileName(tvm.Name) + ".rdp");
                result = true;
            }

            if (result == true)
            {
                // Save document
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    await tvm.DownloadRDPAsync(memoryStream);

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

        #endregion
    }
}
