using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.BatchExplorer.Models
{
    public class CertificateModel : ModelBase
    {
        public CertificateModel(Certificate source)
        {
            this.Certificate = source;
        }

        [ChangeTracked(ModelRefreshType.Basic)]
        public string Thumbprint
        {
            get { return this.Certificate.Thumbprint; }
        }

        [ChangeTracked(ModelRefreshType.Basic)]
        public string ThumbprintAlgorithm
        {
            get { return this.Certificate.ThumbprintAlgorithm; }
        }

        [ChangeTracked(ModelRefreshType.Basic)]
        public CertificateState? State
        {
            get { return this.Certificate.State; }
        }

        private Certificate Certificate { get; set; }

        public override List<PropertyModel> PropertyModel
        {
            get { return this.ObjectToPropertyModel(this.Certificate); }
        }

        public override async Task RefreshAsync(ModelRefreshType refreshType, bool showTrackedOperation = true)
        {
            Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.UpperRight, true));

            if (refreshType.HasFlag(ModelRefreshType.Basic))
            {
                try
                {
                    System.Threading.Tasks.Task asyncTask = this.Certificate.RefreshAsync(OptionsModel.Instance.ListDetailLevel);
                    if (showTrackedOperation)
                    {
                        AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                            asyncTask,
                            new CertificateOperation(CertificateOperation.Refresh, this.Certificate.Thumbprint, this.Certificate.ThumbprintAlgorithm)));
                    }
                    else
                    {
                        AsyncOperationTracker.Instance.AddTrackedInternalOperation(asyncTask);
                    }

                    await asyncTask;
                    this.LastUpdatedTime = DateTime.UtcNow;

                    //
                    // Fire property change events for this models properties
                    //
                    this.FireChangesOnRefresh(ModelRefreshType.Basic);
                }
                catch (Exception e)
                {
                    this.HandleException(e);
                }
            }

            Messenger.Default.Send(new UpdateWaitSpinnerMessage(WaitSpinnerPanel.UpperRight, false));
            Messenger.Default.Send(new CertificateUpdateCompleteMessage());
        }

        private void HandleException(Exception e)
        {
            //Swallow 404's and fire a message
            if (Microsoft.Azure.BatchExplorer.Helpers.Common.IsExceptionNotFound(e))
            {
                Messenger.Default.Send(new ModelNotFoundAfterRefresh(this));
            }
            else
            {
                Messenger.Default.Send(new GenericDialogMessage(e.ToString()));
            }
        }

        /// <summary>
        /// Delete this certificate from the server
        /// </summary>
        public async Task DeleteAsync()
        {
            try
            {
                Task asyncTask = this.Certificate.DeleteAsync();
                AsyncOperationTracker.Instance.AddTrackedOperation(new AsyncOperationModel(
                    asyncTask,
                    new CertificateOperation(CertificateOperation.Delete, this.Certificate.Thumbprint, this.Certificate.ThumbprintAlgorithm)));
                await asyncTask;
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }
    }
}
