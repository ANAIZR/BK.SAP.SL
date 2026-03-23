using Sap.Data.Hana;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SapPortalIntegration.Infrastructure.Persistence;
public interface IHanaPersistence
{
    HanaConnection GetConnection();
}

public class HanaPersistence(
        IDictionary<EPersistence, string> connectionDict
    ) : IHanaPersistence
{
    private readonly IDictionary<EPersistence, string> _connectionDict = connectionDict;

    public HanaConnection GetConnection()
    {
        try
        {
            _connectionDict.TryGetValue(EPersistence.HANA, out var connectionString);
            return new HanaConnection(connectionString);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Error Conexion Hanna: {ex.Message}", ex);
        }
    }
}