/** Result of a provider connection test (from Test button). */
export interface ProviderTestResult {
  success: boolean
  message?: string
  latencyMs?: number
  checkedAt?: string
  quota?: string
  availableModels?: Array<{ id: string; name: string }>
}

export interface ProviderConfig {
  id: string
  /** Backend GUID when loaded from or saved to the API */
  backendId?: string
  name: string
  providerType?: string
  status: "enabled" | "disabled" | "draft"
  apiKey: string
  baseUrl: string
  defaultModel: string
  timeout: number
  rateLimit: number
  availableModels: string[]
  isDefault: boolean
}

export interface AppearanceConfig {
  theme: "dark" | "light" | "system"
  fontSize: "small" | "medium" | "large"
  compactMode: boolean
}

export interface NotificationConfig {
  desktopNotifications: boolean
  soundEnabled: boolean
  mentionNotifications: boolean
}

export interface RoutingConfig {
  strategy: "round-robin" | "priority" | "cost-optimized" | "latency-optimized"
  fallbackEnabled: boolean
  maxRetries: number
}

export interface AdvancedConfig {
  debugMode: boolean
  logLevel: "error" | "warn" | "info" | "debug"
  requestTimeout: number
  maxConcurrentRequests: number
}

export interface AccountConfig {
  displayName: string
}

export interface SettingsState {
  providers: ProviderConfig[]
  account: AccountConfig
  appearance: AppearanceConfig
  notifications: NotificationConfig
  routing: RoutingConfig
  advanced: AdvancedConfig
}

export const defaultSettings: SettingsState = {
  providers: [
    {
      id: "openai",
      name: "OpenAI",
      providerType: "OpenAI",
      status: "enabled",
      apiKey: "sk-••••••••••••••••••••••••••••••••",
      baseUrl: "https://api.openai.com/v1",
      defaultModel: "",
      timeout: 30,
      rateLimit: 60,
      availableModels: [
        "gpt-5-2-chat (default)",
        "gpt-4.1-mini",
        "gpt-4.1",
        "gpt-4o-mini",
        "text-embedding-3-large",
      ],
      isDefault: true,
    },
    {
      id: "azure-openai",
      name: "Azure OpenAI",
      providerType: "Azure OpenAI",
      status: "disabled",
      apiKey: "",
      baseUrl: "https://your-resource.openai.azure.com/",
      defaultModel: "",
      timeout: 30,
      rateLimit: 60,
      availableModels: [],
      isDefault: false,
    },
    {
      id: "anthropic",
      name: "Anthropic",
      providerType: "Anthropic",
      status: "disabled",
      apiKey: "",
      baseUrl: "https://api.anthropic.com/v1",
      defaultModel: "",
      timeout: 30,
      rateLimit: 60,
      availableModels: [],
      isDefault: false,
    },
    {
      id: "ollama",
      name: "Ollama (Local)",
      providerType: "Ollama (Local)",
      status: "enabled",
      apiKey: "",
      baseUrl: "http://localhost:11434",
      defaultModel: "",
      timeout: 120,
      rateLimit: 0,
      availableModels: [],
      isDefault: false,
    },
    {
      id: "openrouter",
      name: "OpenRouter",
      providerType: "OpenRouter",
      status: "disabled",
      apiKey: "",
      baseUrl: "https://openrouter.ai/api/v1",
      defaultModel: "",
      timeout: 30,
      rateLimit: 60,
      availableModels: [],
      isDefault: false,
    },
  ],
  account: {
    displayName: "User",
  },
  appearance: {
    theme: "dark",
    fontSize: "medium",
    compactMode: false,
  },
  notifications: {
    desktopNotifications: true,
    soundEnabled: true,
    mentionNotifications: true,
  },
  routing: {
    strategy: "priority",
    fallbackEnabled: true,
    maxRetries: 3,
  },
  advanced: {
    debugMode: false,
    logLevel: "warn",
    requestTimeout: 30,
    maxConcurrentRequests: 5,
  },
}
