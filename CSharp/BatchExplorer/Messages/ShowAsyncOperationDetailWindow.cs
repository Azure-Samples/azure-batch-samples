//Copyright (c) Microsoft Corporation

using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.Messages
{
    public class ShowAsyncOperationDetailWindow
    {
        public AsyncOperationModel AsyncOperation { get; private set; }

        public ShowAsyncOperationDetailWindow(AsyncOperationModel asyncOperation)
        {
            this.AsyncOperation = asyncOperation;
        }
    }
}
