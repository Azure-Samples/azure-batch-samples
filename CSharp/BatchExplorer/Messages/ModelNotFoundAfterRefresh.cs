
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    public class ModelNotFoundAfterRefresh
    {
        public ModelBase Model { get; private set; }

        public ModelNotFoundAfterRefresh(ModelBase model)
        {
            this.Model = model;
        }
    }
}
