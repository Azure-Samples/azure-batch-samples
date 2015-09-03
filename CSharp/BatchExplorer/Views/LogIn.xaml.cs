//Copyright (c) Microsoft Corporation

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
using System.Configuration;
using Microsoft.Azure.BatchExplorer.Models;

namespace Microsoft.Azure.BatchExplorer
{
    /// <summary>
    /// Interaction logic for LogIn.xaml
    /// </summary>
    public partial class LogIn : Window
    {
        private MainWindow window { get; set; }

        public LogIn(MainWindow win)
        {
            window = win;
            InitializeComponent();
            MainWindow.Resize(MainGrid);
        }

        public LogIn(MainWindow win, string alias)
        {
            window = win;
            InitializeComponent();

            Account account = null;

            foreach (Account a in window.accounts.Accounts)
            {
                if (a.Alias.Equals(alias))
                {
                    account = a;
                }
            }

            if (account == null)
            {
                MessageBox.Show("Error - editing an account that isn't there");
                this.Close();
            }

            window.accounts.RemoveAccount(account);

            txtAccount.Text = account.AccountName;
            txtAlias.Text = account.Alias;
            txtKey.Text = account.Key;
            txtUrl.Text = account.TaskTenantUrl;

            MainWindow.Resize(MainGrid);
        }

        private void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string alias = txtAlias.Text;
                string taskTenantUrl = txtUrl.Text;
                string accountName = txtAccount.Text;
                string key = txtKey.Text;

                Account account = new Account(alias, accountName, taskTenantUrl, key);
                window.accounts.AddAccount(account);
                window.accounts.Serialize();
                window.AccountsComboBox.Items.Add(new ComboBoxItem(){Content = alias });

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Add Account:\n{0}", ex));
            }
        }
    }
}
