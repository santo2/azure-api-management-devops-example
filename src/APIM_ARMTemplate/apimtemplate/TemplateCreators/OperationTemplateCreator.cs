﻿using System.Collections.Generic;
using Microsoft.OpenApi.Models;
using System.Linq;
using Newtonsoft.Json;
using Microsoft.OpenApi.Any;

namespace Microsoft.Azure.Management.ApiManagement.ArmTemplates
{
    public class OperationTemplateCreator
    {
        public List<OperationTemplate> CreateOperationTemplates(OpenApiDocument doc)
        {
            List<OperationTemplate> operationTemplates = new List<OperationTemplate>();
            foreach (KeyValuePair<string, OpenApiPathItem> path in doc.Paths)
            {
                foreach (KeyValuePair<OperationType, OpenApiOperation> operation in path.Value.Operations)
                {
                    OperationTemplate op = new OperationTemplate()
                    {
                        name = operation.Value.OperationId,
                        type = "Microsoft.ApiManagement/service/apis/operations",
                        apiVersion = "2018-06-01-preview",
                        properties = new OperationTemplateProperties()
                        {
                            method = operation.Key.ToString(),
                            urlTemplate = path.Key,
                            description = operation.Value.Description,
                            displayName = operation.Value.Summary,
                            templateParameters = CreateTemplateParameters(operation.Value.Parameters).ToArray(),
                            responses = CreateOperationResponses(operation.Value.Responses).ToArray(),
                            request = CreateOperationRequest(operation.Value),
                            //unfinished
                            policies = null
                        }
                    };
                    operationTemplates.Add(op);
                }
            }
            
            return operationTemplates;
        }

        public OperationTemplateRequest CreateOperationRequest(OpenApiOperation operation)
        {
            OperationTemplateRequest request = new OperationTemplateRequest()
            {
                description = operation.RequestBody != null ? operation.RequestBody.Description : null,
                // request parameters with parameter location query
                queryParameters = CreateTemplateParameters(operation.Parameters.Where(p => p.In == ParameterLocation.Query).ToList()).ToArray(),
                // request parameters with parameter location header
                headers = CreateTemplateParameters(operation.Parameters.Where(p => p.In == ParameterLocation.Header).ToList()).ToArray(),
                representations = operation.RequestBody != null ? CreateRepresentations(operation.RequestBody.Content).ToArray() : null
            };
            return request;
        }

        public List<OperationsTemplateResponse> CreateOperationResponses(OpenApiResponses operationResponses)
        {
            List<OperationsTemplateResponse> responses = new List<OperationsTemplateResponse>();
            foreach (KeyValuePair<string, OpenApiResponse> response in operationResponses)
            {
                OperationsTemplateResponse res = new OperationsTemplateResponse()
                {
                    statusCode = response.Key,
                    description = response.Value.Description,
                    headers = CreateResponseHeaders(response.Value.Headers).ToArray(),
                    representations = CreateRepresentations(response.Value.Content).ToArray()
                };
            }
            return responses;
        }

        public List<OperationTemplateRepresentation> CreateRepresentations(IDictionary<string, OpenApiMediaType> content)
        {
            List<OperationTemplateRepresentation> representations = new List<OperationTemplateRepresentation>();
            foreach (KeyValuePair<string, OpenApiMediaType> pair in content)
            {
                // use representation examples to create values and default value
                OpenApiParameterHeaderIntersection param = new OpenApiParameterHeaderIntersection()
                {
                    Example = pair.Value.Example,
                    Examples = pair.Value.Examples
                };
                OperationTemplateRepresentation representation = new OperationTemplateRepresentation()
                {
                    contentType = pair.Key,
                    sample = JsonConvert.SerializeObject(CreateParameterDefaultValue(param)),
                    // schema has not yet been created, id is null
                    schemaId = null,
                    typeName = pair.Value.Schema != null ? pair.Value.Schema.Type : null,
                    formParameters = null
                };
                // if content type is neither application/x-www-form-urlencoded or multipart/form-data form parameters are null
                if (pair.Value.Schema != null && (pair.Key == "application/x-www-form-urlencoded" || pair.Key == "multipart/form-data"))
                {
                    representation.formParameters = CreateFormParameters(pair.Value.Schema).ToArray();
                };
                representations.Add(representation);
            }
            return representations;

        }

        public List<OperationTemplateParameter> CreateFormParameters(OpenApiSchema schema)
        {
            List<OperationTemplateParameter> formParameters = new List<OperationTemplateParameter>();
            if(schema.Example != null)
            {
                foreach (KeyValuePair<string, OperationSchemaExample> pair in schema.Example as Dictionary<string, OperationSchemaExample>)
                {
                    OperationTemplateParameter formParameter = new OperationTemplateParameter()
                    {
                        name = pair.Key,
                        required = schema.Required.FirstOrDefault(val => val == pair.Key) != null,
                        defaultValue = (JsonConvert.SerializeObject(pair.Value.Value))
                    };
                    formParameters.Add(formParameter);
                };
            }
            return formParameters;
        }

        public List<OperationTemplateParameter> CreateResponseHeaders(IDictionary<string, OpenApiHeader> headerPairs)
        {
            List<OperationTemplateParameter> headers = new List<OperationTemplateParameter>();
            foreach (KeyValuePair<string, OpenApiHeader> pair in headerPairs)
            {
                // use header examples to create values and default value
                OpenApiParameterHeaderIntersection param = new OpenApiParameterHeaderIntersection()
                {
                    Example = pair.Value.Example,
                    Examples = pair.Value.Examples
                };
                OperationTemplateParameter headerTemplate = new OperationTemplateParameter()
                {
                    name = pair.Key,
                    description = pair.Value.Description,
                    type = pair.Value.Schema.Type,
                    required = pair.Value.Required,
                    values = CreateParameterValues(param).ToArray(),
                    defaultValue = CreateParameterDefaultValue(param)
                };
                headers.Add(headerTemplate);
            };
            return headers;
        }

        public List<OperationTemplateParameter> CreateTemplateParameters(IList<OpenApiParameter> parameters)
        {
            List<OperationTemplateParameter> templateParameters = new List<OperationTemplateParameter>();
            foreach (OpenApiParameter parameter in parameters)
            {
                // use parameter examples to create values and default value
                OpenApiParameterHeaderIntersection param = new OpenApiParameterHeaderIntersection()
                {
                    Example = parameter.Example,
                    Examples = parameter.Examples
                };
                OperationTemplateParameter templateParameter = new OperationTemplateParameter()
                {
                    name = parameter.Name,
                    description = parameter.Description,
                    type = parameter.Schema.Type,
                    required = parameter.Required,
                    values = CreateParameterValues(param).ToArray(),
                    defaultValue = CreateParameterDefaultValue(param)
                };
                templateParameters.Add(templateParameter);
            }
            return templateParameters;
        }

        public List<string> CreateParameterValues(OpenApiParameterHeaderIntersection parameter)
        {
            List<string> values = new List<string>();
            if (parameter.Example != null)
            {
                // add example property to values
                values.Add(JsonConvert.SerializeObject(parameter.Example));

            }
            foreach (KeyValuePair<string, OpenApiExample> example in parameter.Examples)
            {
                // add each example in examples list property to values
                values.Add(JsonConvert.SerializeObject(example.Value));
            }
            return values;
        }

        public string CreateParameterDefaultValue(OpenApiParameterHeaderIntersection parameter)
        {
            if (parameter.Example != null)
            {
                // use example property for default value if given
                return JsonConvert.SerializeObject(parameter.Example);

            }
            else if (parameter.Examples != null)
            {
                // use first example in examples list property for default value if example property is not given
                return JsonConvert.SerializeObject(parameter.Examples.SingleOrDefault().Value);
            }
            else
            {
                return null;
            }
        }
    }

    // used to create parameter values
    public class OpenApiParameterHeaderIntersection {
        public IOpenApiAny Example { get; set; }
        public IDictionary<string, OpenApiExample> Examples { get; set; }
    }

    // used to give compiler known object structure in order to create form parameters
    public class OperationSchemaExample
    {
        public object Value { get; set; }
    }
}
