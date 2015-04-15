using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.Batch;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;

namespace Microsoft.Azure.BatchExplorer.Models
{
    /// <summary>
    /// Model representing the different options selected by the user
    /// </summary>
    [DataContract]
    public class OptionsModel : EntityBase
    {
        #region Public properties

        //TODO: Note that this is not thread safe currently

        public ODATADetailLevel ListDetailLevel { get; set; }

        [DataMember]
        public int MaxTrackedOperations { get; set; }

        [DataMember]
        public bool DisplayOperationHistory
        {
            get { return this.displayOperationHistory; }
            set
            {
                this.displayOperationHistory = value;
                Messenger.Default.Send(new ShowAsyncOperationTabMessage(this.displayOperationHistory));
                this.FirePropertyChangedEvent("DisplayOperationHistory");
            }
        }

        [DataMember]
        public bool UseStatsDuringList
        {
            get
            {
                return this.useStatsDuringList;
            }
            set
            {
                this.useStatsDuringList = value;
                this.ListDetailLevel = this.useStatsDuringList ? StatsDetailLevel : null;
            }
        }

        #endregion

        public const string OptionsFileName = "Options.xml";

        //See: http://csharpindepth.com/articles/general/singleton.aspx for other implementations of the singleton pattern
        private static OptionsModel instance;
        private static readonly object instanceLock = new object();

        public static OptionsModel Instance
        {
            get
            {
                //Need thread safety
                lock (instanceLock)
                {
                    if (instance == null)
                    {
                        instance = LoadOptions();
                    }
                    return instance;
                }
            }
        }

        public static ODATADetailLevel StatsDetailLevel = new ODATADetailLevel() { ExpandClause = "stats" };
        private bool displayOperationHistory;
        private bool useStatsDuringList;

        private OptionsModel()
        {
            // TODO: Pick and choose which properties we need to download initially
            //this.ListDetailLevel = new ODATADetailLevel() { SelectClause = "name,state,creationTime,executionInfo" };

            this.ListDetailLevel = new ODATADetailLevel();
            this.MaxTrackedOperations = AsyncOperationTracker.DefaultMaxTrackedOperations;
            this.DisplayOperationHistory = true; //Show operation history by default
            this.UseStatsDuringList = false; //By default we don't use stats during list
        }

        #region Serialization helpers

        /// <summary>
        /// Writes the options to their file location on disk.
        /// </summary>
        public void WriteOptions()
        {
            string filePath = OptionsModel.GetOptionsFilePath();
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
            {
                DataContractSerializer serializer = new DataContractSerializer(typeof(OptionsModel));
                XmlWriterSettings settings = new XmlWriterSettings { Indent = true };
                using (XmlWriter writer = XmlWriter.Create(fileStream, settings))
                {
                    serializer.WriteObject(writer, this);
                }
            }
        }

        /// <summary>
        /// Loads the options from their location on disk.
        /// </summary>
        /// <returns>An OptionsModel instance with the loaded options.</returns>
        private static OptionsModel LoadOptions()
        {
            string filePath = OptionsModel.GetOptionsFilePath();

            OptionsModel result;
            if (File.Exists(filePath))
            {
                try
                {
                    using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
                    {
                        DataContractSerializer serializer = new DataContractSerializer(typeof(OptionsModel));
                        result = (OptionsModel)serializer.ReadObject(fileStream);
                    }
                }
                catch (Exception)
                {
                    //Ignoring exception and skipping file load
                    result = new OptionsModel();
                }
            }
            else
            {
                result = new OptionsModel();
            }

            return result;
        }

        /// <summary>
        /// Gets the path of the options file.
        /// </summary>
        /// <returns>The path of the options file.</returns>
        private static string GetOptionsFilePath()
        {
            // create the directory if necessary
            string fullDirectoryPath = Path.Combine(Common.LocalAppDataDirectory, Common.LocalAppDataSubfolder);
            Directory.CreateDirectory(fullDirectoryPath);

            string path = Path.Combine(fullDirectoryPath, OptionsModel.OptionsFileName);

            return path;
        }

        #endregion
    }
}
