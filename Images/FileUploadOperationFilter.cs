using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.OperationId != "UploadImage")
        {
            return;
        }
        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
                {
                    {
                        "multipart/form-data", new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = "object",
                                Properties = new Dictionary<string, OpenApiSchema>
                                {
                                    {
                                        "file", new OpenApiSchema
                                        {
                                            Type = "string",
                                            Format = "binary"
                                        }
                                    },
                                    {
                                        "licenseImage", new OpenApiSchema
                                        {
                                            Type = "boolean"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
        };

    }
}