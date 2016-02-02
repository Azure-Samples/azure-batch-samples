using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.BatchExplorer.ViewModels
{
    public class CreateCertificateViewModel : EntityBase
    {
        private readonly IDataProvider batchService;

        public CreateCertificateViewModel(IDataProvider batchService)
        {
            this.batchService = batchService;
        }
        
        private string filePath;

        public string FilePath
        {
            get
            {
                return this.filePath;
            }
            set
            {
                this.filePath = value;
                this.FirePropertyChangedEvent("FilePath");
                this.FirePropertyChangedEvent("IsCreateCertificateButtonEnabled");
            }
        }

        private string password;

        public string Password
        {
            get
            {
                return this.password;
            }
            set
            {
                this.password = value;
                this.FirePropertyChangedEvent("Password");
                this.FirePropertyChangedEvent("IsCreateCertificateButtonEnabled");
            }
        }

        public bool IsCreateCertificateButtonEnabled
        {
            get
            {
                if (!FilePathExists())
                {
                    return false;
                }

                var format = Format();

                if (format == null)
                {
                    return false;
                }

                if (format.Value == CertificateFormat.Pfx && string.IsNullOrEmpty(Password))
                {
                    return false;
                }

                return true;
            }
        }

        public CommandBase CreateCertificate
        {
            get
            {
                return new CommandBase(
                    async (o) =>
                    {
                        try
                        {
                            this.IsBusy = true;
                            await this.CreateCertificateAsync();
                        }
                        finally
                        {
                            this.IsBusy = false;
                        }
                    }
                );
            }
        }

        private bool FilePathExists()
        {
            if (String.IsNullOrWhiteSpace(FilePath))
            {
                return false;
            }

            try
            {
                return System.IO.File.Exists(FilePath);
            }
            catch
            {
                return false;
            }
        }

        private CertificateFormat? Format()
        {
            var fileExtension = System.IO.Path.GetExtension(FilePath);
            var comparer = StringComparer.OrdinalIgnoreCase;

            if (comparer.Equals(".pfx", fileExtension))
            {
                return CertificateFormat.Pfx;
            }
            if (comparer.Equals(".cer", fileExtension))
            {
                return CertificateFormat.Cer;
            }
            return null;
        }

        private async Task CreateCertificateAsync()
        {
            try
            {
                var certificateFormat = Format();

                if (certificateFormat == null)
                {
                    return;  // shouldn't have got here
                }

                CreateCertificateOptions options = new CreateCertificateOptions
                {
                    FilePath = FilePath,
                    CertificateFormat = certificateFormat.Value,
                    Password = Password,
                };

                await this.batchService.CreateCertificateAsync(options);

                Messenger.Default.Send<RefreshMessage>(new RefreshMessage(RefreshTarget.Certificates));

                Messenger.Default.Send(new GenericDialogMessage(string.Format("Successfully uploaded certificate from {0}", this.FilePath)));
                this.FilePath = string.Empty; //So that the user doesn't accidentally try to upload the same certificate twice
            }
            catch (Exception e)
            {
                Messenger.Default.Send<GenericDialogMessage>(new GenericDialogMessage(e.ToString()));
            }
        }
    }
}
