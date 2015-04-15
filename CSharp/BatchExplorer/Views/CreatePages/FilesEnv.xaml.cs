using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
using System.Data;
using Microsoft.Azure.Batch.Protocol.Entities;
using TaskEntities = Microsoft.Azure.Batch.Protocol.Entities;
using Microsoft.WindowsAzure.TaskClient.Cache;
using System.Configuration;
using Microsoft.WindowsAzure.StorageClient;

namespace Microsoft.Azure.BatchExplorer
{
    /// <summary>
    /// Interaction logic for CreateTaskP2.xaml
    /// </summary>
    public partial class FilesEnv : Page
    {
        private TaskEntities.Task task { get; set; }
        private Pool pool { get; set; }
        private WorkItem workitem { get; set; }
        private StartTask starttask;
        private string code { get; set; }
        private DataView dataView1 { get; set; }
        private DataView dataView2 { get; set; }
        private DataTable dataTable1 { get; set; }
        private string TaskName;
        private BatchTreeViewItem Sender;
        private List<ResourceFile> resourceFiles;

        public FilesEnv(BatchTreeViewItem sender, object obj, string name)
        {
            try
            {
                Sender = sender;
                InitializeComponent();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format("{0}", ex));
            }

            task = null;
            pool = null;
            workitem = null;
            starttask = null;
            code = name;

            if (obj is TaskEntities.Task)
            {
                this.task = obj as TaskEntities.Task;
                this.TaskName = name;
            }
            else if (obj is Pool)
            {
                this.pool = obj as Pool;
            }
            else if (obj is WorkItem && code.Equals("1"))
            {
                try
                {
                    this.workitem = obj as WorkItem;
                    btnFinish.Content = "Next";
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(string.Format("{0}", ex));
                }
            }
            else if (obj is WorkItem && code.Equals("2"))
            {
                try
                {
                    this.workitem = obj as WorkItem;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(string.Format("{0}", ex));
                }
            }
            else if (obj is StartTask)
            {
                try
                {
                    btnFinish.Content = "Update";
                    this.starttask = obj as StartTask;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("FilesEnv.xaml.cs:\n{0}", ex));
                }
            }
            else
            {

                System.Windows.MessageBox.Show("FilesEnv.xaml.cs: Improper object passed in");
            }

            dataTable1 = new DataTable();
            dataView1 = dataTable1.AsDataView();
            filesGrid.ItemsSource = dataView1;
            dataTable1.Columns.Add("File Name", typeof(string));
            dataTable1.Columns.Add("Blob Path", typeof(string));
            DataRow row1 = dataTable1.NewRow();
            dataTable1.Rows.Add(row1);

            DataTable dataTable2 = new DataTable();
            dataView2 = dataTable2.AsDataView();
            envGrid.ItemsSource = dataView2;
            dataTable2.Columns.Add("Variable Name", typeof(string));
            dataTable2.Columns.Add("Value", typeof(string));
            DataRow row2 = dataTable2.NewRow();
            dataTable2.Rows.Add(row2);

            Loaded += OnLoaded;

            MainWindow.Resize(MainGrid);
        }

        private void OnLoaded(object s, System.EventArgs e)
        {
            //MainWindow.SetHeightWidth(this, MainGrid);
            this.WindowHeight = MainGrid.ActualHeight;
            this.WindowWidth = MainGrid.ActualWidth;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            //Open the dialog box
            System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.InitialDirectory = "C:/";
            dialog.Multiselect = true;
            dialog.RestoreDirectory = true;
            System.Windows.Forms.DialogResult Result = dialog.ShowDialog();

            string[] files;
            //Load the image to the load image picture box
            if (Result == System.Windows.Forms.DialogResult.OK)
            {
                files = dialog.FileNames;

                string blobUri = SettingsValues.BLOB_URL;
                string key = SettingsValues.BLOB_KEY;
                string account = SettingsValues.BLOB_ACCOUNT;
                StorageCredentialsAccountAndKey credentials = new StorageCredentialsAccountAndKey(account, key);
                CloudBlobClient client = new CloudBlobClient(blobUri, credentials);

                string containerName = "container";
                CloudBlobContainer blobContainer = client.GetContainerReference(containerName);
                bool created = blobContainer.CreateIfNotExist();

                resourceFiles = new List<ResourceFile>();

                foreach (string strFile in files)
                {
                    string[] chunks = strFile.Split('\\');
                    string blobName = chunks[chunks.Length - 1];
                    string filePath = strFile.Substring(0, strFile.Length - blobName.Length);

                    CloudBlob blob = blobContainer.GetBlobReference(blobName);
                    //System.Windows.MessageBox.Show(string.Format("filepath: {0}", strFile));
                    blob.UploadFile(strFile);

                    SharedAccessBlobPolicy policy = new SharedAccessBlobPolicy();
                    policy.Permissions = SharedAccessBlobPermissions.Read;
                    policy.SharedAccessExpiryTime = DateTime.Now.AddHours(1);
                    
                    string token = blob.GetSharedAccessSignature(policy);
                    string resourceBlobPath = blobUri + "/" + containerName + "/" + blobName + token;

                    System.Windows.MessageBox.Show("Uploading file complete");

                    DataRow row = dataTable1.NewRow(); ;
                    row.ItemArray = new object[]{blobName, resourceBlobPath};
                    dataView1.Table.Rows.Add(row);
                }
            }
        }

        private void btnFinish_Click(object sender, System.EventArgs e)
        {
            try
            {
                foreach (DataRowView row in dataView1)
                {
                    string key = row.Row.ItemArray[0].ToString();
                    string value = row.Row.ItemArray[1].ToString();
                    if (!String.IsNullOrEmpty(key) &&
                        !String.IsNullOrEmpty(value))
                    {
                        ResourceFile resFile = new ResourceFile();
                        resFile.FilePath = key;
                        resFile.BlobSource = value;                        

                        resourceFiles.Add(resFile);
                    }
                }

                if (resourceFiles != null &&
                    resourceFiles.Count > 0)
                {
                    if (task != null)
                    {
                        task.ResourceFiles = resourceFiles.ToArray();
                    }
                    else if (pool != null)
                    {
                        pool.StartTask.ResourceFiles = resourceFiles.ToArray();
                    }
                    else if (workitem != null && code.Equals("1"))
                    {
                        workitem.JobSpecification.JobManager.ResourceFiles =
                            resourceFiles.ToArray();
                    }
                    else if (workitem != null && code.Equals("2"))
                    {
                        workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool.
                            StartTask.ResourceFiles = resourceFiles.ToArray();
                    }
                    else if (starttask != null)
                    {
                        starttask.ResourceFiles = resourceFiles.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("FilesEnv resource files:\n{0}", ex));
            }

            try
            {
                List<EnvironmentSetting> envSettings = new List<EnvironmentSetting>();
                foreach (DataRowView row in dataView2)
                {
                    string key = row.Row.ItemArray[0].ToString();
                    string value = row.Row.ItemArray[1].ToString();
                    if (!String.IsNullOrEmpty(key) &&
                        !String.IsNullOrEmpty(value))
                    {
                        EnvironmentSetting setting = new EnvironmentSetting();
                        setting.Name = key;
                        setting.Value = value;
                        envSettings.Add(setting);
                    }
                }
                if (envSettings.Count > 0)
                {
                    if (task != null)
                    {
                        task.EnvironmentSettings = envSettings.ToArray();
                    }
                    else if (pool != null)
                    {
                        pool.StartTask.EnvironmentSettings = envSettings.ToArray();
                    }
                    else if (workitem != null && code.Equals("1"))
                    {
                        workitem.JobSpecification.JobManager.EnvironmentSettings = envSettings.ToArray();
                    }
                    else if (workitem != null && code.Equals("2"))
                    {
                        workitem.JobExecutionEnvironment.AutoPoolSpecification.Pool.
                            StartTask.EnvironmentSettings = envSettings.ToArray();
                    }
                    else if (starttask != null)
                    {
                        starttask.EnvironmentSettings = envSettings.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("FilesEnv envSettings:\n{0}", ex));
            }

            try
            {
                if (task != null)
                {
                    (Sender as BatchTreeViewItem).AddTask(task, TaskName);
                    (this.Parent as NavigationWindow).Close();
                }
                else if (pool != null)
                {
                    (Sender as BatchTreeViewItem).AddPool(pool, pool.Name);
                    (this.Parent as NavigationWindow).Close();
                }
                else if (workitem != null && code.Equals("1"))
                {
                    CreateWIP5 p5 = new CreateWIP5(this.Sender, workitem);
                    NavigationService.Navigate(p5);
                }
                else if (workitem != null && code.Equals("2"))
                {
                    (Sender as BatchTreeViewItem).AddWorkItem(workitem);
                    (this.Parent as NavigationWindow).Close();
                }
                else if (starttask != null)
                {
                    //TODO::Certificates and metadata
                    ((Sender as BatchTreeViewItem).Item as PoolViewModel).UpdatePool(starttask, null, null);
                    (this.Parent as NavigationWindow).Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Utils.ExtractExceptionMessage(ex));
            }
        }        
    }
}
