# AzureOpsCrew 🤖

**Hybrid team collaboration platform** — where AI agents and humans work together in a shared chat interface.

Build teams that combine human expertise with AI capabilities, all collaborating through natural conversation.

---

## ✨ What it does

Create **hybrid teams** where AI agents and humans:

- 💬 **Chat together** in shared channels and DMs — everyone participates in the same conversation
- 👥 **Collaborate seamlessly** — agents respond to humans, humans respond to agents, and agents coordinate with each other
- 🔧 **Execute tools** — MCP servers, message tools, backend integrations, triggered through chat
- ⚡ **Respond to events** — automated workflows that wake agents when needed
- 🧠 **Remember everything** — Neo4j long-term memory across conversations

**The key insight:** AI agents aren't isolated tools — they're teammates. AzureOpsCrew gives you a chat interface where multiple agents and multiple humans can all participate, creating truly hybrid teams.

---

## 🛠️ Tech Stack

| Layer     | Tech                           |
|-----------|--------------------------------|
| Backend   | .NET 10, ASP.NET Core, SignalR |
| Frontend  | Blazor WebAssembly             |
| Database  | SQL Server + Neo4j             |
| AI        | Azure Foundry, OpenAI          |

---

## 🎯 Key Features

- **Hybrid Team Chat** — Multiple AI agents and humans collaborating in the same conversation
- **Agent Management** — Create specialized agents with custom roles, prompts & tool access
- **Multi-Provider AI** — Switch between OpenAI, Azure OpenAI, and other providers
- **Real-time Messaging** — SignalR-powered live chat where agents and humans respond in real-time
- **Tool System** — Built-in tools + extensible MCP servers for agent capabilities
- **Channel System** — Organize conversations across different topics and teams
- **Auth** — JWT with email verification

---

## 🚀 Quick Start

```bash
# 1. Configure environment
cp .env.example .env
# Edit .env with your Azure OpenAI keys & JWT signing key

# 2. Run with Docker
docker-compose up

# 3. Access
# Frontend: http://localhost:42080
# API: http://localhost:42000
```

---

*Build hybrid teams where AI and humans work together as equals.*

*Built with ❤️ using .NET 10 & MAF (Microsoft Agents Framework)*
