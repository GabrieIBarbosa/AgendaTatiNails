using Microsoft.Data.SqlClient;
using System.Data;

namespace AgendaTatiNails.Extensions
{
    // Esta é uma classe de extensão "helper"
    public static class SqlDataReaderExtensions
    {
        // Este método nos permite checar se uma coluna existe no resultado do DataReader
        public static bool HasColumn(this SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}