using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    public enum SchedulingType
    {
        Enable = 0,
        Disable = 1
    }
    public class NodeSchedulingMessage
    {
        public SchedulingType ScheduleType { get; set; }
        
        public NodeSchedulingMessage(SchedulingType schedulingType)
        {
            this.ScheduleType = schedulingType;
        }
    }
}
