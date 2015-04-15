using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.Models;
using Microsoft.Azure.BatchExplorer.ViewModels;
using System.Windows;
using Xceed.Wpf.DataGrid;

namespace Microsoft.Azure.BatchExplorer.Views
{
    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public partial class MainView : Window
    {
        private MainViewModel viewModel;
        
        private AboutWindow aboutWindow;
        private GenericEmptyWindow genericEmptyWindow;
        private GenericEmptyWindow genericPopupWindow;
        private GenericEmptyWindow heatmapWindow;

        private GridLength asyncOperationTabHeight;
        private const int InitialWindowWidth = 800;
        private const int InitialWindowHeight = 800;

        public MainView()
        {
            this.viewModel = new MainViewModel();
            this.DataContext = this.viewModel;
            InitializeComponent();

            //Set the default grid height
            asyncOperationTabHeight = new GridLength(1, GridUnitType.Star);

            //Add listener for the generic dialog message
            Messenger.Default.Register<GenericDialogMessage>(this, (m) =>
                {
                    Messenger.Default.Register<CloseGenericMessage>(this, 
                        (o) =>
                        {
                            this.genericPopupWindow.Close();
                            Messenger.Default.Unregister<CloseGenericMessage>(this);
                        });

                    //Need to use dispatcher.Invoke because this message may be called via a non UI thread, and those threads
                    //Cannot create controls/windows.  To work around this, creation of the window is run in the dispacter of the main view (this)
                    this.Dispatcher.Invoke(() =>
                                               {
                                                   this.genericPopupWindow = new GenericEmptyWindow();

                                                   this.genericPopupWindow.Height = InitialWindowHeight;
                                                   this.genericPopupWindow.Width = InitialWindowWidth;
                                                   this.genericPopupWindow.Title = "Message";
                                                   this.genericPopupWindow.Owner = this;
                                                   this.genericPopupWindow.Content = new GenericMessageControl(new GenericMessageViewModel(m.MessageString));
                                                   this.genericPopupWindow.ResizeMode = System.Windows.ResizeMode.CanResizeWithGrip;
                                                   this.IsEnabled = false;
                                                   this.genericPopupWindow.ShowDialog();
                                                   this.IsEnabled = true;
                                                   this.genericPopupWindow = null;
                                               });
                });
            //Add listener for the message to show the delete confirm dialog box
            Messenger.Default.Register<ShowDeleteWarningMessage>(this, (m) =>
                {
                    var result =
                        MessageBox.Show(
                            "Are you sure you want to delete this account?",
                            "Confirm Delete",
                            MessageBoxButton.YesNo);

                    if (result == MessageBoxResult.Yes)
                    {
                        Messenger.Default.Send(new ConfirmAccountDeleteMessage());
                    }
                    else
                    {
                        Messenger.Default.Send(new CloseGenericPopup());
                    }
                });

            //Add a listener to trigger the showing of the account dialog box in edit mode
            Messenger.Default.Register<EditAccountMessage>(this,
                (m) =>
                {
                    //Make sure we close the popup afterward
                    Messenger.Default.Register<CloseGenericPopup>(this,
                        (o) =>
                        {
                            this.genericEmptyWindow.Close();
                            Messenger.Default.Unregister<CloseGenericPopup>(this);
                        });

                    this.genericEmptyWindow = new GenericEmptyWindow();
                    this.genericEmptyWindow.Title = "Edit Account";
                    this.genericEmptyWindow.Content = new AccountManagementControl(m.AccountDialogViewModel);
                    this.genericEmptyWindow.ResizeMode = ResizeMode.NoResize;
                    this.genericEmptyWindow.Owner = this;
                    this.genericEmptyWindow.SizeToContent = SizeToContent.WidthAndHeight;
                    this.IsEnabled = false;
                    this.genericEmptyWindow.ShowDialog();
                    this.IsEnabled = true;
                    this.genericEmptyWindow = null;
                });

            //Add a listener to trigger the showing of the account dialog box in add mode
            Messenger.Default.Register<AddAccountMessage>(this,
                (m) =>
                {
                    //Make sure we close the popup afterward
                    Messenger.Default.Register<CloseGenericPopup>(this,
                        (o) =>
                        {
                            this.genericEmptyWindow.Close();
                            Messenger.Default.Unregister<CloseGenericPopup>(this);
                        });


                    this.genericEmptyWindow = new GenericEmptyWindow();
                    this.genericEmptyWindow.Title = "Add Account";
                    this.genericEmptyWindow.Content = new AccountManagementControl(m.AccountDialogViewModel);
                    this.genericEmptyWindow.ResizeMode = ResizeMode.NoResize;
                    this.genericEmptyWindow.Owner = this;
                    this.genericEmptyWindow.SizeToContent = SizeToContent.WidthAndHeight;
                    this.IsEnabled = false;
                    this.genericEmptyWindow.ShowDialog();
                    this.IsEnabled = true;
                    this.genericEmptyWindow = null;
                });

            //Add a listener to trigger the showing of a dialog box with multiple buttons
            Messenger.Default.Register<LaunchMultibuttonDialogMessage>(this, (message) =>
                {
                    var result = MessageBox.Show(message.DialogMessage, message.Caption, message.MessageBoxButton,
                                                 message.MessageBoxImage);
                    Messenger.Default.Send<MultibuttonDialogReturnMessage>(new MultibuttonDialogReturnMessage(result));
                });

            Messenger.Default.Register<RebootTvmMessage>(this, (message) =>
                {
                    MessageBoxResult result = MessageBox.Show("Are you sure you want to reboot this TVM?", "TVM Reboot", MessageBoxButton.YesNo);
                    TvmRebootConfimation confimation = TvmRebootConfimation.Cancelled;

                    if (result == MessageBoxResult.Yes)
                    {
                        confimation = TvmRebootConfimation.Confirmed;
                    }

                    Messenger.Default.Send<RebootTvmConfirmationMessage>(new RebootTvmConfirmationMessage(confimation));
                });

            Messenger.Default.Register<ReimageTvmMessage>(this, (message) =>
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to reimage this TVM?", "TVM Reimage", MessageBoxButton.YesNo);
                TvmReimageConfimation confimation = TvmReimageConfimation.Cancelled;

                if (result == MessageBoxResult.Yes)
                {
                    confimation = TvmReimageConfimation.Confirmed;
                }

                Messenger.Default.Send<ReimageTvmConfirmationMessage>(new ReimageTvmConfirmationMessage(confimation));
            });

            Messenger.Default.Register<ShowAboutWindow>(this, (message) =>
                {
                    this.aboutWindow = new AboutWindow();
                    this.aboutWindow.Owner = this;
                    this.IsEnabled = false;
                    this.aboutWindow.ShowDialog();
                    this.IsEnabled = true;
                    this.aboutWindow = null;
                });

            Messenger.Default.Register<ShowCreatePoolWindow>(this, (message) =>
            {
                this.genericEmptyWindow = new GenericEmptyWindow();
                this.genericEmptyWindow.Title = "Create Pool";
                this.genericEmptyWindow.Content = new CreateControls.CreatePoolControl(new CreatePoolViewModel(MainViewModel.dataProvider));
                this.genericEmptyWindow.Owner = this;
                this.genericEmptyWindow.SizeToContent = System.Windows.SizeToContent.WidthAndHeight;
                this.IsEnabled = false;
                this.genericEmptyWindow.ShowDialog();
                this.IsEnabled = true;
                this.genericEmptyWindow = null;
            });

            Messenger.Default.Register<ShowAsyncOperationDetailWindow>(this,
                (m) =>
                {
                    this.genericEmptyWindow = new GenericEmptyWindow();
                    this.genericEmptyWindow.Title = "Operation Details";
                    this.genericEmptyWindow.Content = new AsyncOperationDetailsControl(new AsyncOperationDetailsViewModel(m.AsyncOperation));
                    this.genericEmptyWindow.Owner = this;
                    this.genericEmptyWindow.Width = InitialWindowWidth;
                    this.genericEmptyWindow.Height = InitialWindowHeight;
                    this.IsEnabled = false;
                    this.genericEmptyWindow.ShowDialog();
                    this.IsEnabled = true;
                    this.genericEmptyWindow = null;
                });

            Messenger.Default.Register<ShowOptionsDialogMessage>(this,
                (m) =>
                {
                    //Make sure we close the popup afterward
                    Messenger.Default.Register<CloseGenericPopup>(this,
                        (o) =>
                        {
                            this.genericEmptyWindow.Close();
                            Messenger.Default.Unregister<CloseGenericPopup>(this);
                        });

                    this.genericEmptyWindow = new GenericEmptyWindow();
                    this.genericEmptyWindow.Title = "Options";
                    this.genericEmptyWindow.Content = new OptionsControl(new OptionsViewModel());
                    this.genericEmptyWindow.Owner = this;
                    this.genericEmptyWindow.SizeToContent = SizeToContent.WidthAndHeight;
                    this.IsEnabled = false;
                    this.genericEmptyWindow.ShowDialog();
                    this.IsEnabled = true;
                    this.genericEmptyWindow = null;
                });

            //Check if we need to collapse the operation history display on start
            if (!OptionsModel.Instance.DisplayOperationHistory)
            {
                //Store the current height and then hide the control and move the grid to auto
                asyncOperationTabHeight = this.MainGrid.RowDefinitions[3].Height;
                this.AsyncOperationGrid.Visibility = Visibility.Collapsed;
                this.MainGrid.RowDefinitions[3].Height = GridLength.Auto;
            }

            Messenger.Default.Register<ShowAsyncOperationTabMessage>(this,
                m =>
                {
                    if (m.Show)
                    {
                        this.AsyncOperationGrid.Visibility = Visibility.Visible;
                        this.MainGrid.RowDefinitions[3].Height = asyncOperationTabHeight;
                    }
                    else
                    {
                        //Store the current height and then hide the control and move the grid to auto
                        asyncOperationTabHeight = this.MainGrid.RowDefinitions[3].Height;
                        this.AsyncOperationGrid.Visibility = Visibility.Collapsed;
                        this.MainGrid.RowDefinitions[3].Height = GridLength.Auto;
                    }
                });

            Messenger.Default.Register<ShowCreateWorkItemWindow>(this, (message) =>
            {
                this.genericEmptyWindow = new GenericEmptyWindow();
                this.genericEmptyWindow.Title = "Create Work Item";
                this.genericEmptyWindow.Content = new CreateControls.CreateWorkItemControl(new CreateWorkItemViewModel(MainViewModel.dataProvider));
                this.genericEmptyWindow.Owner = this;
                this.genericEmptyWindow.SizeToContent = System.Windows.SizeToContent.Height;
                this.IsEnabled = false;
                this.genericEmptyWindow.ShowDialog();
                this.IsEnabled = true;
                this.genericEmptyWindow = null;
            });

            Messenger.Default.Register<ShowAddTaskWindow>(this, (message) =>
            {
                this.genericEmptyWindow = new GenericEmptyWindow();
                this.genericEmptyWindow.Title = "Add Task";
                this.genericEmptyWindow.Content = new CreateControls.AddTaskControl(new AddTaskViewModel(MainViewModel.dataProvider, message.WorkItemName, message.JobName));
                this.genericEmptyWindow.Owner = this;
                this.genericEmptyWindow.SizeToContent = System.Windows.SizeToContent.Height;
                this.IsEnabled = false;
                this.genericEmptyWindow.ShowDialog();
                this.IsEnabled = true;
                this.genericEmptyWindow = null;
            });

            Messenger.Default.Register<ShowCreateVMUserWindow>(this, (message) =>
            {
                //Make sure we close the popup afterward
                Messenger.Default.Register<CloseGenericPopup>(this,
                    (o) =>
                    {
                        this.genericEmptyWindow.Close();
                        Messenger.Default.Unregister<CloseGenericPopup>(this);
                    });

                this.genericEmptyWindow = new GenericEmptyWindow();
                this.genericEmptyWindow.Title = "Create VM User";
                this.genericEmptyWindow.Content = new CreateControls.CreateVMUserControl(new CreateVMUserViewModel(MainViewModel.dataProvider, message.PoolName, message.VMName));
                this.genericEmptyWindow.Owner = this;
                this.genericEmptyWindow.SizeToContent = System.Windows.SizeToContent.Height;
                this.IsEnabled = false;
                this.genericEmptyWindow.ShowDialog();
                this.IsEnabled = true;
                this.genericEmptyWindow = null;
            });

            Messenger.Default.Register<ShowResizePoolWindow>(this, (message) =>
            {
                //Make sure we close the popup afterward
                Messenger.Default.Register<CloseGenericPopup>(this,
                    (o) =>
                    {
                        this.genericEmptyWindow.Close();
                        Messenger.Default.Unregister<CloseGenericPopup>(this);
                    });

                this.genericEmptyWindow = new GenericEmptyWindow();
                this.genericEmptyWindow.Title = "Resize Pool";
                this.genericEmptyWindow.Content = new CreateControls.ResizePoolControl(new ResizePoolViewModel(MainViewModel.dataProvider, message.PoolName, message.CurrentDedicated));
                this.genericEmptyWindow.Owner = this;
                this.genericEmptyWindow.SizeToContent = System.Windows.SizeToContent.WidthAndHeight;
                this.IsEnabled = false;
                this.genericEmptyWindow.ShowDialog();
                this.IsEnabled = true;
                this.genericEmptyWindow = null;
            });

            Messenger.Default.Register<ShowHeatMapMessage>(this, (message) =>
            {
                //Need to use dispatcher.Invoke because this message may be called via a non UI thread, and those threads
                //Cannot create controls/windows.  To work around this, creation of the window is run in the dispacter of the main view (this)
                this.Dispatcher.Invoke(() =>
                    {
                        //Close the existing window if there is one
                        if (this.heatmapWindow != null)
                        {
                            this.heatmapWindow.Close();
                        }

                        HeatMapModel model = new HeatMapModel(message.Pool);
                        HeatMapControl control = new HeatMapControl(new HeatMapViewModel(model));
                                               
                        this.heatmapWindow = new GenericEmptyWindow();
                        this.heatmapWindow.Title = "Heat map"; //TODO: All these strings should be defined in a constant class somewhere
                        this.heatmapWindow.SizeToContent = SizeToContent.WidthAndHeight;
                        this.heatmapWindow.Content = control;
                        this.heatmapWindow.Closed += (sender, args) => control.Cancel();
                        this.heatmapWindow.Show();
                    });
            });
        }

        private void Selector_OnSelectionChanged(object sender, DataGridSelectionChangedEventArgs e)
        {
            /*
             * HACK - Get Selected Job Item Updated
             * For some reason, the binding of the SelectedItem property to SelectedJob doesn't work.
             * Work around this for now by setting the property manually
             *
             * REMARKS - Using the 0th index of the collection works in this case because we have
             * set the data grid to use Single Selection Mode
             */

            if (this.viewModel.SelectedWorkItem != null)
            {
                if (e.SelectionInfos.Count > 0 && e.SelectionInfos[0].AddedItems.Count > 0)
                {
                    this.viewModel.SelectedWorkItem.SelectedJob = e.SelectionInfos[0].AddedItems[0] as JobModel;
                }
            }
        }
    }
}
