//Copyright (c) Microsoft Corporation

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
        public const string ListJobSchedules = "ListJobSchedules";
        public const string ListJobs = "ListJobs";
        public const string ListPools = "ListPools";
        public const string ListOSVersions = "ListOSVersions";
        public const string ListCertificates = "ListCertificates";

        public AccountOperation(string operationName)
            : base(operationName)
        {
        }

        public override string OperationTarget
        {
            get { return string.Empty; }
        }
    }

    public class JobScheduleOperation : Operation
    {
        public const string Enable = "Enable";
        public const string Disable = "Disable";
        public const string Delete = "Delete";
        public const string Terminate = "Terminate";
        public const string ListJobs = "ListJobs";
        public const string Refresh = "Refresh";

        private readonly string jobScheduleId;
        private const string OperationTargetFormatString = @"JobSchedule: {0}";

        public override string OperationTarget
        {
            get { return string.Format(CultureInfo.CurrentCulture, OperationTargetFormatString, this.jobScheduleId); }
        }

        public JobScheduleOperation(string operationName, string jobScheduleId)
            : base(operationName)
        {
            this.jobScheduleId = jobScheduleId;
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

        private readonly string jobId;

        private const string OperationTargetFormatString = @"Job: {0}";

        public override string OperationTarget
        {
            get { return string.Format(CultureInfo.CurrentCulture, OperationTargetFormatString, this.jobId); }
        }

        public JobOperation(string operationName, string jobId)
            : base(operationName)
        {
            this.jobId = jobId;
        }
    }

    public class TaskOperation : Operation
    {
        public const string Terminate = "Terminate";
        public const string Delete = "Delete";
        public const string Refresh = "Refresh";
        public const string ListFiles = "ListFiles";
        public const string GetFile = "GetFile";

        private readonly string jobId;
        private readonly string taskId;

        private const string OperationTargetFormatString = @"Job: {0}, Task: {1}";

        public override string OperationTarget
        {
            get { return string.Format(CultureInfo.CurrentCulture, OperationTargetFormatString, this.jobId, this.taskId); }
        }

        public TaskOperation(string operationName, string jobId, string taskId)
            : base(operationName)
        {
            this.jobId = jobId;
            this.taskId = taskId;
        }
    }

    public class PoolOperation : Operation
    {
        public const string AddPool = "AddPool";
        public const string Resize = "Resize";
        public const string Delete = "Delete";
        public const string Refresh = "Refresh";
        public const string ListComputeNodes = "ListComputeNodes";
        public const string GetPool = "GetPool";

        private readonly string poolId;
        private const string OperationTargetFormatString = @"Pool: {0}";

        public override string OperationTarget
        {
            get { return string.Format(CultureInfo.CurrentCulture, OperationTargetFormatString, this.poolId); }
        }

        public PoolOperation(string operationName, string poolId)
            : base(operationName)
        {
            this.poolId = poolId;
        }
    }

    public class ComputeNodeOperation : Operation
    {
        public const string Reboot = "Reboot";
        public const string Reimage = "Reimage";
        public const string Refresh = "Refresh";
        public const string ListFiles = "ListFiles";
        public const string GetRdp = "GetRdp";
        public const string GetFile = "GetFile";
        public const string CreateUser = "CreateUser";
        public const string DisableScheduling = "DisableScheduling";
        public const string EnableScheduling = "EnableScheduling";

        private readonly string poolId;
        private readonly string nodeId;
        private const string OperationTargetFormatString = @"Pool: {0}, Node: {1}";

        public override string OperationTarget
        {
            get { return string.Format(CultureInfo.CurrentCulture, OperationTargetFormatString, this.poolId, this.nodeId); }
        }

        public ComputeNodeOperation(string operationName, string poolId, string nodeId)
            : base(operationName)
        {
            this.poolId = poolId;
            this.nodeId = nodeId;
        }
    }

    public class CertificateOperation : Operation
    {
        public const string Refresh = "Refresh";

        private readonly string thumbprint;
        private readonly string thumbprintAlgorithm;
        private const string OperationTargetFormatString = @"Certificate: {0} ({1})";

        public override string OperationTarget
        {
            get { return string.Format(CultureInfo.CurrentCulture, OperationTargetFormatString, this.thumbprint, this.thumbprintAlgorithm); }
        }

        public CertificateOperation(string operationName, string thumbprint, string thumbprintAlgorithm)
            : base(operationName)
        {
            this.thumbprint = thumbprint;
            this.thumbprintAlgorithm = thumbprintAlgorithm;
        }
    }
}
