using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
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
using Microsoft.Azure.Batch.Protocol.Entities;

namespace Microsoft.Azure.BatchExplorer
{
    /// <summary>
    /// Interaction logic for CreatePoolP1.xaml
    /// </summary>
    public partial class CreatePoolP1 : Page
    {
        private Pool pool;
        private WorkItem workitem;
        private BatchTreeViewItem Sender;
        private DataTable dataTableResize;
        private DataView dataView { get; set; }

        public CreatePoolP1(BatchTreeViewItem sender, object obj)
        {
            this.Sender = sender;
            InitializeComponent();

            pool = null;
            workitem = null;

            DataTable dataTable2 = new DataTable();
            dataView = dataTable2.AsDataView();
            envGrid.ItemsSource = dataView;
            dataTable2.Columns.Add("Name", typeof(string));
            dataTable2.Columns.Add("Value", typeof(string));
            //envGrid.ColumnWidth = new DataGridLength(1, DataGridLengthUnitType.Star);
            DataRow row2 = dataTable2.NewRow();
            dataTable2.Rows.Add(row2);

            // Resize Timeout
            Time resizeTime = new Time(true);
            dataTableResize = resizeTime.TimeDataTable;
            datagridResize.ItemsSource = dataTableResize.AsDataView();

            if (obj is Pool)
            {
                this.pool = obj as Pool;
            }
            else if (obj is WorkItem)
            {
                lblTitle.Content = "WorkItem Autopool Specifications";
                txtName.IsEnabled = false;
                lblName.IsEnabled = false;
                lblNameStar.Width = 0;
                this.workitem = obj as WorkItem;
            }
            else
            {
                System.Windows.MessageBox.Show("CreatePoolP1.xaml.cs: Improper object passed in");
            }

            btnNext.Content = "Done";

            Loaded += OnLoaded;

            MainWindow.Resize(MainGrid);
        }

        private void OnLoaded(object s, System.EventArgs e)
        {
            MainWindow.SetHeightWidth(this, MainGrid);
            this.WindowHeight = MainGrid.ActualHeight;
            this.WindowWidth = MainGrid.ActualWidth;
        }

        private void chkStartTask_Click(object s, System.EventArgs e)
        {
            if (chkStartTask.IsChecked == true)
            {
                btnNext.Content = "Next";
            }
            else
            {
                btnNext.Content = "Done";
            }
        }

        private void btnNext_Click(object s, System.EventArgs e)
        {
            if (String.IsNullOrEmpty(txtName.Text) && workitem == null)
            {
                // don't check TVM Size because it has a default
                MessageBox.Show("Pool name cannot be empty");
                return;
            }

            try
            {                

                if (pool != null)
                {
                    pool = new Pool(txtName.Text, "small" /*temporary*/);
                }
                else if (workitem != null)
                {
                    workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool = new PoolUserSpec();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(
                    "{0} Caught\nName is a required field. Please enter a valid name", ex));
            }

            try
            {
                if (Time.CanParse(dataTableResize))
                {
                    TimeSpan span = Time.ParseTimeSpan(dataTableResize);

                    if (workitem != null)
                    {
                        workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool.
                            ResizeTimeout = span;
                    }
                    else if (pool != null)
                    {
                        pool.ResizeTimeout = span;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("{0}", ex));
            }

            try
            {
                string size = (rbtnXLarge.IsChecked == true) ? "extralarge" :
                        (rbtnLarge.IsChecked == true) ? "large" : "small";

                if (workitem != null)
                {
                    workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool.TVMSize = size;
                }
                else if (pool != null)
                {
                    pool.TVMSize = size;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(
                    "{0} Caught\nA TVM size is required", ex));
            }

            try
            {
                if (rbtnAuto.IsChecked == true)
                {
                    if (workitem != null)
                    {
                        workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool.EnableAutoScale = true;
                        workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool.AutoScaleFormula = txtAutoScale.Text;
                    }
                    else if (pool != null)
                    {
                        pool.EnableAutoScale = true;
                        pool.AutoScaleFormula = txtAutoScale.Text;
                    }
                }
                else if (rbtnTD.IsChecked == true)
                {
                    if (workitem != null)
                    {
                        workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool.EnableAutoScale = false;
                        workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool.TargetDedicated = int.Parse(txtTD.Text);
                    }
                    else if (pool != null)
                    {
                        pool.EnableAutoScale = false;
                        pool.TargetDedicated = int.Parse(txtTD.Text);
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("Something is wrong with the Autoscale/TD buttons");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(
                    "{0} Caught\nThis is not a proper Autoscale/TD argument.", ex));
            }

            try
            {
                if (workitem != null)
                {
                    workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool.Communication = (rbtnComT.IsChecked == true) ? true :
                        (rbtnComF.IsChecked == true) ? false : false;
                }
                else if (pool != null)
                {
                    pool.Communication = (rbtnComT.IsChecked == true) ? true :
                        (rbtnComF.IsChecked == true) ? false : false;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(
                    "{0} Caught\nThis is not a proper Communication argument.", ex));
            }

            try
            {
                if (workitem != null)
                {
                    workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool.StorageAccountAffinity = txtSAA.Text;
                }
                else if (pool != null)
                {
                    pool.StorageAccountAffinity = txtSAA.Text;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(
                    "{0} Caught\nThis is not a proper Storage Account Affinity", ex));
            }

            try
            {
                List<MetadataItem> metadata = new List<MetadataItem>();
                foreach (DataRowView row in dataView)
                {
                    string key = row.Row.ItemArray[0].ToString();
                    string value = row.Row.ItemArray[1].ToString();
                    if (!String.IsNullOrEmpty(key) &&
                        !String.IsNullOrEmpty(value))
                    {
                        MetadataItem item = new MetadataItem();
                        item.Name = key;
                        item.Value = value;
                        metadata.Add(item);
                    }
                }
                if (metadata.Count > 0)
                {
                    if (workitem != null)
                    {
                        workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool.Metadata = metadata.ToArray();
                    }
                    else if (pool != null)
                    {
                        pool.Metadata = metadata.ToArray();
                    }                    
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Pool Metadata:\n{0}", ex));
            }

            try
            {
                if (pool != null && chkStartTask.IsChecked == true)
                {
                    CreatePoolP2 p2 = new CreatePoolP2(this.Sender, pool);
                    NavigationService.Navigate(p2);
                }
                else if (pool != null && chkStartTask.IsChecked == false)
                {
                    (Sender as BatchTreeViewItem).AddPool(pool, pool.Name);
                    (this.Parent as NavigationWindow).Close();
                }
                else if (workitem != null && chkStartTask.IsChecked == true)
                {
                    CreatePoolP2 p7 = new CreatePoolP2(this.Sender, workitem);
                    NavigationService.Navigate(p7);
                }
                else if (workitem != null && chkStartTask.IsChecked == false)
                {
                    (Sender as BatchTreeViewItem).AddWorkItem(workitem);
                    (this.Parent as NavigationWindow).Close();
                }
                else
                {
                    MessageBox.Show("Improper pool/workitem argument passed into CreatePoolP1");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Utils.ExtractExceptionMessage(ex));
            }
        }
    }
}
