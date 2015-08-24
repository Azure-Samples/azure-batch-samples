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
        

        private string jobId;
        public string JobId
        {
            get
            {
                return this.jobId;
            }
            set
            {
                this.jobId = value;
                this.FirePropertyChangedEvent("JobId");
            }
        }

        // Basic Task Details
        private string taskId;
        public string TaskId
        {
            get
            {
                return this.taskId;
            }
            set
            {
                this.taskId = value;
                this.FirePropertyChangedEvent("TaskId");
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

        public AddTaskViewModel(IDataProvider batchService, string jobId)
        {
            this.batchService = batchService;
            this.IsBusy = false;

            this.JobId = jobId;
        }

        private async Task AddTaskAsync()
        {
            try
            {
                if (this.IsInputValid())
                {
                    AddTaskOptions options = new AddTaskOptions()
                    {
                        JobId = this.jobId,
                        CommandLine = this.CommandLine,
                        TaskId = this.TaskId
                    };

                    await this.batchService.AddTaskAsync(options);

                    Messenger.Default.Send(new GenericDialogMessage(string.Format("Successfully added Task {0}", this.TaskId)));
                    this.TaskId = string.Empty; //So that the user cannot accidentally try to create the same task twice
                }
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }

        private bool IsInputValid()
        {
            if (string.IsNullOrEmpty(this.TaskId))
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid values for Task Id"));
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
