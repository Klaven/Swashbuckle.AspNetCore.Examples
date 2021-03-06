using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Swashbuckle.AspNetCore.Examples
{
    public class ExamplesOperationFilter : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {
            SetRequestModelExamples(operation, context.SchemaRegistry, context.ApiDescription);
            SetResponseModelExamples(operation, context.ApiDescription);
        }

        public void Apply(Operation operation, SchemaRegistry schemaRegistry, ApiDescription apiDescription)
        {
            SetRequestModelExamples(operation, schemaRegistry, apiDescription);
            SetResponseModelExamples(operation, apiDescription);
        }

        private static void SetRequestModelExamples(Operation operation, ISchemaRegistry schemaRegistry, ApiDescription apiDescription)
        {
            var actionAttributes = apiDescription.ActionAttributes();
            var swaggerRequestAttributes = actionAttributes.Where(r => r.GetType() == typeof(SwaggerRequestExampleAttribute));

            foreach (var attribute in swaggerRequestAttributes)
            {
                var attr = (SwaggerRequestExampleAttribute)attribute;
                var schema = schemaRegistry.GetOrRegister(attr.RequestType);

                var request = operation.Parameters.FirstOrDefault(p => p.In == "body"/* && p.schema.@ref == schema.@ref */);

                if (request != null)
                {
                    var provider = (IExamplesProvider)Activator.CreateInstance(attr.ExamplesProviderType);

                    var parts = schema.Ref.Split('/');
                    var name = parts.Last();

                    var definitionToUpdate = schemaRegistry.Definitions[name];

                    if (definitionToUpdate != null)
                    {
                        var formatter = (IContractResolver)Activator.CreateInstance(attr.JsonResolver);
                        definitionToUpdate.Example = ((dynamic)FormatAsJson(provider,formatter))["application/json"];
                    }
                }
            }
        }

        private static void SetResponseModelExamples(Operation operation, ApiDescription apiDescription)
        {
            var actionAttributes = apiDescription.ActionAttributes();
            var swaggerResponseExampleAttributes = actionAttributes.Where(r => r.GetType() == typeof(SwaggerResponseExampleAttribute));

            foreach (var attribute in swaggerResponseExampleAttributes)
            {
                var attr = (SwaggerResponseExampleAttribute)attribute;
                var statusCode = attr.StatusCode.ToString();

                var response = operation.Responses.FirstOrDefault(r => r.Key == statusCode);

                if (response.Equals(default(KeyValuePair<string, Response>)) == false)
                {
                    if (response.Value != null)
                    {
                        var provider = (IExamplesProvider)Activator.CreateInstance(attr.ExamplesProviderType);
                        var formatter = (IContractResolver)Activator.CreateInstance(attr.JsonResolver);
                        response.Value.Examples = FormatAsJson(provider,formatter);
                    }
                }
            }
        }

        private static object ConvertToDesiredCase(Dictionary<string, object> examples, IContractResolver resolver)
        {
            var jsonString = JsonConvert.SerializeObject(examples, new JsonSerializerSettings { ContractResolver = resolver });
            return JsonConvert.DeserializeObject(jsonString);
        }

        private static object FormatAsJson(IExamplesProvider provider, IContractResolver resolver)
        {
            var examples = new Dictionary<string, object>
            {
                {
                    "application/json", provider.GetExamples()
                }
            };

            return ConvertToDesiredCase(examples,resolver);
        }
    }
}