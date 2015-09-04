//Copyright (c) Microsoft Corporation

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Microsoft.Azure.BatchExplorer.Views
{
    /// <summary>
    /// Interaction logic for WaitSpinner.xaml
    /// </summary>
    public partial class WaitSpinner : UserControl
    {
        public WaitSpinner()
        {
            InitializeComponent();
            this.IsVisibleChanged += OnVisibleChanged;
        }

        private Storyboard storyboard;

        private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ( IsVisible )
            {
                StartAnimation();
            }
            else
            {
                StopAnimation();
            }
        }

        private void StartAnimation()
        {
            this.storyboard = (Storyboard)FindResource("canvasAnimation");
            this.storyboard.Begin(canvas, true);
        }

        private void StopAnimation()
        {
            this.storyboard.Stop(canvas);
            this.storyboard.Remove(canvas);
        }
    }
}
