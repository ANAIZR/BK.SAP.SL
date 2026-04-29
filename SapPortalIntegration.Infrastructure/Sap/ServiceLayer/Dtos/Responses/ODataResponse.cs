using Newtonsoft.Json;

namespace SapPortalIntegration.Infrastructure.Sap.ServiceLayer.Dtos.Responses;

public class ODataResponse<T>
{
    [JsonProperty("value")]
    public List<T> Value { get; set; } = new();
}