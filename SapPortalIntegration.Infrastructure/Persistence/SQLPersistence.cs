using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SapPortalIntegration.Infrastructure.Persistence;

public interface ISQLPersistence
{
    void ExecuteCommand(Action<SqlConnection> task);
    T ExecuteCommand<T>(Func<SqlConnection, T> task);
    T ExecuteMultipleCommand<T>(Func<SqlConnection, T> task);

    T ExecuteCommandInt<T>(Func<SqlConnection, T> task);
    void CloseConnection();
}
public class SQLPersistence(
        IDictionary<EPersistence, string> connectionDict
    ) : ISQLPersistence
{
    private readonly IDictionary<EPersistence, string> _connectionDict = connectionDict;
    private SqlConnection? conn;
    public void CloseConnection()
    {
        if (conn != null && conn.State == ConnectionState.Open)
            conn.Close();
    }

    public void ExecuteCommand(Action<SqlConnection> task)
    {
        conn = CreateConnection();
        conn.Open();
        task(conn);
    }

    public T ExecuteCommand<T>(Func<SqlConnection, T> task)
    {
        conn = CreateConnection();
        conn.Open();
        return task(conn);
    }

    public T ExecuteCommandInt<T>(Func<SqlConnection, T> task)
    {
        conn = CreateConnection();
        conn.Open();
        return task(conn);
    }

    public T ExecuteMultipleCommand<T>(Func<SqlConnection, T> task)
    {
        conn = CreateConnection();
        conn.Open();
        return task(conn);
    }
    private SqlConnection CreateConnection()
    {
        try
        {
            _connectionDict.TryGetValue(EPersistence.SQL, out var connectionString);
            return new SqlConnection(connectionString);
        }
        catch (SqlException ex)
        {
            throw new Exception("ERROR SQL CONNECTION:", ex);
        }
        catch (Exception ex)
        {
            throw new Exception("ERROR:", ex);
        }
    }
}