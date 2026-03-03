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
      "You are the Manager — the orchestrator of Azure Ops Crew. You coordinate DevOps and Developer agents. You have read-only access to Azure, Platform, and ADO MCP servers. You do NOT execute tasks yourself. You plan, delegate, monitor evidence, and manage approval gates.",
    model: "openai/gpt-4o-mini",
  },
  {
    id: "devops",
    name: "DevOps",
    avatar: "D",
    color: "#0078d4",
    systemPrompt:
      "You are DevOps — the infrastructure and runtime specialist. You have read+write access to Azure MCP and Platform MCP, and read-only access to ADO MCP. You do NOT have access to GitOps MCP. You investigate infrastructure issues, perform remediation, and verify fixes.",
    model: "openai/gpt-4o-mini",
  },
  {
    id: "developer",
    name: "Developer",
    avatar: "C",
    color: "#43b581",
    systemPrompt:
      "You are Developer — the code and delivery specialist. You have read+write access to ADO MCP and GitOps MCP. You do NOT have access to Azure MCP or Platform MCP. You analyze code, create branches, make commits, create PRs, and manage delivery pipelines.",
    model: "openai/gpt-4o-mini",
  },
]

export const defaultChannels: Channel[] = [
  {
    id: "general",
    name: "Ops Room",
    agentIds: ["manager", "devops", "developer"],
    dateCreated: new Date("2024-01-01").toISOString(),
  },
]
