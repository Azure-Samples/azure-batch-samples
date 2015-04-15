using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using Media = System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Azure.Batch.Protocol.Entities;

namespace Microsoft.Azure.BatchExplorer
{
    public partial class CreatePoolP2 : Page
    {
        private Pool pool;
        private WorkItem workitem;
        private StartTask starttask;
        private BatchTreeViewItem Sender;

        public CreatePoolP2(BatchTreeViewItem sender, object obj)
        {
            pool = null;
            workitem = null;
            starttask = null;

            if (obj is Pool)
            {
                this.pool = obj as Pool;
            }
            else if (obj is WorkItem)
            {
                this.workitem = obj as WorkItem;
            }
            else if (obj == null)
            {
                // We are going to do some weird things and return a starttask
                this.starttask = new StartTask();
            }
            else
            {
                System.Windows.MessageBox.Show("CreatePoolP2.xaml.cs: Improper object passed in");
            }

            this.Sender = sender;
            InitializeComponent();

            Loaded += OnLoaded;

            MainWindow.Resize(MainGrid);
        }

        private void OnLoaded(object s, System.EventArgs e)
        {
            MainWindow.SetHeightWidth(this, MainGrid);
            this.WindowHeight = MainGrid.ActualHeight;
            this.WindowWidth = MainGrid.ActualWidth;
        }

        private void btnNext_Click(object s, System.EventArgs e)
        {
            if (String.IsNullOrEmpty(txtCmd.Text))
            {
                MessageBox.Show("Please specify a commandline for the StartTask");
                return;
            }

            if (pool != null)
            {
                pool.StartTask = new StartTask();
            }
            else if (workitem != null)
            {
                workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool.StartTask =
                    new StartTask();
            }
            else if (starttask != null)
            {
                // we already declare it above
            }

            try
            {
                if (pool != null)
                {
                    pool.StartTask.CommandLine = txtCmd.Text;
                }
                else if (workitem != null)
                {
                    workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool.
                        StartTask.CommandLine = txtCmd.Text;
                }
                else if (starttask != null)
                {
                    starttask.CommandLine = txtCmd.Text;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(
                    "{0} Caught\nCommand Line is a required field. Please enter a valid name", ex));
            }

            try
            {
                if (pool != null)
                {
                    pool.StartTask.MaxTaskRetryCount = int.Parse(txtMaxTaskRetryCount.Text);
                }
                else if (workitem != null)
                {
                    workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool.
                    StartTask.MaxTaskRetryCount = int.Parse(txtMaxTaskRetryCount.Text);
                }
                else if (starttask != null)
                {
                    starttask.MaxTaskRetryCount = int.Parse(txtMaxTaskRetryCount.Text);
                }
            }
            catch (Exception) {            }

            try
            {
                if (pool != null)
                {
                    pool.StartTask.WaitForSuccess = (rbtnWaitT.IsChecked == true) ? true : false;
                }
                else if (workitem != null)
                {
                    workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool.StartTask.WaitForSuccess = (rbtnWaitT.IsChecked == true) ? true : false;
                }
                else if (starttask != null)
                {
                    starttask.WaitForSuccess = (rbtnWaitT.IsChecked == true) ? true : false;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(
                    "{0} Caught\nThis is not a proper Wait argument.", ex));
            }

            if (pool != null)
            {
                FilesEnv p3 = new FilesEnv(this.Sender, pool, "Client SDK has problemsss...");
                NavigationService.Navigate(p3);
            }
            else if (workitem != null)
            {
                FilesEnv p8 = new FilesEnv(this.Sender, workitem, "2");
                NavigationService.Navigate(p8);
            }
            else if (starttask != null)
            {
                FilesEnv p2 = new FilesEnv(this.Sender, starttask, "Client SDK has problemsss...");
                NavigationService.Navigate(p2);
            }
        }
    }
}
