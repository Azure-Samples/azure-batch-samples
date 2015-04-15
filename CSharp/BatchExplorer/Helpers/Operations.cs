using System.Globalization;

namespace Microsoft.Azure.BatchExplorer.Helpers
{
    /// <summary>
    /// Abstract class describing an operation (e.g. GetTask, ListTasks).
    /// </summary>
    public abstract class Operation
    {
        /// <summary>
        /// The name of the operation.
        /// </summary>
        public string OperationName { get; private set; }

        /// <summary>
        /// The target of the operation.
        /// </summary>
        public abstract string OperationTarget { get; }

        protected Operation(string operationName)
        {
            this.OperationName = operationName;
        }
    }

    public class AccountOperation : Operation
    {
        public const string ListWorkItems = "ListWorkItems";
        public const string ListPools = "ListPools";
        public const string ListOSVersions = "ListOSVersions";

        public AccountOperation(string operationName)
            : base(operationName)
        {
        }

        public override string OperationTarget
        {
            get { return string.Empty; }
        }
    }

    public class WorkItemOperation : Operation
    {
        public const string Enable = "Enable";
        public const string Disable = "Disable";
        public const string Delete = "Delete";
        public const string Terminate = "Terminate";
        public const string ListJobs = "ListJobs";
        public const string Refresh = "Refresh";

        private readonly string workItemName;
        private const string OperationTargetFormatString = @"WorkItem: {0}";

        public override string OperationTarget
        {
            get { return string.Format(CultureInfo.CurrentCulture, OperationTargetFormatString, this.workItemName); }
        }

        public WorkItemOperation(string operationName, string workItemName)
            : base(operationName)
        {
            this.workItemName = workItemName;
        }
    }

    public class JobOperation : Operation
    {
        public const string Enable = "Enable";
        public const string Disable = "Disable";
        public const string Terminate = "Terminate";
        public const string Delete = "Delete";
        public const string Refresh = "Refresh";
        public const string ListTasks = "ListTasks";

        private readonly string workItemName;
        private readonly string jobName;

        private const string OperationTargetFormatString = @"WorkItem: {0}, Job: {1}";

        public override string OperationTarget
        {
            get { return string.Format(CultureInfo.CurrentCulture, OperationTargetFormatString, this.workItemName, this.jobName); }
        }

        public JobOperation(string operationName, string workItemName, string jobName)
            : base(operationName)
        {
            this.workItemName = workItemName;
            this.jobName = jobName;
        }
    }

    public class TaskOperation : Operation
    {
        public const string Terminate = "Terminate";
        public const string Delete = "Delete";
        public const string Refresh = "Refresh";
        public const string ListTaskFiles = "ListTaskFiles";
        public const string GetTaskFile = "GetTaskFile";

        private readonly string workItemName;
        private readonly string jobName;
        private readonly string taskName;

        private const string OperationTargetFormatString = @"WorkItem: {0}, Job: {1}, Task: {2}";

        public override string OperationTarget
        {
            get { return string.Format(CultureInfo.CurrentCulture, OperationTargetFormatString, this.workItemName, this.jobName, this.taskName); }
        }

        public TaskOperation(string operationName, string workItemName, string jobName, string taskName)
            : base(operationName)
        {
            this.workItemName = workItemName;
            this.jobName = jobName;
            this.taskName = taskName;
        }
    }

    public class PoolOperation : Operation
    {
        public const string AddPool = "AddPool";
        public const string Resize = "Resize";
        public const string Delete = "Delete";
        public const string Refresh = "Refresh";
        public const string ListVMs = "ListVMs";
        public const string GetPool = "GetPool";

        private readonly string poolName;
        private const string OperationTargetFormatString = @"Pool: {0}";

        public override string OperationTarget
        {
            get { return string.Format(CultureInfo.CurrentCulture, OperationTargetFormatString, this.poolName); }
        }

        public PoolOperation(string operationName, string poolName)
            : base(operationName)
        {
            this.poolName = poolName;
        }
    }

    public class VMOperation : Operation
    {
        public const string Reboot = "Reboot";
        public const string Reimage = "Reimage";
        public const string Refresh = "Refresh";
        public const string ListVMFiles = "ListVMFiles";
        public const string GetRdp = "GetRdp";
        public const string GetVMFile = "GetVMFile";
        public const string CreateUser = "CreateUser";

        private readonly string poolName;
        private readonly string vmName;
        private const string OperationTargetFormatString = @"Pool: {0}, VM: {1}";

        public override string OperationTarget
        {
            get { return string.Format(CultureInfo.CurrentCulture, OperationTargetFormatString, this.poolName, this.vmName); }
        }

        public VMOperation(string operationName, string poolName, string vmName)
            : base(operationName)
        {
            this.poolName = poolName;
            this.vmName = vmName;
        }
    }
}
