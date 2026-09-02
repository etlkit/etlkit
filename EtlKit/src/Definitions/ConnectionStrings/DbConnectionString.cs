using System.Data.Common;
using EtlKit.Primitives;

namespace EtlKit
{
    /// <summary>
    /// Base <see cref="IDbConnectionString"/> implementation shared by every connection string type:
    /// wraps a <typeparamref name="TBuilder"/> and provides database-name-aware cloning (with a
    /// different database name, or none at all). Derived classes supply <see cref="DbName"/>, <see
    /// cref="DbNameKeyword"/>, and <see cref="MasterDbName"/> for their specific connection string format.
    /// </summary>
    /// <typeparam name="T">The concrete connection string type itself, so cloning methods return that type.</typeparam>
    /// <typeparam name="TBuilder">The <see cref="DbConnectionStringBuilder"/> type backing <see cref="Builder"/>.</typeparam>
    public abstract class DbConnectionString<T, TBuilder> : IDbConnectionString
        where T : DbConnectionString<T, TBuilder>, new()
        where TBuilder : DbConnectionStringBuilder, new()
    {
        /// <summary>
        /// Creates an empty connection string with a fresh <typeparamref name="TBuilder"/>.
        /// </summary>
        protected DbConnectionString() { }

        /// <summary>
        /// Creates a connection string from an existing connection string value.
        /// </summary>
        /// <param name="value">A connection string in the format <typeparamref name="TBuilder"/> parses.</param>
        protected DbConnectionString(string value)
        {
            Value = value;
        }

        /// <summary>
        /// The strongly-typed builder that stores and parses this connection string's key/value pairs.
        /// Replaced with a new instance by <see cref="Clone"/>.
        /// </summary>
        public TBuilder Builder { get; private set; } = new();

        /// <summary>
        /// The connection string. Getting it returns <see cref="GetConnectionString"/>; setting it
        /// parses the value into <see cref="Builder"/>.
        /// </summary>
        public string Value
        {
            get => GetConnectionString();
            set => Builder.ConnectionString = value;
        }

        /// <summary>
        /// Builds the string returned by <see cref="Value"/>. Returns <see
        /// cref="DbConnectionStringBuilder.ConnectionString"/> as-is by default; derived classes
        /// override to normalize the output (e.g. rewriting specific key/value pairs).
        /// </summary>
        protected virtual string GetConnectionString() => Builder.ConnectionString;

        /// <summary>
        /// Creates a copy of this connection string with its own <typeparamref name="TBuilder"/>
        /// instance (so mutating the clone's <see cref="Builder"/> does not affect the original),
        /// initialized from the same <see cref="Value"/>.
        /// </summary>
        protected virtual T Clone()
        {
            var clone = (T)MemberwiseClone();
            clone.Builder = new TBuilder();
            clone.Value = Value;
            return clone;
        }

        /// <inheritdoc cref="Clone" />
        IDbConnectionString IDbConnectionString.Clone() => Clone();

        /// <summary>
        /// Returns <see cref="DbConnectionStringBuilder.ConnectionString"/> directly.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="Value"/>, this does not go through <see cref="GetConnectionString"/>, so
        /// for a derived class that overrides <see cref="GetConnectionString"/> to normalize its
        /// output, <see cref="ToString"/> and <see cref="Value"/> can return different strings for the
        /// same instance (see <c>SqlConnectionString</c>, which rewrites <c>Integrated Security=true</c>
        /// to <c>Integrated Security=SSPI</c> only in <see cref="Value"/>).
        /// </remarks>
        public override string ToString() => Builder.ConnectionString;

        /// <summary>
        /// The target database name, backed by the corresponding property on <see cref="Builder"/>.
        /// </summary>
        public abstract string DbName { get; set; }

        /// <summary>
        /// The connection string key that stores <see cref="DbName"/>, used by <see
        /// cref="CloneWithNewDbName"/> to remove it when clearing the database name.
        /// </summary>
        protected abstract string DbNameKeyword { get; }

        /// <summary>
        /// The database engine's built-in default database name, used by <see cref="CloneWithMasterDbName"/>.
        /// </summary>
        public abstract string MasterDbName { get; }

        /// <summary>
        /// Creates a clone with <see cref="DbName"/> set to <paramref name="value"/>, or with the
        /// database name key removed entirely if <paramref name="value"/> is empty or whitespace.
        /// </summary>
        /// <param name="value">The new database name, or empty/whitespace to remove it.</param>
        public T CloneWithNewDbName(string value)
        {
            var clone = Clone();
            if (string.IsNullOrWhiteSpace(value))
            {
                clone.Builder.Remove(DbNameKeyword);
            }
            else
                clone.DbName = value;

            return clone;
        }

        /// <summary>
        /// Creates a clone with the database name key removed entirely. Equivalent to <see
        /// cref="CloneWithNewDbName"/> with an empty value.
        /// </summary>
        public T CloneWithoutDbName() => CloneWithNewDbName(string.Empty);

        /// <inheritdoc cref="CloneWithNewDbName" />
        IDbConnectionString IDbConnectionString.CloneWithNewDbName(string value) =>
            CloneWithNewDbName(value);

        /// <summary>
        /// Creates a clone with <see cref="DbName"/> set to <see cref="MasterDbName"/>, pointing at the
        /// engine's default database instead of a specific one.
        /// </summary>
        public T CloneWithMasterDbName() => CloneWithNewDbName(MasterDbName);

        /// <inheritdoc cref="CloneWithMasterDbName" />
        IDbConnectionString IDbConnectionString.CloneWithMasterDbName() => CloneWithMasterDbName();
    }
}
