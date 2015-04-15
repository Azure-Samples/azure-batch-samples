using System;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.ViewModels
{
    public class AddTaskViewModel : EntityBase
    {
        #region Services
        private readonly IDataProvider batchService;
        #endregion

        #region Public UI Properties
        
        private string workItemName;
        public string WorkItemName
        {
            get
            {
                return this.workItemName;
            }
            set
            {
                this.workItemName = value;
                this.FirePropertyChangedEvent("WorkItemName");
            }
        }

        private string jobName;
        public string JobName
        {
            get
            {
                return this.jobName;
            }
            set
            {
                this.jobName = value;
                this.FirePropertyChangedEvent("JobName");
            }
        }

        // Basic Task Details
        private string taskName;
        public string TaskName
        {
            get
            {
                return this.taskName;
            }
            set
            {
                this.taskName = value;
                this.FirePropertyChangedEvent("TaskName");
            }
        }

        private string commandLine;
        public string CommandLine
        {
            get
            {
                return this.commandLine;
            }
            set
            {
                this.commandLine = value;
                this.FirePropertyChangedEvent("CommandLine");
            }
        }
        #endregion

        #region Commands
        public CommandBase AddTask
        {
            get
            {
                return new CommandBase(
                    async (o) =>
                    {
                        try
                        {
                            this.IsBusy = true;
                            await this.AddTaskAsync();
                        }
                        finally
                        {
                            this.IsBusy = false;
                        }
                    }
                );
            }
        }
        #endregion

        public AddTaskViewModel(IDataProvider batchService, string workItemName, string jobName)
        {
            this.batchService = batchService;
            this.IsBusy = false;

            this.WorkItemName = workItemName;
            this.JobName = jobName;
        }

        private async Task AddTaskAsync()
        {
            try
            {
                if (this.IsInputValid())
                {
                    AddTaskOptions options = new AddTaskOptions()
                    {
                        WorkItemName = this.workItemName,
                        JobName = this.jobName,
                        CommandLine = this.CommandLine,
                        TaskName = this.TaskName
                    };

                    await this.batchService.AddTask(options);

                    Messenger.Default.Send(new GenericDialogMessage(string.Format("Successfully added Task {0}", this.TaskName)));
                    this.TaskName = string.Empty; //So that the user cannot accidentally try to create the same task twice
                }
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        private bool IsInputValid()
        {
            if (string.IsNullOrEmpty(this.TaskName))
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid values for Task Name"));
                return false;
            }

            if (string.IsNullOrEmpty(this.CommandLine))
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid values for Command Line"));
                return false;
            }

            return true;
        }
    }
}
