using Newtonsoft.Json;


namespace SapPortalIntegration.Application.Contracts.Http;

public class SapOdataResponse
{
    [JsonProperty("odata.metadata")]
    public string OdataMetadata { get; set; }
}
