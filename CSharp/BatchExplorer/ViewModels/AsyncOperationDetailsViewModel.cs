//Copyright (c) Microsoft Corporation

using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.ViewModels
{
    public class AsyncOperationDetailsViewModel : EntityBase
    {
        #region Public UI Properties

        public AsyncOperationModel AsyncOperation { get; private set; }

        #endregion

        public AsyncOperationDetailsViewModel(AsyncOperationModel model)
        {
            this.AsyncOperation = model;
        }
    }
}
