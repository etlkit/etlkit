using System.Collections;
using System.Data.Common;
using System.Linq;

namespace EtlKit.ClickHouse.ConnectionStrings
{
    public class ClickHouseConnectionStringBuilder : DbConnectionStringBuilder
    {
        public string Host
        {
            get => GetValueOrDefault("Host", string.Empty);
            set => this["Host"] = value;
        }

        public int Port
        {
            get => GetValueOrDefault("Port", 9000);
            set => this["Port"] = value;
        }

        public string User
        {
            get => GetValueOrDefault("User", string.Empty);
            set => this["User"] = value;
        }

        public string Password
        {
            get => GetValueOrDefault("Password", string.Empty);
            set => this["Password"] = value;
        }

        public string Database
        {
            get => GetValueOrDefault("Database", string.Empty);
            set => this["Database"] = value;
        }

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

        public override string ToString()
        {
            // Build the connection string based on the properties
            var connectionString =
                $"Host={Host};Port={Port};Database={Database};User={User};Password={Password}";
            return connectionString;
        }
    }
}
