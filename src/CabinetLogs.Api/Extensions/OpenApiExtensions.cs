using CabinetLogs.Api.Models;
using CabinetLogs.Application.DTOs.Logs;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CabinetLogs.Api.Extensions;

public static class OpenApiExtensions
{
    public static IServiceCollection AddCabinetLogsOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Cabinet Logs API",
                    Version = "v1",
                    Description =
                        "REST API for storing cabinet and equipment maintenance/issue logs, automatic cabinet categorization, " +
                        "client device registration, private Supabase Storage photo uploads with signed URLs, and Excel (.xlsx) reporting.\n\n" +
                        "**Version 1 Access Model:** Shared access without user login. Each log captures the submitting PC/mobile device name.\n\n" +
                        "**Date/Time Convention:** Accepts ISO 8601 timestamps with timezone offsets; stores and returns UTC (`timestamptz`). " +
                        "Date range filters use inclusive `from` (`occurredAt >= from`) and exclusive `to` (`occurredAt < to`)."
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    Name = "X-Api-Key",
                    In = ParameterLocation.Header,
                    Description = "Optional shared API key header (only enforced when ApiSecurity:RequireApiKey is set to true)."
                };

                return Task.CompletedTask;
            });

            // Document BOTH application/json and multipart/form-data request bodies on POST /api/v1/logs and PUT /api/v1/logs/{id}
            // so Scalar provides a selector to test either JSON or file-upload requests directly.
            options.AddOperationTransformer(async (operation, context, cancellationToken) =>
            {
                var actionName = context.Description.ActionDescriptor.RouteValues.TryGetValue("action", out var action)
                    ? action
                    : null;
                var controllerName = context.Description.ActionDescriptor.RouteValues.TryGetValue("controller", out var ctrl)
                    ? ctrl
                    : null;

                if (!string.Equals(controllerName, "Logs", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (string.Equals(actionName, "Create", StringComparison.OrdinalIgnoreCase))
                {
                    var jsonSchema = await context.GetOrCreateSchemaAsync(typeof(CreateLogRequest), null, cancellationToken);
                    var formSchema = await context.GetOrCreateSchemaAsync(typeof(CreateLogFormRequest), null, cancellationToken);

                    operation.RequestBody = new OpenApiRequestBody
                    {
                        Required = true,
                        Description = "Submit as `application/json` (without photo) or `multipart/form-data` (with optional `photo` file).",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType { Schema = jsonSchema },
                            ["multipart/form-data"] = new OpenApiMediaType { Schema = formSchema }
                        }
                    };
                }
                else if (string.Equals(actionName, "Update", StringComparison.OrdinalIgnoreCase))
                {
                    var jsonSchema = await context.GetOrCreateSchemaAsync(typeof(UpdateLogRequest), null, cancellationToken);
                    var formSchema = await context.GetOrCreateSchemaAsync(typeof(UpdateLogFormRequest), null, cancellationToken);

                    operation.RequestBody = new OpenApiRequestBody
                    {
                        Required = true,
                        Description = "Submit as `application/json` (to edit fields or set `removePhoto: true`) or `multipart/form-data` (to replace the photo).",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType { Schema = jsonSchema },
                            ["multipart/form-data"] = new OpenApiMediaType { Schema = formSchema }
                        }
                    };
                }
            });
        });

        return services;
    }
}
