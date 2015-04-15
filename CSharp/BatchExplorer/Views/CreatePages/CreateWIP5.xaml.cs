using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.Azure.BatchExplorer.Helpers;
using Media = System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.Batch.Protocol.Entities;

namespace Microsoft.Azure.BatchExplorer
{
    /// <summary>
    /// Interaction logic for CreateWIP5.xaml
    /// </summary>
    public partial class CreateWIP5 : Page
    {
        private WorkItem workitem { get; set; }
        private BatchTreeViewItem Sender;

        public CreateWIP5(BatchTreeViewItem sender, WorkItem workitem)
        {
            this.workitem = workitem;
            this.Sender = sender;

            InitializeComponent();

            Loaded += OnLoaded;

            MainWindow.Resize(MainGrid);
        }

        private void OnLoaded(object s, System.EventArgs e)
        {
            //MainWindow.SetHeightWidth(this, MainGrid);
            this.WindowHeight = MainGrid.ActualHeight;
            this.WindowWidth = MainGrid.ActualWidth;
        }

        private void btnButton_Click(object sender, System.EventArgs e)
        {
            if (rbtnPool.IsChecked == true && String.IsNullOrEmpty(txtPool.Text))
            {
                MessageBox.Show("Please specify a pool name");
                return;
            }            

            workitem.JobExecutionEnvironment = new JobExecutionEnvironment();
            if (rbtnPool.IsChecked == true)
            {
                try
                {
                    workitem.JobExecutionEnvironment.PoolName = txtPool.Text;
                    (Sender as BatchTreeViewItem).AddWorkItem(workitem);
                    (this.Parent as NavigationWindow).Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Utils.ExtractExceptionMessage(ex));
                }
            }
            else
            {                                
                workitem.JobExecutionEnvironment.AutoPoolSpecification = new AutoPoolSpecification();
                
                workitem.JobExecutionEnvironment.AutoPoolSpecification.PoolLifeTimeOption =
                    (rbtnWorkItem.IsChecked == true) ? PoolLifeTimeOption.WorkItem :
                    (rbtnJob.IsChecked == true) ? PoolLifeTimeOption.Job :
                    PoolLifeTimeOption.Invalid;

                workitem.JobExecutionEnvironment.AutoPoolSpecification.KeepAlive =
                    (rbtnKeepAliveT.IsChecked == true) ? true : false;

                workitem.JobExecutionEnvironment.AutoPoolSpecification.AutoPoolNamePrefix
                    = txtNamePrefix.Text;

                CreatePoolP1 p6 = new CreatePoolP1(this.Sender, workitem);
                NavigationService.Navigate(p6);
            }
        }

        private void rbtnPool_Click(object sender, RoutedEventArgs e)
        {
            // And the rest
            txtNamePrefix.IsEnabled = false;
            rbtnJob.IsEnabled = false;
            rbtnWorkItem.IsEnabled = false;
            lblAlive.IsEnabled = false;
            lblLifeTime.IsEnabled = false;
            rbtnKeepAliveF.IsEnabled = false;
            rbtnKeepAliveT.IsEnabled = false;

            btnDone.Content = "Done";
        }

        private void rbtnAuto_Click(object sender, RoutedEventArgs e)
        {
            // And the rest
            txtNamePrefix.IsEnabled = true;
            rbtnJob.IsEnabled = true;
            rbtnWorkItem.IsEnabled = true;
            lblAlive.IsEnabled = true;
            lblLifeTime.IsEnabled = true;
            rbtnKeepAliveF.IsEnabled = true;
            rbtnKeepAliveT.IsEnabled = true;

            btnDone.Content = "Continue";
        }
    }
}
