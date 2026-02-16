export interface Agent {
  id: string
  name: string
  avatar: string
  color: string
  systemPrompt: string
  model: string
  mcpIds?: string[]
  dateCreated?: string
  /** Status from backend; when absent, UI shows "Idle". */
  status?: string
}

export interface Channel {
  id: string
  name: string
  agentIds: string[]
  dateCreated?: string
}

export interface ChatMessage {
  id: string
  role: "user" | "assistant"
  content: string
  agentId?: string
  timestamp: Date
}

export const defaultAgents: Agent[] = [
  {
    id: "manager",
    name: "Manager",
    avatar: "M",
    color: "#9b59b6",
    systemPrompt:
      "You are a Manager AI assistant. You help with planning, priorities, resource allocation, team coordination, and delivery. You think in terms of goals, milestones, risks, and stakeholder communication. Keep answers actionable and concise.",
    model: "openai/gpt-4o-mini",
  },
  {
    id: "azure-devops",
    name: "Azure DevOps",
    avatar: "D",
    color: "#0078d4",
    systemPrompt:
      "You are an Azure DevOps expert. You help with pipelines (YAML and classic), CI/CD, Azure Repos, Boards, Artifacts, Test Plans, and release management. You know branching strategies, approvals, variable groups, service connections, and Azure DevOps REST APIs. Give concrete, step-by-step guidance when asked.",
    model: "openai/gpt-4o-mini",
  },
  {
    id: "azure-dev",
    name: "Azure Dev",
    avatar: "A",
    color: "#43b581",
    systemPrompt:
      "You are an Azure development expert. You help with building and deploying apps on Azure: App Service, Functions, Container Apps, AKS, Azure SDKs, identity (Microsoft Entra ID), storage, messaging, and serverless. You focus on code, configuration, and best practices for Azure-native development.",
    model: "openai/gpt-4o-mini",
  },
]

export const defaultChannels: Channel[] = [
  {
    id: "general",
    name: "General",
    agentIds: ["manager", "azure-devops"],
    dateCreated: new Date("2024-01-01").toISOString(),
  },
]
