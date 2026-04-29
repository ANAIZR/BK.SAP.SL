using Newtonsoft.Json;


namespace SapPortalIntegration.Infrastructure.Sap.ServiceLayer.Dtos.Responses;
public class UdoLoginResponse
{
    public string SessionId { get; set; }
    public string Version { get; set; }
    public int SessionTimeout { get; set; }
}
public class Response<T>
{
    public string CodRespuesta { get; set; }
    public string DescRespuesta { get; set; }
    public List<T> Data { get; set; }

}
public class ErrorSL
{
    public Error error { get; set; }
}

public class Error
{
    public int code { get; set; }
    public Message message { get; set; }
}

public class Message
{
    public string lang { get; set; }
    public string value { get; set; }
}

public class ErrorResponse
{
    [JsonProperty("error")]
    public ErrorDetail? Error { get; set; }
}

public class ErrorDetail
{
    [JsonProperty("code")]
    public int Code { get; set; }

    [JsonProperty("message")]
    public ErrorMessage? Message { get; set; }
}

public class ErrorMessage
{
    [JsonProperty("lang")]
    public string? Language { get; set; }

    [JsonProperty("value")]
    public string? Value { get; set; }
}