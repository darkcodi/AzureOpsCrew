namespace Infrastructure.Ai.Tests.Clients.OpenAi;

public static class TestData
{
    public const string HTTP_REQUEST_1 = """
{"model":"gpt-5-2-chat","messages":[{"role":"system","content":"System prompt:\r\nYou are one of agents in group chat: agents \u002B human. When you have tools available, use them proactively to present information visually instead of plain text. Do NOT issue several tool calls in a row, and always wait for the result of a tool call before issuing another tool call. If you want to issue multiple tool calls, please issue them one by one and wait for the result of each tool call.\r\n\r\nAvailable backend tools:\r\nTool Name: getMyIp\r\nTool Description: Gets the user\u0027s IP information including IP address, location, ISP, and other network details and returns raw JSON.\r\nTool Type: BackEnd\r\nTool JSON Schema: {\r\n  \u0022type\u0022: \u0022object\u0022,\r\n  \u0022properties\u0022: {},\r\n  \u0022additionalProperties\u0022: false\r\n}\r\nTool Return JSON Schema: {\r\n    \u0022type\u0022: \u0022object\u0022,\r\n    \u0022properties\u0022: {\r\n        \u0022ipVersion\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022IP version (4 or 6)\u0022 },\r\n        \u0022ipAddress\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Public IP address\u0022 },\r\n        \u0022latitude\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022Latitude coordinate\u0022 },\r\n        \u0022longitude\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022Longitude coordinate\u0022 },\r\n        \u0022countryName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Country name\u0022 },\r\n        \u0022countryCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Country code (e.g., US)\u0022 },\r\n        \u0022capital\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Capital city\u0022 },\r\n        \u0022phoneCodes\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022number\u0022 }, \u0022description\u0022: \u0022Country phone codes\u0022 },\r\n        \u0022timeZones\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Timezone identifiers\u0022 },\r\n        \u0022zipCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Postal/ZIP code\u0022 },\r\n        \u0022cityName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022City name\u0022 },\r\n        \u0022regionName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Region/State name\u0022 },\r\n        \u0022regionCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Region/State code\u0022 },\r\n        \u0022continent\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Continent name\u0022 },\r\n        \u0022continentCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Continent code\u0022 },\r\n        \u0022currencies\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Currency codes\u0022 },\r\n        \u0022languages\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Language codes\u0022 },\r\n        \u0022asn\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Autonomous System Number\u0022 },\r\n        \u0022asnOrganization\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022ASN organization name\u0022 },\r\n        \u0022isProxy\u0022: { \u0022type\u0022: \u0022boolean\u0022, \u0022description\u0022: \u0022Whether the IP is a proxy\u0022 }\r\n    },\r\n    \u0022required\u0022: [\u0022ipAddress\u0022, \u0022countryName\u0022, \u0022cityName\u0022]\r\n}\r\n\r\nAvailable frontend tools:\r\nTool Name: showMyIp\r\nTool Description: Display the user\u0027s IP information including IP address, location, ISP, and other network details in a visual card widget. NOTE: This is a FRONTEND tool, the returned data will be rendered in a user-friendly format on the frontend, not just raw JSON.\r\nTool Type: FrontEnd\r\nTool JSON Schema: {\r\n    \u0022type\u0022: \u0022object\u0022,\r\n    \u0022properties\u0022: {\r\n        \u0022ipVersion\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022IP version (4 or 6)\u0022 },\r\n        \u0022ipAddress\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Public IP address\u0022 },\r\n        \u0022latitude\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022Latitude coordinate\u0022 },\r\n        \u0022longitude\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022Longitude coordinate\u0022 },\r\n        \u0022countryName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Country name\u0022 },\r\n        \u0022countryCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Country code (e.g., US)\u0022 },\r\n        \u0022capital\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Capital city\u0022 },\r\n        \u0022phoneCodes\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022number\u0022 }, \u0022description\u0022: \u0022Country phone codes\u0022 },\r\n        \u0022timeZones\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Timezone identifiers\u0022 },\r\n        \u0022zipCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Postal/ZIP code\u0022 },\r\n        \u0022cityName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022City name\u0022 },\r\n        \u0022regionName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Region/State name\u0022 },\r\n        \u0022regionCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Region/State code\u0022 },\r\n        \u0022continent\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Continent name\u0022 },\r\n        \u0022continentCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Continent code\u0022 },\r\n        \u0022currencies\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Currency codes\u0022 },\r\n        \u0022languages\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Language codes\u0022 },\r\n        \u0022asn\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Autonomous System Number\u0022 },\r\n        \u0022asnOrganization\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022ASN organization name\u0022 },\r\n        \u0022isProxy\u0022: { \u0022type\u0022: \u0022boolean\u0022, \u0022description\u0022: \u0022Whether the IP is a proxy\u0022 }\r\n    },\r\n    \u0022required\u0022: [\u0022ipAddress\u0022, \u0022countryName\u0022, \u0022cityName\u0022]\r\n}\r\nTool Return JSON Schema: {\r\n  \u0022type\u0022: \u0022object\u0022,\r\n  \u0022properties\u0022: {},\r\n  \u0022additionalProperties\u0022: false\r\n}\n\nTool Name: showDeployment\r\nTool Description: Display a deployment status card in the chat. No parameters; the card is shown with default/empty state.\r\nTool Type: FrontEnd\r\nTool JSON Schema: {\r\n    \u0022type\u0022: \u0022object\u0022,\r\n    \u0022properties\u0022: {},\r\n    \u0022additionalProperties\u0022: false\r\n}\r\nTool Return JSON Schema: {\r\n  \u0022type\u0022: \u0022object\u0022,\r\n  \u0022properties\u0022: {},\r\n  \u0022additionalProperties\u0022: false\r\n}\r\n\r\nYour name is:\r\nManager\r\n\r\nYour description is:\r\n\r\n\r\nUser prompt:\r\nYou are a Manager AI assistant. You help with planning, priorities, resource allocation, team coordination, and delivery. You think in terms of goals, milestones, risks, and stakeholder communication. Keep answers actionable and concise."},{"role":"user","content":"heya","name":"User"}],"stream":true,"tools":[{"type":"function","function":{"name":"getMyIp","description":"Gets the user\u0027s IP information including IP address, location, ISP, and other network details and returns raw JSON."}},{"type":"function","function":{"name":"showMyIp","description":"Display the user\u0027s IP information including IP address, location, ISP, and other network details in a visual card widget. NOTE: This is a FRONTEND tool, the returned data will be rendered in a user-friendly format on the frontend, not just raw JSON."}},{"type":"function","function":{"name":"showDeployment","description":"Display a deployment status card in the chat. No parameters; the card is shown with default/empty state."}}],"tool_choice":"auto"}
""";

    public const string HTTP_RESPONSE_1 = """
data: {"choices":[],"created":0,"id":"","model":"","object":"","prompt_filter_results":[{"prompt_index":0,"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}}}]}


data: {"choices":[{"content_filter_results":{},"delta":{"content":"","refusal":null,"role":"assistant"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"70NTC3qxf","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":"Hey"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"wC83yD3e","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":"!"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"MUU9Wlf7Tt","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" 👋"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"0dTpWVJY0","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":"  \n"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"P1fxTYH","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":"What"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"VI9gq1W","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" are"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"oIZJFv6","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" you"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"Axlo9ie","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" working"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"aDm","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" on"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"Y8odWEmJ","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" right"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"ToAnf","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" now"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"NMf9BwU","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":","},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"KFN5Y2WOM1","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" and"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"PQGITPd","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" how"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"koRds0s","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" can"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"SWbSgnc","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" I"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"q00T9IKoU","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" help"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"mKcEt5","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" you"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"xRpbokN","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" plan"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"1NCUzg","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" or"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"dTYmpQdc","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" move"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"KLtNa4","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" it"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"IE4H2jqw","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":" forward"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"zZG","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}},"delta":{"content":"?"},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"5NtykSlG2s","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{},"finish_reason":"stop","index":0,"logprobs":null}],"created":1772304649,"id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG","model":"gpt-5.2-chat-2025-12-11","obfuscation":"wnbgD","object":"chat.completion.chunk","system_fingerprint":null}



""";

    public const string HTTP_REQUEST_2 = """
{"model":"gpt-5-2-chat","messages":[{"role":"system","content":"System prompt:\r\nYou are one of agents in group chat: agents \u002B human. When you have tools available, use them proactively to present information visually instead of plain text. Do NOT issue several tool calls in a row, and always wait for the result of a tool call before issuing another tool call. If you want to issue multiple tool calls, please issue them one by one and wait for the result of each tool call.\r\n\r\nAvailable backend tools:\r\nTool Name: getMyIp\r\nTool Description: Gets the user\u0027s IP information including IP address, location, ISP, and other network details and returns raw JSON.\r\nTool Type: BackEnd\r\nTool JSON Schema: {\r\n  \u0022type\u0022: \u0022object\u0022,\r\n  \u0022properties\u0022: {},\r\n  \u0022additionalProperties\u0022: false\r\n}\r\nTool Return JSON Schema: {\r\n    \u0022type\u0022: \u0022object\u0022,\r\n    \u0022properties\u0022: {\r\n        \u0022ipVersion\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022IP version (4 or 6)\u0022 },\r\n        \u0022ipAddress\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Public IP address\u0022 },\r\n        \u0022latitude\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022Latitude coordinate\u0022 },\r\n        \u0022longitude\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022Longitude coordinate\u0022 },\r\n        \u0022countryName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Country name\u0022 },\r\n        \u0022countryCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Country code (e.g., US)\u0022 },\r\n        \u0022capital\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Capital city\u0022 },\r\n        \u0022phoneCodes\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022number\u0022 }, \u0022description\u0022: \u0022Country phone codes\u0022 },\r\n        \u0022timeZones\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Timezone identifiers\u0022 },\r\n        \u0022zipCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Postal/ZIP code\u0022 },\r\n        \u0022cityName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022City name\u0022 },\r\n        \u0022regionName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Region/State name\u0022 },\r\n        \u0022regionCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Region/State code\u0022 },\r\n        \u0022continent\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Continent name\u0022 },\r\n        \u0022continentCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Continent code\u0022 },\r\n        \u0022currencies\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Currency codes\u0022 },\r\n        \u0022languages\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Language codes\u0022 },\r\n        \u0022asn\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Autonomous System Number\u0022 },\r\n        \u0022asnOrganization\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022ASN organization name\u0022 },\r\n        \u0022isProxy\u0022: { \u0022type\u0022: \u0022boolean\u0022, \u0022description\u0022: \u0022Whether the IP is a proxy\u0022 }\r\n    },\r\n    \u0022required\u0022: [\u0022ipAddress\u0022, \u0022countryName\u0022, \u0022cityName\u0022]\r\n}\r\n\r\nAvailable frontend tools:\r\nTool Name: showMyIp\r\nTool Description: Display the user\u0027s IP information including IP address, location, ISP, and other network details in a visual card widget. NOTE: This is a FRONTEND tool, the returned data will be rendered in a user-friendly format on the frontend, not just raw JSON.\r\nTool Type: FrontEnd\r\nTool JSON Schema: {\r\n    \u0022type\u0022: \u0022object\u0022,\r\n    \u0022properties\u0022: {\r\n        \u0022ipVersion\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022IP version (4 or 6)\u0022 },\r\n        \u0022ipAddress\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Public IP address\u0022 },\r\n        \u0022latitude\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022Latitude coordinate\u0022 },\r\n        \u0022longitude\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022Longitude coordinate\u0022 },\r\n        \u0022countryName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Country name\u0022 },\r\n        \u0022countryCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Country code (e.g., US)\u0022 },\r\n        \u0022capital\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Capital city\u0022 },\r\n        \u0022phoneCodes\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022number\u0022 }, \u0022description\u0022: \u0022Country phone codes\u0022 },\r\n        \u0022timeZones\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Timezone identifiers\u0022 },\r\n        \u0022zipCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Postal/ZIP code\u0022 },\r\n        \u0022cityName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022City name\u0022 },\r\n        \u0022regionName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Region/State name\u0022 },\r\n        \u0022regionCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Region/State code\u0022 },\r\n        \u0022continent\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Continent name\u0022 },\r\n        \u0022continentCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Continent code\u0022 },\r\n        \u0022currencies\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Currency codes\u0022 },\r\n        \u0022languages\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Language codes\u0022 },\r\n        \u0022asn\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Autonomous System Number\u0022 },\r\n        \u0022asnOrganization\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022ASN organization name\u0022 },\r\n        \u0022isProxy\u0022: { \u0022type\u0022: \u0022boolean\u0022, \u0022description\u0022: \u0022Whether the IP is a proxy\u0022 }\r\n    },\r\n    \u0022required\u0022: [\u0022ipAddress\u0022, \u0022countryName\u0022, \u0022cityName\u0022]\r\n}\r\nTool Return JSON Schema: {\r\n  \u0022type\u0022: \u0022object\u0022,\r\n  \u0022properties\u0022: {},\r\n  \u0022additionalProperties\u0022: false\r\n}\n\nTool Name: showDeployment\r\nTool Description: Display a deployment status card in the chat. No parameters; the card is shown with default/empty state.\r\nTool Type: FrontEnd\r\nTool JSON Schema: {\r\n    \u0022type\u0022: \u0022object\u0022,\r\n    \u0022properties\u0022: {},\r\n    \u0022additionalProperties\u0022: false\r\n}\r\nTool Return JSON Schema: {\r\n  \u0022type\u0022: \u0022object\u0022,\r\n  \u0022properties\u0022: {},\r\n  \u0022additionalProperties\u0022: false\r\n}\r\n\r\nYour name is:\r\nManager\r\n\r\nYour description is:\r\n\r\n\r\nUser prompt:\r\nYou are a Manager AI assistant. You help with planning, priorities, resource allocation, team coordination, and delivery. You think in terms of goals, milestones, risks, and stakeholder communication. Keep answers actionable and concise."},{"role":"user","content":"heya","name":"User"},{"role":"assistant","content":"Hey! \uD83D\uDC4B  \nWhat are you working on right now, and how can I help you plan or move it forward?","name":"Manager"},{"role":"user","content":"what\u0027s my ip?","name":"User"}],"stream":true,"tools":[{"type":"function","function":{"name":"getMyIp","description":"Gets the user\u0027s IP information including IP address, location, ISP, and other network details and returns raw JSON."}},{"type":"function","function":{"name":"showMyIp","description":"Display the user\u0027s IP information including IP address, location, ISP, and other network details in a visual card widget. NOTE: This is a FRONTEND tool, the returned data will be rendered in a user-friendly format on the frontend, not just raw JSON."}},{"type":"function","function":{"name":"showDeployment","description":"Display a deployment status card in the chat. No parameters; the card is shown with default/empty state."}}],"tool_choice":"auto"}
""";

    public const string HTTP_RESPONSE_2 = """
data: {"choices":[],"created":0,"id":"","model":"","object":"","prompt_filter_results":[{"prompt_index":0,"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}}}]}


data: {"choices":[{"content_filter_results":{},"delta":{"content":null,"refusal":null,"role":"assistant","tool_calls":[{"function":{"arguments":"","name":"getMyIp"},"id":"call_uLWyKTpLtOsRmTudAFNQ2Vwv","index":0,"type":"function"}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304653,"id":"chatcmpl-DEJgre24uAUHKoNbjvT3tZcrLP4tr","model":"gpt-5.2-chat-2025-12-11","obfuscation":"beU6bph0","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"{}"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304653,"id":"chatcmpl-DEJgre24uAUHKoNbjvT3tZcrLP4tr","model":"gpt-5.2-chat-2025-12-11","obfuscation":"Q5SzSiCUyQonOyp","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{},"finish_reason":"tool_calls","index":0,"logprobs":null}],"created":1772304653,"id":"chatcmpl-DEJgre24uAUHKoNbjvT3tZcrLP4tr","model":"gpt-5.2-chat-2025-12-11","obfuscation":"IHPfk57LB1qbD0j","object":"chat.completion.chunk","system_fingerprint":null}



""";

    public const string HTTP_REQUEST_3 = """
{"model":"gpt-5-2-chat","messages":[{"role":"system","content":"System prompt:\r\nYou are one of agents in group chat: agents \u002B human. When you have tools available, use them proactively to present information visually instead of plain text. Do NOT issue several tool calls in a row, and always wait for the result of a tool call before issuing another tool call. If you want to issue multiple tool calls, please issue them one by one and wait for the result of each tool call.\r\n\r\nAvailable backend tools:\r\nTool Name: getMyIp\r\nTool Description: Gets the user\u0027s IP information including IP address, location, ISP, and other network details and returns raw JSON.\r\nTool Type: BackEnd\r\nTool JSON Schema: {\r\n  \u0022type\u0022: \u0022object\u0022,\r\n  \u0022properties\u0022: {},\r\n  \u0022additionalProperties\u0022: false\r\n}\r\nTool Return JSON Schema: {\r\n    \u0022type\u0022: \u0022object\u0022,\r\n    \u0022properties\u0022: {\r\n        \u0022ipVersion\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022IP version (4 or 6)\u0022 },\r\n        \u0022ipAddress\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Public IP address\u0022 },\r\n        \u0022latitude\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022Latitude coordinate\u0022 },\r\n        \u0022longitude\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022Longitude coordinate\u0022 },\r\n        \u0022countryName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Country name\u0022 },\r\n        \u0022countryCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Country code (e.g., US)\u0022 },\r\n        \u0022capital\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Capital city\u0022 },\r\n        \u0022phoneCodes\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022number\u0022 }, \u0022description\u0022: \u0022Country phone codes\u0022 },\r\n        \u0022timeZones\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Timezone identifiers\u0022 },\r\n        \u0022zipCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Postal/ZIP code\u0022 },\r\n        \u0022cityName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022City name\u0022 },\r\n        \u0022regionName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Region/State name\u0022 },\r\n        \u0022regionCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Region/State code\u0022 },\r\n        \u0022continent\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Continent name\u0022 },\r\n        \u0022continentCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Continent code\u0022 },\r\n        \u0022currencies\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Currency codes\u0022 },\r\n        \u0022languages\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Language codes\u0022 },\r\n        \u0022asn\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Autonomous System Number\u0022 },\r\n        \u0022asnOrganization\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022ASN organization name\u0022 },\r\n        \u0022isProxy\u0022: { \u0022type\u0022: \u0022boolean\u0022, \u0022description\u0022: \u0022Whether the IP is a proxy\u0022 }\r\n    },\r\n    \u0022required\u0022: [\u0022ipAddress\u0022, \u0022countryName\u0022, \u0022cityName\u0022]\r\n}\r\n\r\nAvailable frontend tools:\r\nTool Name: showMyIp\r\nTool Description: Display the user\u0027s IP information including IP address, location, ISP, and other network details in a visual card widget. NOTE: This is a FRONTEND tool, the returned data will be rendered in a user-friendly format on the frontend, not just raw JSON.\r\nTool Type: FrontEnd\r\nTool JSON Schema: {\r\n    \u0022type\u0022: \u0022object\u0022,\r\n    \u0022properties\u0022: {\r\n        \u0022ipVersion\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022IP version (4 or 6)\u0022 },\r\n        \u0022ipAddress\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Public IP address\u0022 },\r\n        \u0022latitude\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022Latitude coordinate\u0022 },\r\n        \u0022longitude\u0022: { \u0022type\u0022: \u0022number\u0022, \u0022description\u0022: \u0022Longitude coordinate\u0022 },\r\n        \u0022countryName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Country name\u0022 },\r\n        \u0022countryCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Country code (e.g., US)\u0022 },\r\n        \u0022capital\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Capital city\u0022 },\r\n        \u0022phoneCodes\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022number\u0022 }, \u0022description\u0022: \u0022Country phone codes\u0022 },\r\n        \u0022timeZones\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Timezone identifiers\u0022 },\r\n        \u0022zipCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Postal/ZIP code\u0022 },\r\n        \u0022cityName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022City name\u0022 },\r\n        \u0022regionName\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Region/State name\u0022 },\r\n        \u0022regionCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Region/State code\u0022 },\r\n        \u0022continent\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Continent name\u0022 },\r\n        \u0022continentCode\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Continent code\u0022 },\r\n        \u0022currencies\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Currency codes\u0022 },\r\n        \u0022languages\u0022: { \u0022type\u0022: \u0022array\u0022, \u0022items\u0022: { \u0022type\u0022: \u0022string\u0022 }, \u0022description\u0022: \u0022Language codes\u0022 },\r\n        \u0022asn\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022Autonomous System Number\u0022 },\r\n        \u0022asnOrganization\u0022: { \u0022type\u0022: \u0022string\u0022, \u0022description\u0022: \u0022ASN organization name\u0022 },\r\n        \u0022isProxy\u0022: { \u0022type\u0022: \u0022boolean\u0022, \u0022description\u0022: \u0022Whether the IP is a proxy\u0022 }\r\n    },\r\n    \u0022required\u0022: [\u0022ipAddress\u0022, \u0022countryName\u0022, \u0022cityName\u0022]\r\n}\r\nTool Return JSON Schema: {\r\n  \u0022type\u0022: \u0022object\u0022,\r\n  \u0022properties\u0022: {},\r\n  \u0022additionalProperties\u0022: false\r\n}\n\nTool Name: showDeployment\r\nTool Description: Display a deployment status card in the chat. No parameters; the card is shown with default/empty state.\r\nTool Type: FrontEnd\r\nTool JSON Schema: {\r\n    \u0022type\u0022: \u0022object\u0022,\r\n    \u0022properties\u0022: {},\r\n    \u0022additionalProperties\u0022: false\r\n}\r\nTool Return JSON Schema: {\r\n  \u0022type\u0022: \u0022object\u0022,\r\n  \u0022properties\u0022: {},\r\n  \u0022additionalProperties\u0022: false\r\n}\r\n\r\nYour name is:\r\nManager\r\n\r\nYour description is:\r\n\r\n\r\nUser prompt:\r\nYou are a Manager AI assistant. You help with planning, priorities, resource allocation, team coordination, and delivery. You think in terms of goals, milestones, risks, and stakeholder communication. Keep answers actionable and concise."},{"role":"user","content":"heya","name":"User"},{"role":"assistant","content":"Hey! \uD83D\uDC4B  \nWhat are you working on right now, and how can I help you plan or move it forward?","name":"Manager"},{"role":"user","content":"what\u0027s my ip?","name":"User"},{"role":"assistant","name":"Manager","tool_calls":[{"id":"call_uLWyKTpLtOsRmTudAFNQ2Vwv","type":"function","function":{"name":"getMyIp","arguments":"{}"}}]},{"role":"tool","content":"{\u0022SerializedResult\u0022:\u0022{\\u0022ipVersion\\u0022:4,\\u0022ipAddress\\u0022:\\u0022104.186.12.123\\u0022,\\u0022latitude\\u0022:29.7432,\\u0022longitude\\u0022:-95.3887,\\u0022countryName\\u0022:\\u0022United States\\u0022,\\u0022countryCode\\u0022:\\u0022US\\u0022,\\u0022capital\\u0022:\\u0022Washington D.C.\\u0022,\\u0022phoneCodes\\u0022:[1],\\u0022timeZones\\u0022:[\\u0022America\\\\/Adak\\u0022,\\u0022America\\\\/Anchorage\\u0022,\\u0022America\\\\/Boise\\u0022,\\u0022America\\\\/Chicago\\u0022,\\u0022America\\\\/Denver\\u0022,\\u0022America\\\\/Detroit\\u0022,\\u0022America\\\\/Indiana\\\\/Indianapolis\\u0022,\\u0022America\\\\/Indiana\\\\/Knox\\u0022,\\u0022America\\\\/Indiana\\\\/Marengo\\u0022,\\u0022America\\\\/Indiana\\\\/Petersburg\\u0022,\\u0022America\\\\/Indiana\\\\/Tell_City\\u0022,\\u0022America\\\\/Indiana\\\\/Vevay\\u0022,\\u0022America\\\\/Indiana\\\\/Vincennes\\u0022,\\u0022America\\\\/Indiana\\\\/Winamac\\u0022,\\u0022America\\\\/Juneau\\u0022,\\u0022America\\\\/Kentucky\\\\/Louisville\\u0022,\\u0022America\\\\/Kentucky\\\\/Monticello\\u0022,\\u0022America\\\\/Los_Angeles\\u0022,\\u0022America\\\\/Menominee\\u0022,\\u0022America\\\\/Metlakatla\\u0022,\\u0022America\\\\/New_York\\u0022,\\u0022America\\\\/Nome\\u0022,\\u0022America\\\\/North_Dakota\\\\/Beulah\\u0022,\\u0022America\\\\/North_Dakota\\\\/Center\\u0022,\\u0022America\\\\/North_Dakota\\\\/New_Salem\\u0022,\\u0022America\\\\/Phoenix\\u0022,\\u0022America\\\\/Sitka\\u0022,\\u0022America\\\\/Yakutat\\u0022,\\u0022Pacific\\\\/Honolulu\\u0022],\\u0022zipCode\\u0022:\\u002277250\\u0022,\\u0022cityName\\u0022:\\u0022Houston\\u0022,\\u0022regionName\\u0022:\\u0022Texas\\u0022,\\u0022regionCode\\u0022:\\u0022TX\\u0022,\\u0022continent\\u0022:\\u0022Americas\\u0022,\\u0022continentCode\\u0022:\\u0022AM\\u0022,\\u0022currencies\\u0022:[\\u0022USD\\u0022,\\u0022USN\\u0022,\\u0022USS\\u0022],\\u0022languages\\u0022:[\\u0022en\\u0022],\\u0022asn\\u0022:\\u00227018\\u0022,\\u0022asnOrganization\\u0022:\\u0022AT\\u0026T Enterprises, LLC\\u0022,\\u0022isProxy\\u0022:false}\u0022,\u0022IsError\u0022:false}","tool_call_id":"call_uLWyKTpLtOsRmTudAFNQ2Vwv"}],"stream":true,"tools":[{"type":"function","function":{"name":"getMyIp","description":"Gets the user\u0027s IP information including IP address, location, ISP, and other network details and returns raw JSON."}},{"type":"function","function":{"name":"showMyIp","description":"Display the user\u0027s IP information including IP address, location, ISP, and other network details in a visual card widget. NOTE: This is a FRONTEND tool, the returned data will be rendered in a user-friendly format on the frontend, not just raw JSON."}},{"type":"function","function":{"name":"showDeployment","description":"Display a deployment status card in the chat. No parameters; the card is shown with default/empty state."}}],"tool_choice":"auto"}
""";

    public const string HTTP_RESPONSE_3 = """
data: {"choices":[],"created":0,"id":"","model":"","object":"","prompt_filter_results":[{"prompt_index":0,"content_filter_results":{"hate":{"filtered":false,"severity":"safe"},"self_harm":{"filtered":false,"severity":"safe"},"sexual":{"filtered":false,"severity":"safe"},"violence":{"filtered":false,"severity":"safe"}}}]}


data: {"choices":[{"content_filter_results":{},"delta":{"content":null,"refusal":null,"role":"assistant","tool_calls":[{"function":{"arguments":"","name":"showMyIp"},"id":"call_OW6GhPkVXNfP7dlpCEvCqAVK","index":0,"type":"function"}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"kj0ZAtv","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"{\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"87m7mvqkw7X9ci","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"ip"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"xSlTaW5PXRS5Hng","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Version"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"T2eZD1jJYX","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"0SrxYgYa761w0z","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"4"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":",\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"jonbnwRBnjexsI","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"ip"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"jSZv18wyFd2FUFh","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Address"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"QyKTtfHpSR","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"VVFtlzQi4FLZ","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"104"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"ha5u11b2mljWxC","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"."},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"186"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"Cq5GS3y7Y9LoeS","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"."},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"12"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"zQApMPOUdEH6ICJ","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"."},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"123"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"K5LfyAwxnjUIaL","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\",\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"dyxlmBFgZ2OF","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"latitude"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"sClcHYjsy","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"3QeRTYAzw2cUG0","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"29"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"fDgT2PKZc7mwjht","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"."},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"743"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"CdfEGlG4b9I5lv","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"2"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":",\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"VJWIMNfbaiEPfr","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"longitude"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"iP1uGjBs","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"fMNEFyKk4KVVBp","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"-"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"95"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"n0sUGK30MCeexkQ","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"."},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"388"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"hiPKfcaQIeHzAT","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"7"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":",\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"9xhypzGkW4LmD9","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"country"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"lZVJEPvq25","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Name"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"SYoWBw9Q6XB6u","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"o3ntav9LxAkx","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"United"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"FvaZuL6ijfc","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":" States"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"VCUNwduQ8D","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\",\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"NLLQXIuiZdqf","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"country"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"hA669boMKp","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Code"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"f8Ot59ECTNfie","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"llB5ucmewUSb","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"US"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"8cApYm4iPHLqxvv","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\",\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"zrQj896OdCiN","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"capital"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"KREEp1cYYh","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"Up9t0AuWNlg8","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Washington"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"72Yly3I","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":" D"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"vtcCHb7uX7GIL2y","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":".C"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"Z47GHMliHe84eDz","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":".\",\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"jXkqUDyxwVa","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"phone"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"SGLX1XuvCYvt","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Codes"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"ujxF83cKTQ7T","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":["},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"qTwxqEzNWgmH9","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"1"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"],\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"1NJy4upMEwKqc","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"time"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"ZhBeEj4PNV0LZ","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Zones"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"30M5HWaDs4un","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":[\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"CfL0Z5ZlRNO","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"America"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"FTHVSLQ8CJ","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"/"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Chicago"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"2UUicLbhMv","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\"],"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"yqx9seY8bX7xm","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"v56XJik2JMWXxqs","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"zip"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"DI1spon6uiILsp","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Code"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"J30ACiCXAuCJh","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"vSGz3fhWvdJ0","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"772"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"mn0gwSb3UZ1mpS","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"50"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"N57zeXDEir4PljG","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\",\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"nKR5IjdluYEJ","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"city"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"btUyWtrqZOaYG","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Name"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"Ws35whjn7J4gI","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"aLTzaratIr7Z","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Houston"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"QWrmY6zbuY","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\",\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"V4hC7f7fTlsy","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"region"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"G74NoDIkem5","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Name"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"HjG6rxUulZjRJ","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"cCfKO1O4II6E","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Texas"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"uJijSrq2mTOO","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\",\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"BQ7Bmxa7ZWtM","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"region"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"ob5FQFonuE4","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Code"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"37NIR9FOmOF7U","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"stvXIcYU3Vfd","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"TX"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"hHXJgkPtIiCasnQ","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\",\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"c9rhFmrzGfla","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"continent"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"IFio3cu0","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"t85892ZHyeHV","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Amer"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"UZlrAsTOurgBO","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"icas"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"NE6RHsmT9adSO","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\",\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"PjbfeBr98kt4","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"continent"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"LShjWdNg","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Code"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"DQOvL7GEnu09N","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"SNN07VFTrxsw","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"AM"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"t3KDjp8d3RsmM73","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\",\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"MzysQM6SwECL","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"curr"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"SWrBc02vzkywT","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"encies"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"z0E8GWJn4az","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":[\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"NAD480PC1Up","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"USD"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"RcvLlbXZ7cxJhM","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\"],"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"T0R2copDJaI6r","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"hL3eucpZ4cfp8s5","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"languages"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"Eeia5NUk","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":[\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"tucHFZlNLKe","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"en"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"4gWX8BngQC4ZYmt","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\"],"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"5NNkZkAiHxgEO","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"KelFGkoW8fM14pt","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"asn"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"RbMMLqosFAuJlA","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"6OM4UYIDqwDk","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"701"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"PGgbokB6UWAdor","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"8"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\",\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"J0SDcmdvxxvC","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"asn"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"OwYNwonV05tgRI","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Organization"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"F7584","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"ibt98Hf0i5Ry","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"AT"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"OncT0j6uieol9Cb","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\u0026T"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"6P1YTDyg1zAD0J9","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":" Enterprises"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"hlh7Y","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":","},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":" LLC"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"nZD8gE12Tjbfs","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\",\""},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"N0oMzpgpKG2r","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"is"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"t8kdBiqzqFKrYUA","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"Proxy"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"0mb3OsrPki9J","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"\":"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"swsyTRnX7lKv28","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"false"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"8FhmS47EXo82","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{"tool_calls":[{"function":{"arguments":"}"},"index":0}]},"finish_reason":null,"index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"","object":"chat.completion.chunk","system_fingerprint":null}


data: {"choices":[{"content_filter_results":{},"delta":{},"finish_reason":"tool_calls","index":0,"logprobs":null}],"created":1772304656,"id":"chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi","model":"gpt-5.2-chat-2025-12-11","obfuscation":"c2lCQzpKSXh6Kly","object":"chat.completion.chunk","system_fingerprint":null}



""";
}
