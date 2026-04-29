using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

Console.OutputEncoding = Encoding.UTF8;

var config = new AppConfig
{
    ServiceLayerBaseUrl = "https://binswangerhdb:50000/b1s/v1/",
    CompanyDb = "AISAC_TEST",
    UserName = "manager4",
    Password = "Ramo@123",

    TotalSociosACrear = 700,

    MinFacturasPorSocio = 1,
    MaxFacturasPorSocio = 3,

    ItemCode = "S.SER.VAR.00151",
    TaxCode = "IGV",

    NumAtCardPrefix = "ASB",

    GroupCode = 100,

    RandomSeed = 20260428,

    DelayMsEntreDocumentos = 100,

    // Si quieres solo RUC: "RUC"
    // Si quieres solo DNI: "DNI"
    // Si quieres mezcla: "MIXTO"
    TipoDocumentoFake = "MIXTO"
};

var handler = new HttpClientHandler
{
    CookieContainer = new CookieContainer(),

    // Solo para TEST si el certificado es interno/self-signed.
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};

using var http = new HttpClient(handler)
{
    BaseAddress = new Uri(config.ServiceLayerBaseUrl),
    Timeout = TimeSpan.FromMinutes(5)
};

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = null,
    WriteIndented = false
};

var random = new Random(config.RandomSeed);

var codigosAsbanc = new List<CodigoAsbancGenerado>();
var errores = new List<string>();

try
{
    await LoginAsync();

    for (int i = 1; i <= config.TotalSociosACrear; i++)
    {
        Console.WriteLine("");
        Console.WriteLine($"========== SOCIO {i}/{config.TotalSociosACrear} ==========");

        var socio = GenerarSocio(i);

        Console.WriteLine($"LicTradNum: {socio.LicTradNum}");
        Console.WriteLine($"Tipo Doc:   {socio.TipoDocumento}");
        Console.WriteLine($"Nombre:     {socio.CardName}");

        string? cardCodeExistente = await BuscarCardCodePorLicTradNumAsync(socio.LicTradNum);

        string? cardCodeFinal;

        if (!string.IsNullOrWhiteSpace(cardCodeExistente))
        {
            cardCodeFinal = cardCodeExistente;
            Console.WriteLine($"SOCIO YA EXISTE. CardCode encontrado: {cardCodeFinal}");
        }
        else
        {
            cardCodeFinal = await CrearSocioAsync(socio);

            if (string.IsNullOrWhiteSpace(cardCodeFinal))
            {
                errores.Add($"No se pudo crear socio con LicTradNum {socio.LicTradNum}");
                continue;
            }
        }

        int cantidadFacturas = random.Next(config.MinFacturasPorSocio, config.MaxFacturasPorSocio + 1);
        Console.WriteLine($"Facturas a crear para {cardCodeFinal}: {cantidadFacturas}");

        for (int nroFactura = 1; nroFactura <= cantidadFacturas; nroFactura++)
        {
            var factura = GenerarFactura(cardCodeFinal, i, nroFactura, random);

            var creada = await CrearFacturaAsync(factura);

            if (!creada)
            {
                errores.Add($"No se pudo crear factura {factura.NumAtCard} para CardCode {cardCodeFinal}");
            }

            await Task.Delay(config.DelayMsEntreDocumentos);
        }

        codigosAsbanc.Add(new CodigoAsbancGenerado
        {
            CardCode = cardCodeFinal,
            CardName = socio.CardName,
            LicTradNum = socio.LicTradNum,
            TipoDocumento = socio.TipoDocumento
        });
    }

    await LogoutAsync();

    GenerarTxtCodigosAsbanc(codigosAsbanc);
    GenerarCsvCodigosAsbanc(codigosAsbanc);
    GenerarLogErrores(errores);

    Console.WriteLine("");
    Console.WriteLine("==================================");
    Console.WriteLine("PROCESO FINALIZADO");
    Console.WriteLine("==================================");
    Console.WriteLine($"Socios procesados: {codigosAsbanc.Count}");
    Console.WriteLine($"Errores: {errores.Count}");
    Console.WriteLine("Archivos generados:");
    Console.WriteLine("- codigos_asbanc.txt");
    Console.WriteLine("- codigos_asbanc.csv");
    Console.WriteLine("- errores_asbanc.log");
}
catch (Exception ex)
{
    Console.WriteLine("");
    Console.WriteLine("ERROR GENERAL:");
    Console.WriteLine(ex.Message);
}

async Task LoginAsync()
{
    var payload = new
    {
        CompanyDB = config.CompanyDb,
        UserName = config.UserName,
        Password = config.Password
    };

    var response = await PostJsonAsync("Login", payload);
    var body = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        throw new Exception($"Error Login Service Layer: {response.StatusCode} - {body}");
    }

    Console.WriteLine("Login OK Service Layer");
}

async Task LogoutAsync()
{
    try
    {
        var response = await http.PostAsync("Logout", null);
        Console.WriteLine(response.IsSuccessStatusCode
            ? "Logout OK"
            : $"Logout con observación: {response.StatusCode}");
    }
    catch
    {
        Console.WriteLine("No se pudo cerrar sesión, pero el proceso ya terminó.");
    }
}

SocioGenerado GenerarSocio(int index)
{
    string tipoDoc;

    if (config.TipoDocumentoFake.Equals("DNI", StringComparison.OrdinalIgnoreCase))
    {
        tipoDoc = "DNI";
    }
    else if (config.TipoDocumentoFake.Equals("RUC", StringComparison.OrdinalIgnoreCase))
    {
        tipoDoc = "RUC";
    }
    else
    {
        tipoDoc = index % 2 == 0 ? "RUC" : "DNI";
    }

    string licTradNum = tipoDoc == "DNI"
        ? GenerarDniFake(index)
        : GenerarRucFake(index);

    string nroCarnet = $"{900000 + index}";

    string nombres = $"CLIENTE{index:000000}";
    string apellidoPaterno = "PRUEBA";
    string apellidoMaterno = "ASBANC";

    string cardName = tipoDoc == "DNI"
        ? $"{apellidoPaterno} {apellidoMaterno} {nombres}"
        : $"EMPRESA PRUEBA ASBANC {index:000000} SAC";

    return new SocioGenerado
    {
        CardName = cardName,
        LicTradNum = licTradNum,
        TipoDocumento = tipoDoc,
        NroCarnet = nroCarnet,

        TipoPersonaSap = tipoDoc == "RUC" ? "TPJ" : "TPN",
        TipoDocumentoSap = tipoDoc == "RUC" ? "6" : "1",

        Nombres = tipoDoc == "DNI" ? nombres : "",
        ApellidoPaterno = tipoDoc == "DNI" ? apellidoPaterno : "",
        ApellidoMaterno = tipoDoc == "DNI" ? apellidoMaterno : ""
    };
}
string GenerarDniFake(int index)
{
    // DNI fake de 8 dígitos
    return $"79{index:000000}";
}

string GenerarRucFake(int index)
{
    // RUC fake de 11 dígitos
    return $"2099{index:0000000}";
}
FacturaGenerada GenerarFactura(string cardCode, int indexSocio, int nroFactura, Random rnd)
{
    decimal monto = Math.Round((decimal)(rnd.Next(2000, 50000) / 100.0), 2);

    int mesesAtras = rnd.Next(1, 7);
    int diasExtra = rnd.Next(0, 20);

    DateTime fechaEmision = DateTime.Today
        .AddMonths(-mesesAtras)
        .AddDays(-diasExtra);

    int diasVencimiento = rnd.Next(7, 45);
    DateTime fechaVencimiento = fechaEmision.AddDays(diasVencimiento);

    if (fechaVencimiento >= DateTime.Today)
    {
        fechaVencimiento = DateTime.Today.AddDays(-rnd.Next(1, 30));
    }

    return new FacturaGenerada
    {
        CardCode = cardCode,
        DocDate = fechaEmision,
        DocDueDate = fechaVencimiento,
        TaxDate = fechaEmision,
        Monto = monto,
        NumAtCard = $"{config.NumAtCardPrefix}-{indexSocio:000000}-{nroFactura:00}"
    };
}

async Task<string?> BuscarCardCodePorLicTradNumAsync(string licTradNum)
{
    string filtro = $"$select=CardCode,CardName,FederalTaxID&$filter=FederalTaxID eq '{EscapeODataString(licTradNum)}' and CardType eq 'cCustomer'";

    var response = await http.GetAsync($"BusinessPartners?{filtro}");
    var body = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"No se pudo buscar socio por LicTradNum {licTradNum}: {response.StatusCode}");
        Console.WriteLine(body);
        return null;
    }

    using var doc = JsonDocument.Parse(body);

    if (!doc.RootElement.TryGetProperty("value", out var value))
        return null;

    if (value.GetArrayLength() == 0)
        return null;

    var first = value[0];

    if (first.TryGetProperty("CardCode", out var cardCodeElement))
    {
        return cardCodeElement.GetString();
    }

    return null;
}

string GenerarCardCode12(string licTradNum)
{
    // Solo números, por seguridad
    string limpio = new string(licTradNum.Where(char.IsDigit).ToArray());

    // CardCode de 12 caracteres:
    // C + documento rellenado a 11
    return "C" + limpio.PadLeft(11, '0');
}
async Task<string?> CrearSocioAsync(SocioGenerado socio)
{
    string cardCodeInterno = GenerarCardCode12(socio.LicTradNum);

    var payload = new Dictionary<string, object?>
    {
        ["CardCode"] = cardCodeInterno,
        ["CardName"] = socio.CardName,

        ["CardType"] = "cCustomer",
        ["GroupCode"] = config.GroupCode,

        ["FederalTaxID"] = socio.LicTradNum,

        ["Currency"] = "##",
        ["Valid"] = "tYES",

        ["U_BPP_BPTP"] = socio.TipoPersonaSap,
        ["U_BPP_BPTD"] = socio.TipoDocumentoSap,

        ["U_ST_NroCarnet"] = socio.NroCarnet,
        ["U_Estado"] = "HA"
    };

    if (socio.TipoDocumento == "DNI")
    {
        payload["U_BPP_BPNO"] = socio.Nombres;
        payload["U_BPP_BPAP"] = socio.ApellidoPaterno;
        payload["U_BPP_BPAM"] = socio.ApellidoMaterno;
    }

    var response = await PostJsonAsync("BusinessPartners", payload);
    var body = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"BP ERROR LicTradNum {socio.LicTradNum}: {response.StatusCode}");
        Console.WriteLine(body);
        return null;
    }

    Console.WriteLine($"BP OK. CardCode usado: {cardCodeInterno}");

    return cardCodeInterno;
}
async Task<bool> CrearFacturaAsync(FacturaGenerada factura)
{
    var payload = new
    {
        CardCode = factura.CardCode,
        DocDate = factura.DocDate.ToString("yyyy-MM-dd"),
        DocDueDate = factura.DocDueDate.ToString("yyyy-MM-dd"),
        TaxDate = factura.TaxDate.ToString("yyyy-MM-dd"),

        DocCurrency = "SOL",
        NumAtCard = factura.NumAtCard,
        Comments = $"Factura fake para prueba estrés ASBANC - {factura.NumAtCard}",

        U_STR_Procesando = "C",

        DocumentLines = new[]
        {
            new
            {
                ItemCode = config.ItemCode,
                Quantity = 1,
                UnitPrice = factura.Monto,
                TaxCode = config.TaxCode
            }
        }
    };

    var response = await PostJsonAsync("Invoices", payload);
    var body = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"INV ERROR {factura.CardCode} / {factura.NumAtCard}: {response.StatusCode}");
        Console.WriteLine(body);
        return false;
    }

    Console.WriteLine(
        $"INV OK {factura.CardCode} / {factura.NumAtCard} / Monto {factura.Monto.ToString("0.00", CultureInfo.InvariantCulture)} / Vence {factura.DocDueDate:yyyy-MM-dd}");

    return true;
}

async Task<HttpResponseMessage> PostJsonAsync(string endpoint, object payload)
{
    string json = JsonSerializer.Serialize(payload, jsonOptions);

    var content = new StringContent(json, Encoding.UTF8, "application/json");

    return await http.PostAsync(endpoint, content);
}

string EscapeODataString(string value)
{
    return value.Replace("'", "''");
}

void GenerarTxtCodigosAsbanc(List<CodigoAsbancGenerado> codigos)
{
    var sb = new StringBuilder();

    foreach (var item in codigos)
    {
        sb.AppendLine(item.LicTradNum);
    }

    File.WriteAllText("codigos_asbanc.txt", sb.ToString(), Encoding.UTF8);
}

void GenerarCsvCodigosAsbanc(List<CodigoAsbancGenerado> codigos)
{
    var sb = new StringBuilder();

    sb.AppendLine("LicTradNum;TipoDocumento;CardCode;CardName");

    foreach (var item in codigos)
    {
        sb.AppendLine($"{item.LicTradNum};{item.TipoDocumento};{item.CardCode};{item.CardName}");
    }

    File.WriteAllText("codigos_asbanc.csv", sb.ToString(), Encoding.UTF8);
}

void GenerarLogErrores(List<string> errores)
{
    if (errores.Count == 0)
    {
        File.WriteAllText("errores_asbanc.log", "Sin errores.", Encoding.UTF8);
        return;
    }

    File.WriteAllLines("errores_asbanc.log", errores, Encoding.UTF8);
}

public class AppConfig
{
    public string ServiceLayerBaseUrl { get; set; } = "";
    public string CompanyDb { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";

    public int TotalSociosACrear { get; set; }
    public int MinFacturasPorSocio { get; set; }
    public int MaxFacturasPorSocio { get; set; }

    public string ItemCode { get; set; } = "";
    public string TaxCode { get; set; } = "";

    public string NumAtCardPrefix { get; set; } = "";

    public int GroupCode { get; set; }
    public int RandomSeed { get; set; }
    public int DelayMsEntreDocumentos { get; set; }

    public string TipoDocumentoFake { get; set; } = "MIXTO";
}

public class SocioGenerado
{
    public string CardName { get; set; } = "";
    public string LicTradNum { get; set; } = "";
    public string TipoDocumento { get; set; } = "";
    public string TipoPersonaSap { get; set; } = "";
    public string TipoDocumentoSap { get; set; } = "";
    public string NroCarnet { get; set; } = "";

    public string Nombres { get; set; } = "";
    public string ApellidoPaterno { get; set; } = "";
    public string ApellidoMaterno { get; set; } = "";
}

public class FacturaGenerada
{
    public string CardCode { get; set; } = "";
    public DateTime DocDate { get; set; }
    public DateTime DocDueDate { get; set; }
    public DateTime TaxDate { get; set; }
    public decimal Monto { get; set; }
    public string NumAtCard { get; set; } = "";
}

public class CodigoAsbancGenerado
{
    public string CardCode { get; set; } = "";
    public string CardName { get; set; } = "";
    public string LicTradNum { get; set; } = "";
    public string TipoDocumento { get; set; } = "";
}