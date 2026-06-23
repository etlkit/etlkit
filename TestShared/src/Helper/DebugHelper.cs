using System.Data;
using System.Linq;
using EtlKit;
using EtlKit.ControlFlow;
using EtlKit.Primitives;

namespace EtlKit.TestShared.Helper
{
    /// <summary>
    /// Debug helper for debug purposes
    /// </summary>
    public static class DebugHelper
    {
        /// <summary>
        /// Retrieves a data from a specified table into DataTable for debug / assert purposes
        /// </summary>
        /// <param name="tableDef">table definition</param>
        /// <param name="connection">connection manager</param>
        /// <returns>data table</returns>
        public static DataTable GetTableData(
            TableDefinition tableDef,
            IConnectionManager connection
        )
        {
            var dt = new DataTable();
            foreach (var col in tableDef.Columns)
            {
                var dataCol = dt.Columns.Add(col.Name, col.NETDataType);
                dataCol.DefaultValue = col.DefaultValue;
                dataCol.AllowDBNull = col.AllowNulls;
                dataCol.AutoIncrement = col.IsIdentity;
                dataCol.AutoIncrementSeed = col.IdentitySeed ?? 0;
                dataCol.AutoIncrementStep = col.IdentityIncrement ?? 1;
                dataCol.Unique = col.IsPrimaryKey;
            }
            DataRow row = null;
            new SqlTask(
                "Degug",
                $"select * from {connection.QB}{tableDef.Name}{connection.QE}",
                () => row = dt.NewRow(),
                () => dt.Rows.Add(row),
                tableDef
                    .Columns.Select(c => new Action<object>(o =>
                        row[c.Name] = MapToType(o, c.NETDataType)
                    ))
                    .ToArray()
            )
            {
                ConnectionManager = connection,
            }.ExecuteReader();
            return dt;
        }

        private static object MapToType(object o, Type type)
        {
            if (type == typeof(int))
            {
                return Convert.ToInt32(o);
            }
            if (type == typeof(string))
            {
                return Convert.ToString(o);
            }
            if (type == typeof(DateTime))
            {
                return Convert.ToDateTime(o);
            }
            if (type == typeof(bool))
            {
                return Convert.ToBoolean(o);
            }
            return o;
        }
    }
}
