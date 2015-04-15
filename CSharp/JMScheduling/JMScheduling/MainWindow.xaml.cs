using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Protocol;

namespace Azure.Batch.SDK.Samples.JobScheduling.JMScheduling
{
    /// <summary>
    /// This sample shows how to use some of the less common aspects of job scheduling.  This includes:
    /// - Making a job which will run at a predetermined time
    /// - Making a recurring job
    /// - Ensuring a job stops so that it the next recurrence will run
    /// - Using retries on a request
    /// - Using environment variables to pass data to a task
    /// - Having a Job Manager wait for the associated tasks to exit
    /// 
    /// The UI is provided as it gives an easier way to set up the time the task will run or its recurrence interval.
    /// </summary>
    public partial class MainWindow : Window
    {
        // The following are provided for convenience - you can fill them out here, or in the interface.
        // The Pool with Idle VMs and the Storage Container must be pre-created.
        private const string PoolName = "";
        private const string BatchAccountName = "";
        private const string BatchAccountKey = "";
        private const string StorageAccountName = "";
        private const string StorageAccountKey = "";
        private const string StorageContainer = "";

        public MainWindow()
        {
            InitializeComponent();

            // Populate the UI with the strings if provided above.
            txtBAccountName.Text = BatchAccountName;
            txtBAccountKey.Text = BatchAccountKey;
            txtSAccountName.Text = StorageAccountName;
            txtSAccountKey.Text = StorageAccountKey;
            txtSContainerName.Text = StorageContainer;
            txtPoolName.Text = PoolName;

            // Fill in dropdowns with values for hour / minute.
            for (int i = 0; i < 24; i++)
            {
                cbxHourO.Items.Add(i);
                cbxHourR.Items.Add(i);
            }
            for (int i = 0; i < 60; i++)
            {
                cbxMinuteO.Items.Add(i);
                cbxMinuteR.Items.Add(i);
            }

            // set defaults - recurrence, evey hour, one time at midnight
            cbxHourR.SelectedIndex = 1;
            cbxMinuteR.SelectedIndex = 0;

            cbxHourO.SelectedIndex = 0;
            cbxMinuteO.SelectedIndex = 0;
        }

        /// <summary>
        /// This helper will upload the needed files into the storage account.
        /// </summary>
        private void UploadFiles()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=" + txtSAccountName.Text + ";AccountKey=" + txtSAccountKey.Text);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(StorageContainer);

            // upload the required files into blob storage. The postbuild step copies the files from the configuration
            // based directory to a directory that is peer with JobManager and JMScheduling
            foreach (string file in SampleConstants.JobManagerFiles)
            {
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(file);
                using (var fileStream = System.IO.File.OpenRead(@"..\..\..\FilesToUpload\" + file))
                {
                    blockBlob.UploadFromStream(fileStream);
                }
            }
        }

        /// <summary>
        /// This will take the basic data provided about the account, upload the necessary information to the account, and schedule a job.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            BatchCredentials credentials = new BatchCredentials(txtBAccountName.Text, txtBAccountKey.Text);
            IBatchClient bClient = BatchClient.Connect(SampleConstants.BatchSvcEndpoint, credentials);

            // Setting a retry policy adds robustness against an individual call timing out.  When using a policy, by default all recoverable failures are retried.
            bClient.CustomBehaviors.Add(new SetRetryPolicy(new ExponentialRetry(TimeSpan.FromSeconds(5), 5)));

            // Create a unique workitem name; don't forget to delete these when you're done
            string workItemName = SampleConstants.WorkItemNamePrefix + Guid.NewGuid().ToString();

            // Identify the pre-existing pool of VMs that will run the tasks. An Autopool specification
            // is fine but there is the delay associated with the creation of the pool along with waiting
            // for the VMs to reach Idle state before tasks are running. You can use Batch Explorer to
            // pre-create the pool and then resize it to the desired size and number of VMs.
            JobExecutionEnvironment jee = new JobExecutionEnvironment()
            {
                PoolName = PoolName
            };

            // Next, create the JobManager instance describing the environment settings and resources it
            // needs to run
            JobManager jobMgr = new JobManager()
            {
                Name = "JM1",
                CommandLine = SampleConstants.JobManager,

                // NOTE: We do not in general recommend that customers put their secrets on the command line or as environmental variables, as 
                //       these are not a secure locations.  This was done for the simplicity of the sample.
                EnvironmentSettings = new List<IEnvironmentSetting>() {
                    { new EnvironmentSetting( SampleConstants.EnvWorkItemName, workItemName ) },
                    { new EnvironmentSetting( SampleConstants.EnvBatchAccountKeyName, txtBAccountKey.Text) }
                },

                // In many cases you will want KillJobOnCompletion to be set to 'TRUE' - this allows the previous job to finish before
                // a recurrence is scheduled.  As an alternative, you can set this to 'FALSE' and use MaxWallClockTime as shown below,
                // which will instead ensure that every recurrence happens.
                KillJobOnCompletion = true
            };

            // Create a list of resource files that are needed to run JobManager.exe. A shared access signature key specifying
            // readonly access is used so the JobManager program will have access to the resource files when it is started
            // on a VM.
            var sasPrefix = Helpers.ConstructContainerSas(
                txtSAccountName.Text,
                txtSAccountKey.Text,
                "core.windows.net",
                txtSContainerName.Text);

            jobMgr.ResourceFiles = Helpers.GetResourceFiles(sasPrefix, SampleConstants.JobManagerFiles);

            // Create the job specification, identifying that this job has a job manager associated with it
            JobSpecification jobSpec = new JobSpecification()
            {
                JobManager = jobMgr
            };

            // Set up the desired recurrence or start time schedule.
            WorkItemSchedule wiSchedule = new WorkItemSchedule();

            if (rdoOnce.IsChecked == true)
            {
                // Set information if the task is to be run once.
                DateTime runOnce = (DateTime)(dpkDate.SelectedDate);
                runOnce = runOnce.AddHours(cbxHourO.SelectedIndex);
                runOnce = runOnce.AddMinutes(cbxMinuteO.SelectedIndex);
                wiSchedule.DoNotRunUntil = runOnce;
            }
            else
            {
                // Set information if the task is to be recurring.
                TimeSpan recurring = new TimeSpan(cbxHourR.SelectedIndex, cbxMinuteR.SelectedIndex, 0);
                wiSchedule.RecurrenceInterval = recurring;
                TimeSpan countback = new TimeSpan(0, 0, 30);
                jobSpec.JobConstraints = new JobConstraints(recurring.Subtract(countback), null);
            }

            // Upload files and create workitem.
            UploadFiles();

            using (IWorkItemManager wiMgr = bClient.OpenWorkItemManager())
            {
                ICloudWorkItem workItem = wiMgr.CreateWorkItem(workItemName);
                workItem.JobExecutionEnvironment = jee;
                workItem.Schedule = wiSchedule;
                workItem.JobSpecification = jobSpec;

                try
                {
                    workItem.Commit();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
            // Remember to clean up your workitems and jobs
        }
    }
}
