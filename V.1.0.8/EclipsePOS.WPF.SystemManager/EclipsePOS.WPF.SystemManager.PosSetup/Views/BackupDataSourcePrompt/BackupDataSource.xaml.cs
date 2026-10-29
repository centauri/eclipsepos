using System;
using System.Threading.Tasks;
using System.Windows;

namespace EclipsePOS.WPF.SystemManager.PosSetup.Views.BackupDataSourcePrompt
{
    public partial class BackupDataSource : Window, IBackupDataSource
    {
        public BackupDataSource() { InitializeComponent(); }
        public int ShowInputDialog() { return ShowDialog() == true ? 1 : 0; }
        private async void Backup_Click(object sender, RoutedEventArgs e)
        {
            RunBackup.IsEnabled = false;
            string path = BackupPath.Text;
            try
            {
                await Task.Run(() => new BackupDataSourcePresenter().Backup(path));
                MessageBox.Show(this, "Backup completed and verified.", "Database backup");
                DialogResult = true;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Backup failed", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { RunBackup.IsEnabled = true; }
        }
    }
}
