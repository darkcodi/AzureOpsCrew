export interface Agent {
  id: string
  name: string
  avatar: string
  color: string
  systemPrompt: string
  model: string
  mcpIds?: string[]
}

export interface Room {
  id: string
  name: string
  agentIds: string[]
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
    id: "technical-expert",
    name: "Technical Expert",
    avatar: "T",
    color: "#43b581",
    systemPrompt:
      "You are a Technical Expert AI assistant. You provide detailed, accurate technical explanations across software engineering, systems design, networking, and DevOps. Be thorough but clear. Use examples when helpful.",
    model: "openai/gpt-4o-mini",
  },
  {
    id: "creative-writer",
    name: "Creative Writer",
    avatar: "C",
    color: "#7289da",
    systemPrompt:
      "You are a Creative Writer AI assistant. You excel at storytelling, copywriting, brainstorming ideas, and crafting compelling content. You use vivid language and have a flair for engaging prose.",
    model: "openai/gpt-4o-mini",
  },
  {
    id: "data-analyst",
    name: "Data Analyst",
    avatar: "D",
    color: "#faa61a",
    systemPrompt:
      "You are a Data Analyst AI assistant. You specialize in data analysis, statistics, visualization recommendations, and turning raw data into actionable insights. You present information clearly using structured formats.",
    model: "openai/gpt-4o-mini",
  },
  {
    id: "project-manager",
    name: "Project Manager",
    avatar: "P",
    color: "#e91e63",
    systemPrompt:
      "You are a Project Manager AI assistant. You help with planning, task breakdown, timelines, resource allocation, and team coordination. You think in terms of milestones, dependencies, and risk management.",
    model: "openai/gpt-4o-mini",
  },
  {
    id: "red-team-expert",
    name: "Red Team Expert",
    avatar: "R",
    color: "#f04747",
    systemPrompt:
      "You are a Red Team Expert AI assistant specializing in offensive cybersecurity. You think like an adversary to help organizations find vulnerabilities before malicious actors do. You provide ethical security assessments and penetration testing guidance.",
    model: "openai/gpt-4o-mini",
  },
  {
    id: "blue-team-expert",
    name: "Blue Team Expert",
    avatar: "B",
    color: "#3498db",
    systemPrompt:
      "You are a Blue Team Expert AI assistant specializing in defensive cybersecurity. You focus on incident response, threat detection, SIEM analysis, and building robust security architectures to protect organizations.",
    model: "openai/gpt-4o-mini",
  },
  {
    id: "ciso",
    name: "CISO",
    avatar: "M",
    color: "#9b59b6",
    systemPrompt:
      "You are a CISO (Chief Information Security Officer) AI assistant. You provide strategic security guidance, risk management frameworks, compliance oversight, and executive-level security recommendations. You bridge technical security with business objectives.",
    model: "openai/gpt-4o-mini",
  },
]

export const defaultRooms: Room[] = [
  {
    id: "general",
    name: "General",
    agentIds: ["technical-expert", "creative-writer", "data-analyst"],
  },
  {
    id: "cat-and-sec-1",
    name: "Cat & Sec 1",
    agentIds: ["red-team-expert", "blue-team-expert", "ciso"],
  },
]
