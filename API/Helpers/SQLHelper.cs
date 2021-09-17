using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace API.Helpers
{
    public static class SQLHelper
    {
        public static List<T> RawSqlQuery<T>(DbContext context, string query, Func<DbDataReader, T> map)
        {
            //using var context = new DbContext();
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = query;
            command.CommandType = CommandType.Text;

            context.Database.OpenConnection();

            using var result = command.ExecuteReader();
            var entities = new List<T>();

            while (result.Read())
            {
                entities.Add(map(result));
            }

            return entities;
        }
    }
}
