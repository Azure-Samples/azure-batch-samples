using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Data;
using Microsoft.Azure.BatchExplorer.Helpers;

namespace Microsoft.Azure.BatchExplorer
{
    /// <summary>
    /// Interaction logic for ResizePool.xaml
    /// </summary>
    public partial class ResizePool : Window
    {
        public PoolViewModel Pool { get; private set; }
        private DataTable dataTableResize;

        public ResizePool(PoolViewModel pool)
        {
            InitializeComponent();

            Pool = pool;

            // Resize Timeout
            Time resizeTime = new Time(true);
            dataTableResize = resizeTime.TimeDataTable;
            datagridResize.ItemsSource = dataTableResize.AsDataView();
        }

        private void btnGo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int td = -1;
                string formula = null;
                TimeSpan? span = null;

                // formula, or TD
                if (rbtnTD.IsChecked == true)
                {
                    td = int.Parse(txtTD.Text);
                }
                else
                {
                    formula = txtAutoScale.Text;
                }

                // Resize timeout
                if (Time.CanParse(dataTableResize))
                {
                    span = Time.ParseTimeSpan(dataTableResize);
                }

                if (td != -1)
                {
                    Pool.ResizePool(td, span);
                }
                else
                {
                    Pool.EnableAutoScale(formula);
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Utils.ExtractExceptionMessage(ex));
            }
        }
    }
}
