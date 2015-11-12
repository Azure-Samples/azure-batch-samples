//Copyright (c) Microsoft Corporation

using System;
using System.Linq;
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

        private bool isMultiInstanceTask;
        public bool IsMultiInstanceTask
        {
            get
            {
                return this.isMultiInstanceTask;
            }
            set
            {
                this.isMultiInstanceTask = value;
                this.FirePropertyChangedEvent("IsMultiInstanceTask");
            }
        }

        private string backgroundCommand;
        public string BackgroundCommand
        {
            get
            {
                return this.backgroundCommand;
            }
            set
            {
                this.backgroundCommand = value;
                this.FirePropertyChangedEvent("BackgroundCommand");
            }
        }

        private string instanceNumber;
        public string InstanceNumber
        {
            get
            {
                return this.instanceNumber;
            }
            set
            {
                this.instanceNumber = value;
                this.FirePropertyChangedEvent("InstanceNumber");
            }
        }

        private string commonResourceFiles;
        public string CommonResourceFiles
        {
            get
            {
                return this.commonResourceFiles;
            }
            set
            {
                this.commonResourceFiles = value;
                this.FirePropertyChangedEvent("CommonResourceFiles");
            }
        }

        private string resourceFiles;
        public string ResourceFiles
        {
            get
            {
                return this.resourceFiles;
            }
            set
            {
                this.resourceFiles = value;
                this.FirePropertyChangedEvent("ResourceFiles");
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
                        TaskId = this.TaskId,
                        IsMultiInstanceTask = this.IsMultiInstanceTask,
                        ResourceFiles = ResourceFileStringParser.Parse(this.ResourceFiles).Files.ToList(),
                    };

                    if (this.isMultiInstanceTask)
                    {
                        options.BackgroundCommand = this.BackgroundCommand;
                        options.InstanceNumber = Int32.Parse(this.InstanceNumber);
                        options.CommonResourceFiles = ResourceFileStringParser.Parse(this.commonResourceFiles).Files.ToList();
                    }

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

            if (this.IsMultiInstanceTask)
            {
                if (string.IsNullOrEmpty(this.InstanceNumber))
                {
                    Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Invalid values for Instance Number"));
                }

                int i;
                if (!Int32.TryParse(this.InstanceNumber, out i))
                {
                    Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage("Instance Number must be an integer"));
                }

                var commonResourceFiles = ResourceFileStringParser.Parse(this.CommonResourceFiles);
                if (commonResourceFiles.HasErrors)
                {
                    Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(String.Join(Environment.NewLine, commonResourceFiles.Errors)));
                    return false;
                }
            }

            var resourceFiles = ResourceFileStringParser.Parse(this.ResourceFiles);
            if (resourceFiles.HasErrors)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(String.Join(Environment.NewLine, resourceFiles.Errors)));
                return false;
            }

            return true;
        }
    }
}
