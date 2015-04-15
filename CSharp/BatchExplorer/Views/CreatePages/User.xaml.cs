using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Data;
using System.Windows.Controls;
using Microsoft.Azure.BatchExplorer.Helpers;

namespace Microsoft.Azure.BatchExplorer
{
    /// <summary>
    /// Interaction logic for User.xaml
    /// </summary>
    public enum UserOperation { ADD, EDIT, REMOVE }
    public partial class User : Window
    {
        private DataTable dataTableExpire;
        public UserOperation Operation { get; internal set; }
        public TVMViewModel TVM_VM { get; internal set; }

        public User(UserOperation operation, TVMViewModel vm)
        {
            InitializeComponent();

            Operation = operation;
            TVM_VM = vm;
            //System.Windows.Forms.TextBox txtPassword = new TextBox();
            //txtPassword.PasswordChar = '*';
            //MainGrid.Children.Add(txtPassword as UIElement);
            txtPassword.PasswordChar = '*';
            

            // Expiration Time DataGrid
            Time expiryTime = new Time(false);
            dataTableExpire = expiryTime.TimeDataTable;
            timeExpiry.ItemsSource = dataTableExpire.AsDataView();

            if (Operation == UserOperation.ADD)
            {
                lblName.IsEnabled = true;
                lblPassword.IsEnabled = true;
                lblExpirtyTime.IsEnabled = true;
                txtName.IsEnabled = true;
                txtPassword.IsEnabled = true;
                lblIsAdmin.IsEnabled = true;
                rbtnIsAdminT.IsEnabled = true;
                rbtnIsAdminF.IsEnabled = true;
                dateExpirty.IsEnabled = true;
                timeExpiry.IsEnabled = true;
            }
            else if (Operation == UserOperation.EDIT)
            {
                lblName.IsEnabled = true;
                lblPassword.IsEnabled = true;
                lblExpirtyTime.IsEnabled = true;
                txtName.IsEnabled = true;
                txtPassword.IsEnabled = true;
                dateExpirty.IsEnabled = true;
                timeExpiry.IsEnabled = true;
                lblIsAdmin.IsEnabled = false;
                rbtnIsAdminT.IsEnabled = false;
                rbtnIsAdminF.IsEnabled = false;
                lblIsAdmin.Visibility = System.Windows.Visibility.Hidden;
                rbtnIsAdminT.Visibility = System.Windows.Visibility.Hidden;
                rbtnIsAdminF.Visibility = System.Windows.Visibility.Hidden;
            }
            else if (Operation == UserOperation.REMOVE)
            {
                lblName.IsEnabled = true;
                lblPassword.IsEnabled = false;
                lblExpirtyTime.IsEnabled = false;
                txtName.IsEnabled = true;
                txtPassword.IsEnabled = false;
                dateExpirty.IsEnabled = false;
                timeExpiry.IsEnabled = false;
                lblIsAdmin.IsEnabled = false;
                rbtnIsAdminT.IsEnabled = false;
                rbtnIsAdminF.IsEnabled = false;

                lblPassword.Visibility = System.Windows.Visibility.Hidden;
                txtPassword.Visibility = System.Windows.Visibility.Hidden;
                dateExpirty.Visibility = System.Windows.Visibility.Hidden;
                timeExpiry.Visibility = System.Windows.Visibility.Hidden;
                lblExpirtyTime.Visibility = System.Windows.Visibility.Hidden;
                s1.Visibility = System.Windows.Visibility.Hidden;
                s2.Visibility = System.Windows.Visibility.Hidden;
                lblIsAdmin.Visibility = System.Windows.Visibility.Hidden;
                rbtnIsAdminT.Visibility = System.Windows.Visibility.Hidden;
                rbtnIsAdminF.Visibility = System.Windows.Visibility.Hidden;

                Grid.SetRow(btnGo, 2);
                Height = 200;
            }
            else
            {
                System.Windows.MessageBox.Show("Invalid user operation - this should NEVER happen");
            }
        }

        private void btnDone_Click(object sender, EventArgs e)
        {
            try
            {
                string name = txtName.Text;

                string password = txtPassword.Password;

                DateTime? expirtyDate = dateExpirty.SelectedDate;
                if (expirtyDate != null && Time.CanParse(dataTableExpire))
                {
                    TimeSpan span = Time.ParseDateTime(dataTableExpire);
                    expirtyDate = expirtyDate.Value.Add(span);
                }

                if (Operation == UserOperation.ADD)
                {
                    bool isAdmin = (rbtnIsAdminT.IsChecked == true) ? true :
                        (rbtnIsAdminF.IsChecked == true) ? false : false;
                    TVM_VM.AddUser(name, password, isAdmin, expirtyDate);
                    Close();
                }
                else if (Operation == UserOperation.EDIT)
                {
                    TVM_VM.UpdateUser(name, password, expirtyDate);
                    Close();
                }
                else if (Operation == UserOperation.REMOVE)
                {
                    TVM_VM.RemoveUser(name);
                    Close();
                }
                else
                {
                    System.Windows.MessageBox.Show("Invalid user operation - this should NEVER happen");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Utils.ExtractExceptionMessage(ex));
            }


        }
    }
}
