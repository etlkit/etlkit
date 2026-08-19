using System.Collections;
using System.Data.Common;
using System.Linq;

namespace EtlKit.ClickHouse.ConnectionStrings
{
    /// <summary>
    /// Builds and parses ClickHouse connection strings. Unlike most other EtlKit connection string
    /// builders, ClickHouse has no dedicated ADO.NET connection string builder to wrap, so this class
    /// implements <see cref="System.Data.Common.DbConnectionStringBuilder"/> directly.
    /// </summary>
    public class ClickHouseConnectionStringBuilder : DbConnectionStringBuilder
    {
        /// <summary>
        /// ClickHouse server host name. Defaults to an empty string.
        /// </summary>
        public string Host
        {
            get => GetValueOrDefault("Host", string.Empty);
            set => this["Host"] = value;
        }

        /// <summary>
        /// ClickHouse native TCP port. Defaults to <c>9000</c>.
        /// </summary>
        public int Port
        {
            get => GetValueOrDefault("Port", 9000);
            set => this["Port"] = value;
        }

        /// <summary>
        /// Login user name. Defaults to an empty string.
        /// </summary>
        public string User
        {
            get => GetValueOrDefault("User", string.Empty);
            set => this["User"] = value;
        }

        /// <summary>
        /// Login password. Defaults to an empty string.
        /// </summary>
        public string Password
        {
            get => GetValueOrDefault("Password", string.Empty);
            set => this["Password"] = value;
        }

        /// <summary>
        /// Target database name. Defaults to an empty string.
        /// </summary>
        public string Database
        {
            get => GetValueOrDefault("Database", string.Empty);
            set => this["Database"] = value;
        }

        /// <summary>
        /// Connection string keys with only their first character capitalized (e.g. <c>"Host"</c>, not <c>"HOST"</c>).
        /// </summary>
        public override ICollection? Keys
#pragma warning disable S2365 // The property Keys does not have a setter, it is not possible to set the correct keys
            =>
            base.Keys?.Cast<string>().Select(k => $"{k.ToUpper()[0]}{k.Substring(1)}").ToArray();
#pragma warning restore S2365 // Properties should not make collection or array copies

        // Helper method to get a value from the connection string
        private T GetValueOrDefault<T>(string key, T defaultValue)
        {
            if (TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Builds the connection string in a fixed <c>Host;Port;Database;User;Password</c> key order.
        /// </summary>
        public override string ToString()
        {
            // Build the connection string based on the properties
            var connectionString =
                $"Host={Host};Port={Port};Database={Database};User={User};Password={Password}";
            return connectionString;
        }
    }
}
