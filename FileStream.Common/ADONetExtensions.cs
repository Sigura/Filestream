using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;

namespace FileStream.Common
{
    public static partial class Extensions
    {
        public static T OpenIt<T>(this T conn)
            where T : IDbConnection
        {
            conn.Open();
            return conn;
        }

        static string AddA(this string str)
        {
            return str.StartsWith(@"@") ? str : string.Format(@"@{0}", str);
        }

        public static T AddParam<T>(this T command, string name, SqlDbType type, int size, object value, ParameterDirection? direction)
            where T : IDbCommand
        {
            var param = new SqlParameter(name.AddA(), type)
            {
                Value = value ?? DBNull.Value,
                Direction = direction ?? ParameterDirection.Input
            };

            if (size != 0)
                param.Size = size;

            command.Parameters.Add(param);

            return command;
        }
        public static T AddParam<T>(this T command, string name, SqlDbType type, int size, object value)
            where T : IDbCommand
        {
            return command.AddParam(name, type, size, value, null);
        }

        public static IDbCommand CreateCommand(this IDbConnection connection, string sqlCommand)
        {
            var command = connection.CreateCommand();

            command.CommandText = sqlCommand;

            return command;
        }

        public static IDbCommand CreateCommand(this IDbConnection connection, IDbTransaction transaction, string sqlCommand)
        {
            var command = connection.CreateCommand(sqlCommand);

            command.Transaction = transaction;

            return command;
        }
    }
}
