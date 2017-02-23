using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.BatchExplorer.Models
{
    public class SubtaskModel
    {
        private readonly int? id;
        private readonly SubtaskState? state;
        private readonly string nodeId;
        private readonly int? exitCode;
        private readonly string taskRoot;

        public SubtaskModel(SubtaskInformation subtask)
        {
            this.id = subtask.Id;
            this.state = subtask.State;
            if (subtask.ComputeNodeInformation != null)
            {
                this.nodeId = subtask.ComputeNodeInformation.ComputeNodeId;
                this.taskRoot = subtask.ComputeNodeInformation.TaskRootDirectory;
            }
            this.exitCode = subtask.ExitCode;
        }

        public string Id
        {
            get { return id.HasValue ? id.Value.ToString() : String.Empty; }
        }

        public string State
        {
            get { return state.HasValue ? state.Value.ToString() : String.Empty; }
        }

        public string NodeId
        {
            get { return nodeId; }
        }

        public string TaskRootDir
        {
            get { return taskRoot;  }
        }

        public string ExitCode
        {
            get { return exitCode.HasValue ? exitCode.Value.ToString() : String.Empty; }
        }
    }
}
