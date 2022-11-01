using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;

namespace SOFDCoreSignaturcentral.Service.Signaturcentral
{
    public class Dao
    {
        private static readonly string connectionString = Settings.GetStringValue("Signaturcentral.ConnectionString");
        private static readonly string query = Settings.GetStringValue("Signaturcentral.Query");

        public List<MOCES> ReadAll()
        {
            List<MOCES> result = new List<MOCES>();

            using (DbConnection connection = GetConnection())
            {
                connection.Open();

                using (DbCommand command = GetCommand(query, connection))
                {
                    using (DbDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var moces = new MOCES();
                            moces.email = GetStringValue(reader, "email");
                            moces.subject = GetStringValue(reader, "subjectDN");
                            moces.userId = GetStringValue(reader, "userIdentifier");
                            moces.tts = GetDateValue(reader, "tts");

                            result.Add(moces);
                        }
                    }
                }
            }

            return result;
        }

        private static string GetStringValue(DbDataReader reader, string key)
        {
            if (reader[key] is DBNull)
            {
                return null;
            }

            return (string)reader[key];
        }

        private static DateTime GetDateValue(DbDataReader reader, string key)
        {
            if (reader[key] is DBNull)
            {
                return DateTime.Now;
            }

            object val = reader[key];
            if (val is string)
            {
                string valStr = (string)val;
                if (string.IsNullOrEmpty(valStr))
                {
                    return DateTime.Now;
                }

                if (valStr.Length >= 10)
                {
                    return DateTime.Parse(valStr.Substring(0, 10));
                }
            }

            return DateTime.Now;
        }

        private static DbConnection GetConnection()
        {
            return new SqlConnection(connectionString);
        }

        private static DbCommand GetCommand(string statement, DbConnection connection)
        {
            return new SqlCommand(statement, (SqlConnection)connection);
        }
    }
}
