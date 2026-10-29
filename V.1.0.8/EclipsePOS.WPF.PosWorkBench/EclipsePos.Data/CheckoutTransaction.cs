using System;
using System.Data;
using System.Data.SqlClient;

namespace EclipsePos.Data
{
    // A single local SQL transaction for completion, totals and stock writes.
    // No distributed transaction coordinator or additional service is needed.
    public sealed class CheckoutTransaction : IDisposable
    {
        [ThreadStatic] internal static CheckoutTransaction Current;
        internal readonly SqlConnection Connection;
        internal readonly SqlTransaction Transaction;
        private bool completed;
        private bool failed;
        public CheckoutTransaction()
        {
            if (Current != null) throw new InvalidOperationException("Nested checkouts are not supported.");
            Connection = new SqlConnection(Util.PosSettings.Default.DataSource);
            try
            {
                Connection.Open();
                Transaction = Connection.BeginTransaction(IsolationLevel.ReadCommitted);
                Current = this;
            }
            catch { Connection.Dispose(); throw; }
        }
        internal void MarkFailed() { failed = true; }
        public void Commit()
        {
            if (failed) throw new DataException("A checkout write failed. The transaction cannot be committed.");
            Transaction.Commit(); completed = true;
        }
        public void Dispose()
        {
            Current = null;
            try { if (!completed) Transaction.Rollback(); }
            finally { Transaction.Dispose(); Connection.Dispose(); }
        }
    }
}
