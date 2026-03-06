using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Utils;

namespace AzureOpsCrew.Domain.Tools.BackEnd;

public class GetMyIpTool : ITool
{
    private static readonly HttpClient HttpClient = new();

    public ToolDeclaration GetDeclaration()
    {
        return new ToolDeclaration
        {
            Name = "getMyIp",
            Description = "Gets the user's IP information including IP address, location, ISP, and other network details and returns raw JSON.",
            JsonSchema = JsonUtils.Schema("""
                                          {
                                            "type": "object",
                                            "properties": {},
                                            "additionalProperties": false
                                          }
                                          """).ToString(),
            ReturnJsonSchema = JsonUtils.Schema("""
                                                {
                                                    "type": "object",
                                                    "properties": {
                                                        "ipVersion": { "type": "number", "description": "IP version (4 or 6)" },
                                                        "ipAddress": { "type": "string", "description": "Public IP address" },
                                                        "latitude": { "type": "number", "description": "Latitude coordinate" },
                                                        "longitude": { "type": "number", "description": "Longitude coordinate" },
                                                        "countryName": { "type": "string", "description": "Country name" },
                                                        "countryCode": { "type": "string", "description": "Country code (e.g., US)" },
                                                        "capital": { "type": "string", "description": "Capital city" },
                                                        "phoneCodes": { "type": "array", "items": { "type": "number" }, "description": "Country phone codes" },
                                                        "timeZones": { "type": "array", "items": { "type": "string" }, "description": "Timezone identifiers" },
                                                        "zipCode": { "type": "string", "description": "Postal/ZIP code" },
                                                        "cityName": { "type": "string", "description": "City name" },
                                                        "regionName": { "type": "string", "description": "Region/State name" },
                                                        "regionCode": { "type": "string", "description": "Region/State code" },
                                                        "continent": { "type": "string", "description": "Continent name" },
                                                        "continentCode": { "type": "string", "description": "Continent code" },
                                                        "currencies": { "type": "array", "items": { "type": "string" }, "description": "Currency codes" },
                                                        "languages": { "type": "array", "items": { "type": "string" }, "description": "Language codes" },
                                                        "asn": { "type": "string", "description": "Autonomous System Number" },
                                                        "asnOrganization": { "type": "string", "description": "ASN organization name" },
                                                        "isProxy": { "type": "boolean", "description": "Whether the IP is a proxy" }
                                                    },
                                                    "required": ["ipAddress", "countryName", "cityName"]
                                                }
                                                """).ToString(),
            ToolType = ToolType.BackEnd
        };
    }

    public async Task<ToolCallResult> ExecuteAsync(Agent agent, string callId, IDictionary<string, object?>? arguments)
    {
        try
        {
            var response = await HttpClient.GetAsync("https://free.freeipapi.com/api/json/");
            if (!response.IsSuccessStatusCode)
            {
                return new ToolCallResult(callId, new { ErrorMessage = $"Unsuccessful HTTP response code: {response.StatusCode}" }, IsError: true);
            }

            var content = await response.Content.ReadAsStringAsync();
            return new ToolCallResult(callId, content, IsError: false);
        }
        catch (Exception e)
        {
            return new ToolCallResult(callId, new { ErrorMessage = $"Error calling showMyIp API: {e.Message}" }, IsError: true);
        }
    }
}
