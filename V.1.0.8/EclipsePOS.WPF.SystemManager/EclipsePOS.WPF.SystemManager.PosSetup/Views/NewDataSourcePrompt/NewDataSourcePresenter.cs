using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Windows.Threading;
using System.Threading;

using System.Text.RegularExpressions;
using Microsoft.Practices.Composite.Presentation.Events;
using Microsoft.Practices.Composite.Presentation.Commands;



using EclipsePOS.WPF.SystemManager.PosSetup.Util;


namespace EclipsePOS.WPF.SystemManager.PosSetup.Views.NewDataSourcePrompt
{
    public class NewDataSourcePresenter
    {
        private SqlConnection conn;
        private string database1;
        private string server1;
        private INewDataSource _view;

        private SqlConnection sqlCon = new SqlConnection();
        private SqlCommand sqlCmd = new SqlCommand();

        public DelegateCommand<object> OKCommand;
        public delegate void RunCreateDatabaseDelegate();

        
        public NewDataSourcePresenter()
        {
            OKCommand = new DelegateCommand<object>(OnOKCommandExecute, OnOKCommandCanExecute);
            
        }

        public INewDataSource View
        {
            set
            {
                _view = value;
                View.SetOKBtnDataContext(OKCommand);
               // View.SetCurrDataSourcePath(this.GetCurrentDatabasePathAndSize());  
              
            }
            get
            {
                return _view;
            }
        }

        #region OK Command

        public void OnOKCommandExecute(object obj)
        {
           // MessageBox.Show("OK command"); 
            if (!View.FieldEmpty())
            {
               
               //this.SetupDatabase( View.ServerInstance , View.Authentication, this.View.UserName, this.View.Password, this.View.DatabaseName);
                RunCreateDatabaseDelegate runCreateDatabase = new RunCreateDatabaseDelegate(this.SetupDatabaseHelper);
                runCreateDatabase.BeginInvoke(null, null);
            }
        }

        public bool OnOKCommandCanExecute(object obj)
        {
            // Implement business logic for myCommand enablement.
            return true;
        }

        #endregion

        public void SetupDatabaseHelper()
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send, (SendOrPostCallback)delegate { View.StartSyncAnimation(); }, null);
            this.SetupDatabase(View.ServerInstance, View.Authentication, this.View.UserName, this.View.Password, this.View.DatabaseName);
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send, (SendOrPostCallback)delegate { View.EndSyncAnimation(); }, null);
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send, (SendOrPostCallback)delegate { View.SetDataSourcePath(); }, null);
    
        }


       
        public bool SetupDatabase(string server, int autMode, string user, string password, string databaseName)
        {
            if (View.Mode != CreateMode.ExistingDatabase)
            {
                MessageBox.Show("Use scripts/Set-EclipseDatabase.ps1 in the Windows package to create or upgrade a SQL Server 2025 database. See WINDOWS-SETUP.md. The destructive legacy wizard has been retired.", "Database setup");
                return false;
            }
            try
            {
                var builder = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = databaseName, IntegratedSecurity = true, Encrypt = true };
                if (autMode != 0) throw new InvalidOperationException("Use Windows authentication. SQL passwords are no longer stored in plain-text application settings.");
                using (var connection = new SqlConnection(builder.ConnectionString))
                using (var command = connection.CreateCommand())
                {
                    connection.Open();
                    command.CommandText = "SELECT MAX(version) FROM dbo.EclipseSchemaVersion";
                    if (Convert.ToInt32(command.ExecuteScalar()) != 2) throw new InvalidOperationException("Upgrade the database to schema 2 first.");
                }
                PosSettings.Default.DataSource = builder.ConnectionString;
                PosSettings.Default.possiteConnectionString = builder.ConnectionString;
                PosSettings.Default.Save();
                return true;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Database configuration failed"); return false; }
        }
        public bool RunScript(string strFile)
        {
         
            int count = 0;
            string[] strCommands;
            strCommands = ParseScriptToCommands(strFile);
            try
            {
                if (sqlCon.State != ConnectionState.Open) sqlCon.Open();
                
                sqlCmd.Connection = sqlCon;

                foreach (string strCmd in strCommands)
                {
                    if (strCmd.Length > 0)
                    {
                        try
                        {
                            count++;
                           // View.UpdateProgress(count);
                            sqlCmd.CommandText = strCmd;
                            sqlCmd.ExecuteNonQuery();
                        }
                        catch (SqlException sql_ex0)
                        {
                            MessageBox.Show(sql_ex0.Number.ToString() + " " + sql_ex0.Message.ToString() + "\n" +strCmd);
                            return false;
                        }
                    }

                }
            }
            catch (SqlException sql_ex)
            {
                MessageBox.Show(sql_ex.Number.ToString() + " " + sql_ex.Message.ToString()  );
                return false;
            }
            //MessageBox.Show(count.ToString());
            
            return true;
        }

        public string[] ParseScriptToCommands(string strScript)
        {
            string[] commands;
            commands = Regex.Split(strScript, "GO\r\n", RegexOptions.IgnoreCase);
            return commands;
        }


        public string GetCurrentDatabasePathAndSize()
        {
            StringBuilder output = new StringBuilder();

            string connString = PosSettings.Default.DataSource;
            SqlConnection conn = new SqlConnection(connString);

            try
            {
                conn.Open();
                SqlCommand command = new SqlCommand("select filename, size from dbo.sysfiles", conn);
                SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    // MessageBox.Show(reader[0].ToString());
                    // MessageBox.Show(reader[1].ToString());
                    output.Append(reader[0].ToString());
                    output.Append(", Size=");
                    output.Append(reader[1].ToString());
                    output.AppendLine(" KB");
                }



            }
            catch (Exception e)
            {

                // MessageBox.Show(e.ToString());
            }
            finally
            {
                conn.Close();
            }

            return output.ToString();
        }


        
    }
}
