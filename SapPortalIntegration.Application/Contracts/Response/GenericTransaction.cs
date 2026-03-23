using System.Text.Json.Serialization;

namespace SapPortalIntegration.Application.Contracts.Response;
public class GenericTransaction<T>
{
    public T? Data { get; set; }
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = "";
    [JsonIgnore]
    public decimal? DocTotal { get; set; } = 0;
}

public class GenericTransaction
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = "";
}

public class ODataResponse<T>
{
    [Newtonsoft.Json.JsonProperty("value")]
    public List<T> Value { get; set; }
}