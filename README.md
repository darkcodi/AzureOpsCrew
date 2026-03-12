# AzureOpsCrew 🤖

**Agentic DevOps Platform** 🚀 — Multi-agent system for automating software delivery workflows, where AI agents and humans collaborate as teammates.

---

## About 💭

AzureOpsCrew is a **hybrid collaboration platform** where specialized AI agents work alongside human developers in shared conversations — automating CI/CD, incident response, and reliability engineering workflows.

---

## What It Does 🎯

Create hybrid teams where AI agents and humans:

- **Collaborate in real-time** 💬 — SignalR-powered chat where agents and humans participate in the same conversation
- **Automate DevOps workflows** 🔄 — Agents execute tools, respond to events, and coordinate with each other
- **Orchestrate multi-agent systems** 🤖 — Specialized agents with custom roles, prompts, and tool access
- **Integrate with Azure** ☁️ — MCP servers, Azure services, and tool-based extensibility
- **Remember everything** 🧠 — Neo4j long-term memory across conversations

**The key insight:** AI agents aren't isolated tools — they're teammates. AzureOpsCrew creates a chat interface where multiple agents and multiple humans all participate together. 🤝

---

## Tech Stack 🛠️

| Layer | Tech |
|-------|------|
| Backend | .NET 10, ASP.NET Core, SignalR |
| Frontend | Blazor WebAssembly 🌐 |
| AI | **Microsoft Foundry**, Azure OpenAI |
| Agent Framework | **Microsoft Agents Framework (MAF)** 🤖 |
| Database | SQL Server + Neo4j |
| Deployment | **Azure** (Azure App Service, Azure SQL) ☁️ |
| Development | **GitHub + VS Code + GitHub Copilot** |

---

## Key Features ✨

- **Hybrid Team Chat** 👥 — Multiple AI agents and humans collaborating in the same conversation
- **Agent Management** ⚙️ — Create specialized agents with custom roles, prompts & tool access
- **Multi-Provider AI** 🎛️ — Switch between providers via Microsoft Foundry
- **Real-time Messaging** ⚡ — SignalR-powered live chat with streaming responses
- **Tool System** 🔧 — Built-in tools + extensible MCP servers
- **Channel System** 📢 — Organize conversations across topics and teams
- **JWT Auth** 🔐 — Secure authentication with email verification

---

## Quick Start 🚀

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

## Deploy to Azure ☁️

This project is designed for Azure deployment:

- Azure App Service for hosting 🖥️
- Azure SQL for relational data
- Azure OpenAI for AI capabilities
- Microsoft Foundry for agent orchestration 🤖

---

*Built with ❤️ using .NET 10, Microsoft Foundry & Microsoft Agent Framework* ✨
