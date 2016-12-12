//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.Views
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using GalaSoft.MvvmLight.Messaging;
    using Microsoft.Azure.BatchExplorer.Messages;
    using Microsoft.Azure.BatchExplorer.Models;
    using Microsoft.Azure.BatchExplorer.ViewModels;

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
        private const int MinWindowHeight = 100;
        private const int MaxWindowHeight = 800;
        private const int MinWindowWidth = 180;
        private const int MaxWindowWidth = 800;

        public MainView()
        {
            try
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

                            this.genericPopupWindow.MinHeight = MinWindowHeight;
                            this.genericPopupWindow.MaxHeight = MaxWindowHeight;
                            this.genericPopupWindow.MinWidth = MinWindowWidth;
                            this.genericPopupWindow.MaxWidth = MaxWindowWidth;
                            this.genericPopupWindow.Title = "Message";
                            this.genericPopupWindow.Owner = this;
                            this.genericPopupWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                            this.genericPopupWindow.Content = new GenericMessageControl(new GenericMessageViewModel(m.MessageString));
                            this.genericPopupWindow.ResizeMode = System.Windows.ResizeMode.CanResizeWithGrip;
                            this.genericPopupWindow.SizeToContent = System.Windows.SizeToContent.WidthAndHeight;
                            this.genericPopupWindow.VerticalContentAlignment = System.Windows.VerticalAlignment.Top;
                            this.genericPopupWindow.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
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

                Messenger.Default.Register<RebootComputeNodeMessage>(this, (message) =>
                    {
                        MessageBoxResult result = MessageBox.Show("Are you sure you want to reboot this Compute Node?", "Compute Node Reboot", MessageBoxButton.YesNo);
                        ComputeNodeRebootConfimation confimation = ComputeNodeRebootConfimation.Cancelled;

                        if (result == MessageBoxResult.Yes)
                        {
                            confimation = ComputeNodeRebootConfimation.Confirmed;
                        }

                        Messenger.Default.Send<RebootComputeNodeConfirmationMessage>(new RebootComputeNodeConfirmationMessage(confimation));
                    });

                Messenger.Default.Register<ReimageComputeNodeMessage>(this, (message) =>
                {
                    MessageBoxResult result = MessageBox.Show("Are you sure you want to reimage this Compute Node?", "Compute Node Reimage", MessageBoxButton.YesNo);
                    ComputeNodeReimageConfimation confimation = ComputeNodeReimageConfimation.Cancelled;

                    if (result == MessageBoxResult.Yes)
                    {
                        confimation = ComputeNodeReimageConfimation.Confirmed;
                    }

                    Messenger.Default.Send<ReimageComputeNodeConfirmationMessage>(new ReimageComputeNodeConfirmationMessage(confimation));
                });

                Messenger.Default.Register<ReimageComputeNodeMessage>(this, (message) =>
                {
                    MessageBoxResult result = MessageBox.Show("Are you sure you want to reimage this Compute Node?", "Compute Node Reimage", MessageBoxButton.YesNo);
                    ComputeNodeReimageConfimation confimation = ComputeNodeReimageConfimation.Cancelled;

                    if (result == MessageBoxResult.Yes)
                    {
                        confimation = ComputeNodeReimageConfimation.Confirmed;
                    }

                    Messenger.Default.Send<ReimageComputeNodeConfirmationMessage>(new ReimageComputeNodeConfirmationMessage(confimation));
                });

                Messenger.Default.Register<DisableSchedulingComputeNodeMessage>(this, (message) =>
                {
                    MessageBoxResult result = MessageBox.Show("Are you sure you want to disable scheduling on this Compute Node?", "Compute Node Disable Scheduling", MessageBoxButton.YesNo);
                    ComputeNodeDisableSchedulingConfimation confimation = ComputeNodeDisableSchedulingConfimation.Cancelled;

                    if (result == MessageBoxResult.Yes)
                    {
                        confimation = ComputeNodeDisableSchedulingConfimation.Confirmed;
                    }

                    Messenger.Default.Send<DisableSchedulingComputeNodeConfirmationMessage>(new DisableSchedulingComputeNodeConfirmationMessage(confimation));
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
                    this.genericEmptyWindow.Content = new CreateControls.CreatePoolControl(new CreatePoolViewModel(MainViewModel.dataProvider, message.NodeAgentSkus));
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
                        this.genericEmptyWindow.MinHeight = MinWindowHeight;
                        this.genericEmptyWindow.MaxHeight = MaxWindowHeight;
                        this.genericEmptyWindow.MinWidth = MinWindowWidth;
                        this.genericEmptyWindow.MaxWidth = MaxWindowWidth;
                        this.genericEmptyWindow.Title = "Operation Details";
                        this.genericEmptyWindow.Content = new AsyncOperationDetailsControl(new AsyncOperationDetailsViewModel(m.AsyncOperation));
                        this.genericEmptyWindow.Owner = this;
                        this.genericEmptyWindow.ResizeMode = System.Windows.ResizeMode.CanResizeWithGrip;
                        this.genericEmptyWindow.SizeToContent = System.Windows.SizeToContent.WidthAndHeight;
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

                const int optionsRowLocation = 3;
                //Check if we need to collapse the operation history display on start
                if (!OptionsModel.Instance.DisplayOperationHistory)
                {
                    //Store the current height and then hide the control and move the grid to auto
                    asyncOperationTabHeight = this.MainGrid.RowDefinitions[optionsRowLocation].Height;
                    this.AsyncOperationGrid.Visibility = Visibility.Collapsed;
                    this.MainGrid.RowDefinitions[optionsRowLocation].Height = GridLength.Auto;
                }

                Messenger.Default.Register<ShowAsyncOperationTabMessage>(this,
                    m =>
                    {
                        if (m.Show)
                        {
                            this.AsyncOperationGrid.Visibility = Visibility.Visible;
                            this.MainGrid.RowDefinitions[optionsRowLocation].Height = asyncOperationTabHeight;
                        }
                        else
                        {
                            //Store the current height and then hide the control and move the grid to auto
                            asyncOperationTabHeight = this.MainGrid.RowDefinitions[optionsRowLocation].Height;
                            this.AsyncOperationGrid.Visibility = Visibility.Collapsed;
                            this.MainGrid.RowDefinitions[optionsRowLocation].Height = GridLength.Auto;
                        }
                    });

                Messenger.Default.Register<ShowCreateJobScheduleWindow>(this, (message) =>
                {
                    this.genericEmptyWindow = new GenericEmptyWindow();
                    this.genericEmptyWindow.Title = "Create Job Schedule";
                    this.genericEmptyWindow.Content = new CreateControls.CreateJobScheduleControl(new CreateJobScheduleViewModel(MainViewModel.dataProvider));
                    this.genericEmptyWindow.Owner = this;
                    this.genericEmptyWindow.SizeToContent = System.Windows.SizeToContent.Height;
                    this.IsEnabled = false;
                    this.genericEmptyWindow.ShowDialog();
                    this.IsEnabled = true;
                    this.genericEmptyWindow = null;
                });

                Messenger.Default.Register<ShowCreateJobWindow>(this, (message) =>
                {
                    this.genericEmptyWindow = new GenericEmptyWindow();
                    this.genericEmptyWindow.Title = "Create Job";
                    this.genericEmptyWindow.Content = new CreateControls.CreateJobControl(new CreateJobViewModel(MainViewModel.dataProvider));
                    this.genericEmptyWindow.Owner = this;
                    this.genericEmptyWindow.SizeToContent = System.Windows.SizeToContent.Height;
                    this.IsEnabled = false;
                    this.genericEmptyWindow.ShowDialog();
                    this.IsEnabled = true;
                    this.genericEmptyWindow = null;
                });

                Messenger.Default.Register<ShowCreateCertificateWindow>(this, message =>
                {
                    this.genericEmptyWindow = new GenericEmptyWindow();
                    this.genericEmptyWindow.Title = "Add Certificate";
                    this.genericEmptyWindow.Content = new CreateControls.CreateCertificateControl(new CreateCertificateViewModel(MainViewModel.dataProvider));
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
                    this.genericEmptyWindow.Content = new CreateControls.AddTaskControl(new AddTaskViewModel(MainViewModel.dataProvider, message.JobId));
                    this.genericEmptyWindow.Owner = this;
                    this.genericEmptyWindow.SizeToContent = System.Windows.SizeToContent.Height;
                    this.IsEnabled = false;
                    this.genericEmptyWindow.ShowDialog();
                    this.IsEnabled = true;
                    this.genericEmptyWindow = null;
                });

                Messenger.Default.Register<ShowCreateComputeNodeUserWindow>(this, (message) =>
                {
                    //Make sure we close the popup afterward
                    Messenger.Default.Register<CloseGenericPopup>(this,
                        (o) =>
                        {
                            this.genericEmptyWindow.Close();
                            Messenger.Default.Unregister<CloseGenericPopup>(this);
                        });

                    this.genericEmptyWindow = new GenericEmptyWindow();
                    this.genericEmptyWindow.Title = "Create Compute Node User";
                    this.genericEmptyWindow.Content = new CreateControls.CreateComputeNodeUserControl(new CreateComputeNodeUserViewModel(MainViewModel.dataProvider, message.PoolId, message.ComputeNodeId));
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
                    this.genericEmptyWindow.Content = new CreateControls.ResizePoolControl(new ResizePoolViewModel(MainViewModel.dataProvider, message.PoolId, message.CurrentDedicated, message.CurrentAutoScaleFormula));
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
            catch (Exception e)
            {
                // Record the exception before throwing
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "batchexplorerexception.txt"), e.ToString());
                throw;
            }
        }

        public void TreeViewCopyCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            TreeView treeView = (TreeView)sender;
            PropertyModel propertyModel = treeView.SelectedItem as PropertyModel;
            SimplePropertyModel simplePropertyModel = propertyModel as SimplePropertyModel;

            string text = null;

            if (simplePropertyModel != null)
            {
                //TODO: This is bad to do?
                text = string.Format("{0}: {1}", simplePropertyModel.PropertyName, simplePropertyModel.PropertyValue);
            }
            else if (propertyModel != null)
            {
                text = propertyModel.PropertyName;
            }
            else
            {
                throw new ArgumentException("sender");
            }

            Clipboard.SetText(text);
        }

        public void TreeViewCopyCommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            TreeView treeView = (TreeView)sender;
            SimplePropertyModel simplePropertyModel = treeView.SelectedItem as SimplePropertyModel;

            e.CanExecute = simplePropertyModel != null;
        }

        private void SearchJobsTxtBox_GotFocus(object sender, RoutedEventArgs e)
        {
            this.SearchJobsTxtBox.Text = String.Empty;
        }

        private void SearchJobsTxtBox_KeyUp(object sender, KeyEventArgs e)
        {
            this.viewModel.JobsSearchFilter = this.SearchJobsTxtBox.Text;
            if (e.Key == Key.Enter && this.viewModel.IsAccountConnected)
            {
                Messenger.Default.Send<RefreshMessage>(new RefreshMessage(RefreshTarget.Jobs));
            }
        }

        private void LinkedStorageHelp_RequestNavigate(object sender,
                                               System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start("https://docs.microsoft.com/en-us/azure/batch/batch-task-output");
        }
    }
}
