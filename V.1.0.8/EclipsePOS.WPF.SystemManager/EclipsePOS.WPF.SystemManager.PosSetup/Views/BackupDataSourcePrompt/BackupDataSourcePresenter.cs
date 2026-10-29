using System;
using System.Data.SqlClient;
using EclipsePOS.WPF.SystemManager.PosSetup.Util;

namespace EclipsePOS.WPF.SystemManager.PosSetup.Views.BackupDataSourcePrompt
{
    public class BackupDataSourcePresenter
    {
        public void Backup(string serverPath)
        {
            if (string.IsNullOrWhiteSpace(serverPath) || !serverPath.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Enter an absolute path ending in .bak on the SQL Server machine.");
            if (!System.IO.Path.IsPathRooted(serverPath)) throw new ArgumentException("An absolute backup path is required.");
            var builder = new SqlConnectionStringBuilder(PosSettings.Default.DataSource);
            string database = builder.InitialCatalog;
            if (string.IsNullOrWhiteSpace(database)) throw new InvalidOperationException("Configure a database first.");
            builder.InitialCatalog = "master";
            using (var connection = new SqlConnection(builder.ConnectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "BACKUP DATABASE [" + database.Replace("]", "]]") + "] TO DISK = @path WITH COPY_ONLY, CHECKSUM, NOINIT, NAME = @name; DECLARE @position int = (SELECT TOP (1) position FROM msdb.dbo.backupset WHERE name = @name AND database_name = @database ORDER BY backup_finish_date DESC); IF @position IS NULL THROW 51001, 'Could not locate the new backup set.', 1; RESTORE VERIFYONLY FROM DISK = @path WITH FILE = @position, CHECKSUM;";
                command.Parameters.AddWithValue("@path", serverPath); command.Parameters.AddWithValue("@name", "EclipsePOS-" + Guid.NewGuid().ToString("N")); command.Parameters.AddWithValue("@database", database);
                command.CommandTimeout = 600;
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
}

