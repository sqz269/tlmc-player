using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TlmcPlayerBackend.Subsonic;

/// <summary>
/// A second OpenAPI document for the /rest facade, so it can be exercised from
/// Swagger UI without contaminating the v1 document that native clients are
/// generated from. The document is a browsing aid, not a contract — the
/// contract is the OpenSubsonic spec, and nothing should generate a client
/// from this doc (responses are deliberately left schemaless: every endpoint
/// answers HTTP 200 with an enveloped body in the format `f` selects).
/// </summary>
public static class SubsonicSwagger
{
    public const string DocName = "subsonic";

    public static void AddSubsonicDoc(this SwaggerGenOptions options)
    {
        // Declaring any document replaces the implicit default, so v1 must be
        // re-declared with its historical identity.
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "TlmcPlayerBackend",
            Version = "1.0",
        });
        options.SwaggerDoc(DocName, new OpenApiInfo
        {
            Title = "Subsonic compatibility facade",
            Version = "1.16.1",
            Description =
                "OpenSubsonic-compatible surface (Docs/SUBSONIC.md). Browsing aid only — "
                + "the contract is https://opensubsonic.netlify.app/docs/, responses are "
                + "HTTP 200 with an in-band status envelope, and clients must not be "
                + "generated from this document. Auth is the `apiKey` query parameter "
                + "(see /api/user/api-keys on the v1 document).",
        });

        options.DocInclusionPredicate((doc, api) =>
            doc == DocName ? api.GroupName == DocName : api.GroupName == null);

        options.OperationFilter<SubsonicCommonParametersFilter>();
        options.DocumentFilter<SubsonicDocumentFilter>();
    }
}

/// <summary>
/// The protocol's boilerplate parameters (`apiKey`, `f`) are read from the raw
/// request by the auth filter and SubsonicResult, not bound by MVC, so the API
/// explorer cannot see them; this adds them to every facade operation.
/// </summary>
public class SubsonicCommonParametersFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.GroupName != SubsonicSwagger.DocName)
        {
            return;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "apiKey",
            In = ParameterLocation.Query,
            Required = false,
            Schema = new OpenApiSchema { Type = "string" },
            Description = "API key minted via POST /api/user/api-keys (OpenSubsonic apiKeyAuthentication).",
        });
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "f",
            In = ParameterLocation.Query,
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = "string",
                Enum = [new OpenApiString("xml"), new OpenApiString("json"), new OpenApiString("jsonp")],
            },
            Description = "Response format; XML when omitted.",
        });
    }
}

/// <summary>
/// Prunes the facade document down to one operation per method: the `.view`
/// alias routes and the formPost POST bindings exist for client compatibility,
/// not for browsing. Also drops the document-wide Bearer requirement — the
/// facade authenticates with the `apiKey` query parameter, never a JWT.
/// </summary>
public class SubsonicDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        // Only the facade document contains /rest paths; the v1 document
        // passes through untouched.
        if (!document.Paths.Keys.Any(p => p.StartsWith("/rest")))
        {
            return;
        }

        foreach (var alias in document.Paths.Keys.Where(p => p.EndsWith(".view")).ToList())
        {
            document.Paths.Remove(alias);
        }

        foreach (var (_, item) in document.Paths)
        {
            item.Operations.Remove(OperationType.Post);
        }

        document.SecurityRequirements.Clear();
        document.Components?.SecuritySchemes.Remove("Bearer");
    }
}
