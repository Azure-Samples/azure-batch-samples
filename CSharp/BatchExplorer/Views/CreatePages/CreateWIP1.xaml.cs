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
using System.Data;

namespace Microsoft.Azure.BatchExplorer
{
    /// <summary>
    /// Interaction logic for CreateTaskP1.xaml
    /// </summary>
    public partial class CreateWIP1 : Page
    {
        private WorkItem workitem;
        private DataTable dataTableInterval;
        private DataTable dataTableUntil;
        private DataTable dataTableAfter;
        private DataTable dataTableWindow;
        private BatchTreeViewItem Sender;

        public CreateWIP1(BatchTreeViewItem sender)
        {
            Sender = sender;
            // WorkItem happens after you click

            InitializeComponent();

            // Do Not Run Until Time
            Time timeUntilTime = new Time(false);
            dataTableUntil = timeUntilTime.TimeDataTable;
            timeUntil.ItemsSource = dataTableUntil.AsDataView();

            // Do Not Run After Time
            Time timeAfterTime = new Time(false); // heh
            dataTableAfter = timeAfterTime.TimeDataTable;
            timeAfter.ItemsSource = dataTableAfter.AsDataView();

            // Recurrence Interval
            Time recurrenceIntervalTime = new Time(true);
            dataTableInterval = recurrenceIntervalTime.TimeDataTable;
            dataGridInterval.ItemsSource = dataTableInterval.AsDataView();

            // Start Window
            Time startWindowTime = new Time(true);
            dataTableWindow = startWindowTime.TimeDataTable;
            dataGridWindow.ItemsSource = dataTableWindow.AsDataView();

            Loaded += OnLoaded;

            MainWindow.Resize(MainGrid);
        }

        private void OnLoaded(object s, System.EventArgs e)
        {
            //MainWindow.SetHeightWidth(this, MainGrid);
            this.WindowHeight = MainGrid.ActualHeight;
            this.WindowWidth = MainGrid.ActualWidth;
        }

        private void chkSched_Click(object sender, RoutedEventArgs e)
        {
            if (chkSched.IsChecked == true)
            {
                dpAfter.IsEnabled = true;
                dpUntil.IsEnabled = true;
                timeAfter.IsEnabled = true;
                timeUntil.IsEnabled = true;
                dataGridInterval.IsEnabled = true;
                dataGridWindow.IsEnabled = true;
            }
            else
            {
                dpAfter.IsEnabled = false;
                dpUntil.IsEnabled = false;
                timeAfter.IsEnabled = false;
                timeUntil.IsEnabled = false;
                dataGridInterval.IsEnabled = false;
                dataGridWindow.IsEnabled = false;
            }
        }

        private void btnNext_Click(object sender, System.EventArgs e)
        {
            if (String.IsNullOrEmpty(txtName.Text))
            {
                MessageBox.Show("Please specify a name for the Workitem");
                return;
            }

            try
            {
                workitem = new WorkItem(txtName.Text, new JobExecutionEnvironment() /*temporary*/);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Create WorkItem Page 1:\n{0}", ex));
            }

            if (chkSched.IsChecked == true)
            {
                workitem.Schedule = new WorkItemSchedule();
                try
                {
                    DateTime? doNotRunUntil = dpUntil.SelectedDate;
                    if (doNotRunUntil != null && Time.CanParse(dataTableUntil))
                    {
                        TimeSpan span = Time.ParseDateTime(dataTableUntil);
                        doNotRunUntil = doNotRunUntil.Value.Add(span);
                    }
                    workitem.Schedule.DoNotRunUntil = doNotRunUntil;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("{0} Caught", ex));
                }

                try
                {
                    DateTime? doNotRunAfter = dpAfter.SelectedDate;
                    if (doNotRunAfter != null && Time.CanParse(dataTableAfter))
                    {
                        doNotRunAfter = doNotRunAfter.Value.Add(Time.ParseDateTime(dataTableAfter));
                    }
                    workitem.Schedule.DoNotRunAfter = doNotRunAfter;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("{0} Caught", ex));
                }

                try
                {
                    if (Time.CanParse(dataTableInterval))
                    {
                        TimeSpan span = Time.ParseTimeSpan(dataTableInterval);
                        workitem.Schedule.RecurrenceInterval = span;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("{0} Caught", ex));
                }

                try
                {
                    if (Time.CanParse(dataTableWindow))
                    {
                        TimeSpan span = Time.ParseTimeSpan(dataTableWindow);
                        workitem.Schedule.StartWindow = span;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("{0} Caught", ex));
                }
            }

            CreateWIP2 page2 = new CreateWIP2(Sender, workitem);
            NavigationService.Navigate(page2);
        }
    }
}
