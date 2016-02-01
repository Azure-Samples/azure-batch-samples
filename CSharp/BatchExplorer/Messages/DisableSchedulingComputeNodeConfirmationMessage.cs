//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.BatchExplorer.Messages
{
    public enum ComputeNodeDisableSchedulingConfimation
    {
        Confirmed,
        Cancelled
    }

    public class DisableSchedulingComputeNodeConfirmationMessage
    {
        public ComputeNodeDisableSchedulingConfimation Confirmation { get; private set; }

        public DisableSchedulingComputeNodeConfirmationMessage(ComputeNodeDisableSchedulingConfimation confirmation)
        {
            this.Confirmation = confirmation;
        }
    }
}
