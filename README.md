# AzureOpsCrew 🤖

**AI-powered agent platform** for automating tasks through chat-based collaboration.

---

## ✨ What it does

Create AI agents that:
- 💬 Chat in channels & DMs
- 🔧 Execute tools (MCP servers, message tools, backend integrations)
- ⚡ Respond to triggers & approval workflows
- 🧠 Remember via Neo4j long-term memory

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

- **Agent Management** — Create agents with custom prompts & MCP server integrations
- **Multi-Provider AI** — Switch between OpenAI, Azure OpenAI, and more
- **Real-time Messaging** — SignalR-powered live chat with agents
- **Tool System** — Built-in tools + extensible MCP servers
- **Channel System** — Organize agents across channels
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

*Built with ❤️ using .NET 10 & MAF (Microsoft Agents Framework)*
