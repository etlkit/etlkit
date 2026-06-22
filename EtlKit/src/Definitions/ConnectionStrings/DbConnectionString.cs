using System.Data.Common;
using EtlKit.Primitives;

namespace EtlKit
{
    /// <summary>
    /// <see cref="IDbConnectionString"/> base
    /// </summary>
    /// <typeparam name="T">Derived type</typeparam>
    /// <typeparam name="TBuilder"><see cref="Builder"/> type</typeparam>
    public abstract class DbConnectionString<T, TBuilder> : IDbConnectionString
        where T : DbConnectionString<T, TBuilder>, new()
        where TBuilder : DbConnectionStringBuilder, new()
    {
        protected DbConnectionString() { }

        protected DbConnectionString(string value)
        {
            Value = value;
        }

        public TBuilder Builder { get; private set; } = new();

        public string Value
        {
            get => GetConnectionString();
            set => Builder.ConnectionString = value;
        }

        protected virtual string GetConnectionString() => Builder.ConnectionString;

        protected virtual T Clone()
        {
            var clone = (T)MemberwiseClone();
            clone.Builder = new TBuilder();
            clone.Value = Value;
            return clone;
        }

        IDbConnectionString IDbConnectionString.Clone() => Clone();

        public override string ToString() => Builder.ConnectionString;

        public abstract string DbName { get; set; }
        protected abstract string DbNameKeyword { get; }

        public abstract string MasterDbName { get; }

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

        public T CloneWithoutDbName() => CloneWithNewDbName(string.Empty);

        IDbConnectionString IDbConnectionString.CloneWithNewDbName(string value) =>
            CloneWithNewDbName(value);

        public T CloneWithMasterDbName() => CloneWithNewDbName(MasterDbName);

        IDbConnectionString IDbConnectionString.CloneWithMasterDbName() => CloneWithMasterDbName();
    }
}
