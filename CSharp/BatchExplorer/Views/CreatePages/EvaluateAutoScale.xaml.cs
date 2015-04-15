using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Azure.BatchExplorer.Helpers;

namespace Microsoft.Azure.BatchExplorer
{
    public partial class EvaluateAutoScale : Window
    {
        public PoolViewModel Pool { get; private set; }

        public EvaluateAutoScale(PoolViewModel pool)
        {
            InitializeComponent();

            Pool = pool;
        }

        private void btnEvaluate_Click(object sender, EventArgs e)
        {
            try
            {
                string formula = txtFormula.Text;
                DataTable datatable = Pool.EvaluateAutoScale(formula);

                DataGrid datagrid = new DataGrid();
                DataView dataView = datatable.AsDataView();
                datagrid.ItemsSource = dataView;

                datagrid.IsReadOnly = true;
                datagrid.HorizontalGridLinesBrush = SettingsValues.GRID_LINE_COLOR;
                datagrid.VerticalGridLinesBrush = SettingsValues.GRID_LINE_COLOR;
                datagrid.AlternatingRowBackground = System.Windows.Media.Brushes.WhiteSmoke;
                datagrid.RowBackground = System.Windows.Media.Brushes.AliceBlue;
                datagrid.CanUserAddRows = false;

                lblResults.Visibility = System.Windows.Visibility.Visible;
                MainGrid.Children.Add(datagrid);
                Grid.SetRow(datagrid, 3);
                Grid.SetColumnSpan(datagrid, 2);
                this.Width = datagrid.Width;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Utils.ExtractExceptionMessage(ex));
            }
        }
    }
}
