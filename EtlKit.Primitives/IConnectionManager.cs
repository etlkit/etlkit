using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace EtlKit.Primitives
{
    public interface IConnectionManager : IDisposable
    {
        ConnectionManagerType ConnectionManagerType { get; }
        IDbConnectionString ConnectionString { get; set; }
        void Open();
        void Close();
        void CloseIfAllowed();
        ConnectionState? State { get; }
        int MaxLoginAttempts { get; set; }
        IDbCommand CreateCommand(string commandText, IEnumerable<IQueryParameter> parameterList);
        int ExecuteNonQuery(string command, IEnumerable<IQueryParameter> parameterList = null);
        object ExecuteScalar(string command, IEnumerable<IQueryParameter> parameterList = null);
        IDataReader ExecuteReader(
            string command,
            IEnumerable<IQueryParameter> parameterList = null
        );
        IDbTransaction Transaction { get; set; }
        void BulkInsert(ITableData data, string tableName);
        void BeforeBulkInsert(string tableName);
        void AfterBulkInsert(string tableName);
        IConnectionManager Clone();
        IConnectionManager CloneIfAllowed();
        bool LeaveOpen { get; set; }
        bool IsInBulkInsert { get; set; }
        void PrepareBulkInsert(string tableName);
        void CleanUpBulkInsert(string tableName);
        void BeginTransaction(IsolationLevel isolationLevel);
        void BeginTransaction();
        void CommitTransaction();
        void RollbackTransaction();
        void CloseTransaction();
        bool IndexExists(ITask callingTask, string sql);

        string QB { get; }
        string QE { get; }
        bool SupportDatabases { get; }
        bool SupportProcedures { get; }
        bool SupportSchemas { get; }
        bool SupportComputedColumns { get; }
        CultureInfo ConnectionCulture { get; }
    }
}
