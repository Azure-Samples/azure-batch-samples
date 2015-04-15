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
using TaskEntities = Microsoft.Azure.Batch.Protocol.Entities;
using System.Data;

namespace Microsoft.Azure.BatchExplorer
{
    /// <summary>
    /// Interaction logic for CreateTaskP1.xaml
    /// </summary>
    public partial class CreateTaskP1 : Page
    {
        private TaskEntities.Task task;
        private DataTable dataTableWallClock;
        private DataTable dataTableRetention;
        private BatchTreeViewItem Sender;
        private FilesEnv page2;

        public CreateTaskP1(BatchTreeViewItem sender)
        {
            Sender = sender;
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

        private void btnNext_Click(object sender, System.EventArgs e)
        {
            if (String.IsNullOrEmpty(txtName.Text))
            {
                MessageBox.Show("Please specify a name for the Task");
                return;
            }

            if (String.IsNullOrEmpty(txtCmd.Text))
            {
                MessageBox.Show("Please specify a commandline for the Task");
                return;
            }

            try
            {
                task = new TaskEntities.Task(txtName.Text, txtCmd.Text);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(
                    "{0} Caught\nTask Name is a required field. Please enter a valid name", ex));
            }

            if (!txtAffinityInfo.Text.Equals(""))
            {
                task.AffinityInfo = new AffinityInfo();

                try
                {
                    task.AffinityInfo.AffinityId = txtAffinityInfo.Text;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Create Task: Affinity Info:\n{0}", ex));
                }
            }
            else
            {
                task.AffinityInfo = null;
            }

            task.TaskConstraints = new TaskConstraints();

            try
            {
                if (!txtMaxTaskRetryCount.Text.Equals(""))
                {
                    task.TaskConstraints.MaxTaskRetryCount = int.Parse(txtMaxTaskRetryCount.Text);
                }
                else
                {
                    task.TaskConstraints.MaxTaskRetryCount = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Create Task: Max Retry Count error:\n{0}", ex));
            }

            try
            {
                if (Time.CanParse(dataTableWallClock))
                {
                    TimeSpan span = Time.ParseTimeSpan(dataTableWallClock);
                    task.TaskConstraints.MaxWallClockTime = span;
                }
                else
                {
                    task.TaskConstraints.MaxWallClockTime = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Create Task: Max Wall Clock Time error:\n{0}", ex));
            }

            try
            {
                if (Time.CanParse(dataTableWallClock))
                {
                    TimeSpan span = Time.ParseTimeSpan(dataTableRetention);
                    task.TaskConstraints.RetentionTime = span;
                }
                else
                {
                    task.TaskConstraints.RetentionTime = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Create Task: Retention Time error:\n{0}", ex));
            }

            page2 = new FilesEnv(Sender, task, txtName.Text);
            NavigationService.Navigate(page2);
        }
    }
}
