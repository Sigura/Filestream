using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;

namespace FileStream.DAL
{
    using Common;

    public class Files
    {
        private const int CopyBufferSize = 4 * 1024 * 1024;

        public static void SetBody(string connectionString, Stream source, Guid ID)
        {
            const string setBodyQuery =
                @"SELECT top 1 Body.PathName() AS path, GET_FILESTREAM_TRANSACTION_CONTEXT() as context " +
                @"FROM Files " +
                @"where  ID = @ID";

            using (var connection = new SqlConnection(connectionString).OpenIt())
            using (var tran = connection.BeginTransaction())
            using (var command = connection.CreateCommand(tran, setBodyQuery)
                .AddParam(@"ID", SqlDbType.UniqueIdentifier, 16, ID))
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) throw new InvalidOperationException(string.Format(@"File for {{{0}}} does not exists", ID));

                Copy(source, reader);

                reader.Close();
                tran.Commit();
            }
        }

        private static void Copy(Stream source, IDataReader reader)
        {
            using (var stream = GetSqlFileStream(reader, FileAccess.Write))
            {
                source.CopyTo(stream, CopyBufferSize);
            }
        }

        public static SqlFileStream GetSqlFileStream(IDataRecord reader, FileAccess fileAccess)
        {
            var index = reader.GetOrdinal(@"context");
            if (reader.IsDBNull(index))
                throw new InvalidOperationException(@"GET_FILESTREAM_TRANSACTION_CONTEXT return null");

            return new SqlFileStream(reader.GetString(reader.GetOrdinal(@"path")), (byte[])reader.GetValue(index),
                fileAccess) { Position = 0 };
        }

        public static void GetBody(string connectionString, Guid id, Action<Stream> action)
        {
            const string getBodyQuery =
                @"SELECT top 1 Body.PathName() AS path, GET_FILESTREAM_TRANSACTION_CONTEXT() as context " +
                @"FROM dbo.Files " +
                @"where  ID = @id";

            using (var connection = new SqlConnection(connectionString).OpenIt())
            using (var tran = connection.BeginTransaction())
            using (var command = connection.CreateCommand(tran, getBodyQuery)
                .AddParam(@"id", SqlDbType.UniqueIdentifier, 16, id))
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read())
                    throw new InvalidOperationException(
                        string.Format(@"File for {{{0}}} does not exists", id));

                using (var stream = GetSqlFileStream(reader, FileAccess.Read))
                    action(stream);

                reader.Close();
                tran.Commit();
            }
        }
    }
}