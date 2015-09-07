//Copyright (c) Microsoft Corporation

using System;
using System.Collections.ObjectModel;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Azure.BatchExplorer.Helpers;
using Microsoft.Azure.BatchExplorer.Messages;
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer.ViewModels
{
    public class HeatMapViewModel : EntityBase
    {
        #region Public UI Properties

        public HeatMapModel HeatMap { get; private set; }

        public ObservableCollection<int> RefreshIntervalChoices { get; private set; }
        
        private int refreshIntervalSeconds;
        public int RefreshIntervalSeconds
        {
            get { return this.refreshIntervalSeconds; }
            set
            {
                this.refreshIntervalSeconds = value;
                Messenger.Default.Send(new HeatMapPollIntervalUpdatedMessage(this));
            }
        }

        public TimeSpan RefreshInterval
        {
            get { return TimeSpan.FromSeconds(this.RefreshIntervalSeconds); }
        }

        #endregion
        
        public HeatMapViewModel(HeatMapModel heatMapModel)
        {
            this.HeatMap = heatMapModel;

            this.RefreshIntervalChoices = new ObservableCollection<int>()
                                              {
                                                  1, 5, 15, 30, 60, 180
                                              };
            this.RefreshIntervalSeconds = 30; //Default
        }

    }
}
