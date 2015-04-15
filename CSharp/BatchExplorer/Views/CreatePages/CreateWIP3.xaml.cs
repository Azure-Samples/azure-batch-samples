using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Data;
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
    /// <summary>
    /// Interaction logic for CreatePoolP2.xaml
    /// </summary>
    public partial class CreateWIP3 : Page
    {
        private WorkItem workitem;
        private BatchTreeViewItem Sender;
        private DataTable dataTableWallClock;
        private DataTable dataTableRetention;

        public CreateWIP3(BatchTreeViewItem sender, WorkItem parent)
        {
            this.workitem = parent;
            this.Sender = sender;
            InitializeComponent();

            // Wall Clock Time
            Time wallClockTime = new Time(true);
            dataTableWallClock = wallClockTime.TimeDataTable;
            dataGridWallClock.ItemsSource = dataTableWallClock.AsDataView();

            // Retention Time
            Time retentionTime = new Time(true);
            dataTableRetention = retentionTime.TimeDataTable;
            dataGridRetention.ItemsSource = dataTableRetention.AsDataView();

            Loaded += OnLoaded;

            MainWindow.Resize(MainGrid);
        }

        private void OnLoaded(object s, System.EventArgs e)
        {
            //MainWindow.SetHeightWidth(this, MainGrid);
            this.WindowHeight = MainGrid.ActualHeight;
            this.WindowWidth = MainGrid.ActualWidth;
        }

        private void btnNext_Click(object s, System.EventArgs e)
        {
            if (String.IsNullOrEmpty(txtName.Text))
            {
                MessageBox.Show("Please specify a name for the JobManager");
                return;
            }

            if (String.IsNullOrEmpty(txtCmd.Text))
            {
                MessageBox.Show("Please specify a command line for the JobManager");
                return;
            }

            workitem.JobSpecification.JobManager = new JobManager();
            try
            {
                workitem.JobSpecification.JobManager.Name = txtName.Text;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format("{0}", ex));
            }

            try
            {
                workitem.JobSpecification.JobManager.CommandLine = txtCmd.Text;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format("{0}", ex));
            }

            workitem.JobSpecification.JobManager.TaskConstraints = new TaskConstraints();

            try
            {
                if (!String.IsNullOrEmpty(txtMaxTaskRetryCount.Text))
                {
                    workitem.JobSpecification.JobManager.TaskConstraints.MaxTaskRetryCount
                        = int.Parse(txtMaxTaskRetryCount.Text);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("{0}", ex));
            }

            try
            {
                if (Time.CanParse(dataTableWallClock))
                {
                    TimeSpan span = Time.ParseTimeSpan(dataTableWallClock);
                    workitem.JobSpecification.JobManager.TaskConstraints.MaxWallClockTime = span;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("{0}", ex));
            }

            try
            {
                if (Time.CanParse(dataTableRetention))
                {
                    TimeSpan span = Time.ParseTimeSpan(dataTableRetention);
                    workitem.JobSpecification.JobManager.TaskConstraints.RetentionTime = span;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("{0}", ex));
            }

            try
            {
                workitem.JobSpecification.JobManager.KillJobOnCompletion = 
                    (rbtnKillT.IsChecked == true) ? true :
                    (rbtnKillF.IsChecked == true) ? false : false;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format("{0}", ex));
            }

            try
            {
                FilesEnv p4 = new FilesEnv(this.Sender, workitem, "1");
                NavigationService.Navigate(p4);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("{0}", ex));
            }
        }
    }
}
