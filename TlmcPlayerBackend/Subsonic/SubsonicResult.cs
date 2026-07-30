using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace TlmcPlayerBackend.Subsonic;

/// <summary>Subsonic protocol error codes actually used by the facade.</summary>
public static class SubsonicErrorCodes
{
    public const int Generic = 0;
    public const int MissingParameter = 10;
    public const int NotImplemented = 30;
    public const int NotAuthenticated = 40;
    public const int AuthMechanismNotSupported = 42;
    public const int ConflictingAuthMechanisms = 43;
    public const int InvalidApiKey = 44;
    public const int NotAuthorized = 50;
    public const int NotFound = 70;
}

/// <summary>
/// Writes a <see cref="SubsonicEnvelope"/> in the format the request asked for
/// (`f=xml` default, `json`, or `jsonp` with `callback=`). Errors are protocol
/// level — HTTP 200 with status="failed" — which is why this is an ActionResult
/// and not content negotiation: /rest must not inherit the API's snake_case
/// contract or its ProblemDetails error shape.
/// </summary>
public class SubsonicResult(SubsonicEnvelope envelope) : ActionResult
{
    private static readonly string AssemblyVersion =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    private static readonly XmlSerializer XmlEnvelopeSerializer = new(typeof(SubsonicEnvelope));

    private static readonly JsonSerializer JsonEnvelopeSerializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
        NullValueHandling = NullValueHandling.Ignore,
    });

    private readonly SubsonicEnvelope _envelope = envelope;

    public static SubsonicResult Ok(Action<SubsonicEnvelope>? fill = null)
    {
        var envelope = new SubsonicEnvelope { ServerVersion = AssemblyVersion };
        fill?.Invoke(envelope);
        return new SubsonicResult(envelope);
    }

    public static SubsonicResult Error(int code, string message)
    {
        return new SubsonicResult(new SubsonicEnvelope
        {
            ServerVersion = AssemblyVersion,
            Status = "failed",
            Error = new SubsonicErrorDto { Code = code, Message = message },
        });
    }

    public override async Task ExecuteResultAsync(ActionContext context)
    {
        var request = context.HttpContext.Request;
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status200OK;

        var format = request.Query["f"].ToString().ToLowerInvariant();
        switch (format)
        {
            case "json":
            {
                response.ContentType = "application/json; charset=utf-8";
                await response.WriteAsync(ToJson(), Encoding.UTF8);
                break;
            }
            case "jsonp":
            {
                // A missing callback is unrecoverable in-band: the client is going
                // to eval whatever comes back, so a bare error object is the least
                // broken answer.
                var callback = request.Query["callback"].ToString();
                response.ContentType = "application/javascript; charset=utf-8";
                await response.WriteAsync(
                    string.IsNullOrEmpty(callback) ? ToJson() : $"{callback}({ToJson()});",
                    Encoding.UTF8);
                break;
            }
            default:
            {
                response.ContentType = "text/xml; charset=utf-8";
                await response.WriteAsync(ToXml(), Encoding.UTF8);
                break;
            }
        }
    }

    private string ToJson()
    {
        var body = new JObject(
            new JProperty("subsonic-response", JObject.FromObject(_envelope, JsonEnvelopeSerializer)));
        return body.ToString(Newtonsoft.Json.Formatting.None);
    }

    private string ToXml()
    {
        // Pin the default namespace so the serializer does not emit xsi/xsd noise.
        var namespaces = new XmlSerializerNamespaces();
        namespaces.Add("", "http://subsonic.org/restapi");

        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false };
        var buffer = new Utf8StringWriter();
        using (var writer = XmlWriter.Create(buffer, settings))
        {
            XmlEnvelopeSerializer.Serialize(writer, _envelope, namespaces);
        }

        return buffer.ToString();
    }

    /// <summary>StringWriter reports UTF-16, which would lie in the XML declaration.</summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
