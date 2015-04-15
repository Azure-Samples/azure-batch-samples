using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Media = System.Windows.Media;
using Microsoft.Azure.Batch.Protocol.Entities;

namespace Microsoft.Azure.BatchExplorer
{
    /// <summary>
    /// Interaction logic for CreateWIP2.xaml
    /// </summary>
    public partial class CreateWIP2 : Page
    {
        private WorkItem workitem;
        private DataTable dataTableWallClock;
        private BatchTreeViewItem Sender;

        public CreateWIP2(BatchTreeViewItem sender, WorkItem wi)
        {
            InitializeComponent();
            this.Sender = sender;
            workitem = wi;

            // Wall Clock Time
            Time wallClockTime = new Time(true);
            dataTableWallClock = wallClockTime.TimeDataTable;
            dataGridWallClock.ItemsSource = dataTableWallClock.AsDataView();

            Loaded += OnLoaded;

            MainWindow.Resize(MainGrid);
        }

        private void OnLoaded(object s, System.EventArgs e)
        {
            //MainWindow.SetHeightWidth(this, MainGrid);
            this.WindowHeight = MainGrid.ActualHeight;
            this.WindowWidth = MainGrid.ActualWidth;
        }

        private void btnNext_Click(object sender, System.EventArgs e)
        {
            workitem.JobSpecification = new JobSpecification();

            try
            {
                if (txtRetry.Text != "")
                {
                    workitem.JobSpecification.Priority = int.Parse(txtPriority.Text);
                }
                else
                {
                    workitem.JobSpecification.Priority = 0;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(
                    "{0} Caught", ex));
            }

            workitem.JobSpecification.JobConstraints = new JobConstraints();

            try
            {
                if (txtRetry.Text != "")
                {
                    workitem.JobSpecification.JobConstraints.MaxTaskRetryCount = int.Parse(txtRetry.Text);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(
                    "Max Task Retry Count:\n{0}", ex));
            }

            try
            {
                if (Time.CanParse(dataTableWallClock))
                {
                    TimeSpan span = Time.ParseTimeSpan(dataTableWallClock);
                    workitem.JobSpecification.JobConstraints.MaxWallClockTime = span;
                }
                else
                {
                    workitem.JobSpecification.JobConstraints.MaxWallClockTime = null;
                }
            }
            catch (Exception) { }

            if (chkJobManager.IsChecked == true)
            {
                CreateWIP3 p3 = new CreateWIP3(Sender, workitem);
                NavigationService.Navigate(p3);
            }
            else
            {
                CreateWIP5 p5 = new CreateWIP5(Sender, workitem);
                NavigationService.Navigate(p5);
            }
        }
    }
}
