using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace API.Helpers;

public static class SqlHelper
{
    public static List<T> RawSqlQuery<T>(DbContext context, string query, Func<DbDataReader, T> map)
    {
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
