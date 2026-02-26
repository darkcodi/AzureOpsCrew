using System.Text.Json;
using Worker.Models;

namespace Worker.Tools;

public static class FrontEndTools
{
    public static List<ToolDeclaration> GetDeclarations()
    {
        return new List<ToolDeclaration>() { ShowMyIpTool() };
    }

    private static ToolDeclaration ShowMyIpTool()
    {
        return new ToolDeclaration
        {
            Name = "showMyIp",
            Description = "Display the user's IP information including IP address, location, ISP, and other network details in a visual card widget. NOTE: This is a FRONTEND tool, the returned data will be rendered in a user-friendly format on the frontend, not just raw JSON.",
            JsonSchema = Schema("""
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
            ReturnJsonSchema = Schema("""
                                      {
                                        "type": "object",
                                        "properties": {},
                                        "additionalProperties": false
                                      }
                                      """).ToString(),
            ToolType = ToolType.FrontEnd
        };
    }

    private static JsonElement Schema(string json)
        => JsonDocument.Parse(json).RootElement.Clone();
}
