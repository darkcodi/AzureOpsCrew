# 🚀 AzureOpsCrew

> **Agentic DevOps Platform** — A multi-agent system where AI agents and humans collaborate as teammates to automate software delivery workflows.

[![.NET](https://img.shields.io/badge/.NET-10-purple)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Azure](https://img.shields.io/badge/Azure-Deploy-blue)](https://azure.microsoft.com/)

![Demo](demo.png)

---

## 📑 Table of Contents

- [Overview](#overview)
- [Production-Grade AI Principles](#production-grade-ai-principles)
- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Quick Start (Docker compose)](#quick-start-docker-compose)
- [Quick Start (Locally)](#quick-start-locally)
- [Development](#development)
- [Deploy to Azure](#deploy-to-azure)

---

## 📖 Overview

AzureOpsCrew is a **hybrid collaboration platform** where specialized AI agents 🤖 work alongside human developers 👨‍💻 in shared conversations. It automates CI/CD pipelines, incident response, and reliability engineering workflows through a chat-based interface where multiple agents and humans collaborate in real-time ⚡.

**The key insight:** AI agents aren't isolated tools — they're teammates 🤝. AzureOpsCrew creates a chat interface where multiple agents and multiple humans all participate together.

## Production-Grade AI Principles

AzureOpsCrew implements enterprise-level multi-agent architecture from the ground up:

- **🎯 Intelligent Orchestration** — Trigger-based activation, parallel execution across chats, sequential consistency within each.
- **🔒 Isolated Agent Context** — Each agent has its own memory scope, tools, and instructions. No cross-agent data leakage.
- **🧠 Persistent Long-Term Memory** — Graph-based knowledge store that persists across sessions. Agents learn, not just respond.
- **📐 Smart Context Management** — Token-aware windowing with adaptive truncation keeps conversations sharp regardless of length.
- **🛡️ Human-in-the-Loop Control** — Tool approval before execution. Real-time visibility into agent reasoning and thinking.
- **🔌 Multi-Provider LLM Support** — OpenAI, Anthropic, Ollama, OpenRouter, Azure Foundry. Switch models per agent, zero vendor lock-in.
- **🧰 MCP Tool Ecosystem** — Connect any tool via Model Context Protocol with per-tool approval policies.
- **🧩 Composable Prompt Architecture** — Modular prompt chunks assembled dynamically by agent role and context.
- **💬 Real-time Collaboration** — SignalR-powered chat where agents and humans participate in the same conversation.
- **☁️ Azure-Native Integration** — Engineered to natively run on Azure. Leverages Azure Foundry for model orchestration, Azure OpenAI for inference, Azure SQL for persistence, and Azure App Service for deployment. No glue code, no adapters — just native Azure SDK integration end-to-end.
---

## 🏗️ Architecture

AzureOpsCrew follows **Clean Architecture** principles with Domain-Driven Design (DDD):

```
┌─────────────────────────────────────────────────────────────┐
│                        Front (Blazor)                       │
│                    Blazor WebAssembly UI                    │
└─────────────────────────────┬───────────────────────────────┘
                              │ SignalR + HTTP
┌─────────────────────────────▼───────────────────────────────┐
│                         API Layer                           │
│              ASP.NET Core Web API + SignalR                 │
├─────────────────────────────────────────────────────────────┤
│                    Application Services                     │
│  Channels │ Chats │ Agents │ Users │ Settings │ Auth        │
├─────────────────────────────┬───────────────────────────────┤
│         Domain Layer        │      Infrastructure           │
│   (Entities & Interfaces)   │  AI │ DB │ MCP │ Email        │
└─────────────────────────────┴───────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
    Azure OpenAI          SQL Server           Neo4j
    Microsoft Foundry    (App Data)        (Long-term Memory)
```

---

## 🛠️ Tech Stack

| Layer | Technology |
|-------|------------|
| **⚙️ Backend** | .NET 10, ASP.NET Core, SignalR, Serilog |
| **🎨 Frontend** | Blazor WebAssembly, Nginx |
| **🤖 AI/Agents** | Microsoft Foundry, Azure OpenAI, Microsoft Agents Framework (MAF) |
| **🗄️ Databases** | SQL Server (primary), Neo4j (memory) |
| **🚀 Deployment** | Azure App Service, Azure SQL, Docker |
| **🔐 Authentication** | JWT with email verification |

---

## 📁 Project Structure

```
AzureOpsCrew/
├── src/
│   ├── Api/                      # ASP.NET Core Web API backend
│   ├── Domain/                   # Domain models and interfaces (DDD)
│   ├── Front/                    # Blazor WebAssembly frontend
│   ├── Infrastructure.Ai/        # AI infrastructure (MCP, AI clients, Long-term memory)
│   └── Infrastructure.Db/        # Database infrastructure (EF Core)
├── tests/
│   ├── Api.Tests/                # API integration tests
│   └── Infrastructure.Ai.Tests/  # AI infrastructure tests
├── .env.example                  # Environment variables template
├── docker-compose.yml            # Container orchestration
└── README.md                     # This file
```

### Key Modules

- **🔌 Api** — Main backend server with authentication, channels, chats, and agents endpoints
- **🎨 Front** — Single-page application with real-time chat interface
- **🏛️ Domain** — Core business entities (Agents, Channels, Chats, Messages, MCP Servers)
- **🤖 Infrastructure.Ai** — MCP servers, AI client abstractions, provider facades
- **💾 Infrastructure.Db** — Entity Framework context and migrations

---

## 🚀 Quick Start

### Clone and Configure

```bash
git clone https://github.com/your-org/AzureOpsCrew.git
cd AzureOpsCrew
cp .env.example .env
```

### Option 1: Run with Docker compose (Recommended)

```bash
docker-compose up -d
```

Services will be available at:
- **Frontend**: http://localhost:42080
- **API**: http://localhost:42000

### Option 2: Run Locally

```bash
# Install dependencies
dotnet restore

# Run API
cd src/Api
dotnet run

# Run Frontend (in another terminal)
cd src/Front
dotnet run
```

---

## 💻 Development

### Code Style

The project follows C# coding conventions with:
- Clean Architecture principles 🏛️
- Domain-Driven Design patterns 🎯
- Async/await for I/O operations ⏳
- Dependency Injection throughout 💉

---

## ☁️ Deploy to Azure

### Azure Resources

- **Azure App Service** — Hosting for API and Frontend 🌐
- **Azure SQL Database** — Relational data storage 🗄️
- **Azure OpenAI** — AI model hosting 🧠
- **Azure Container Instances** — For Neo4j (optional) 🐳

### Deployment Steps

1. **Create resources** via Azure Portal or Terraform 🏗️
2. **Configure environment** variables in App Service ⚙️
3. **Deploy API** 🚀:
   ```bash
   az webapp up --name azureopscrew-api --resource-group YourRG
   ```
4. **Deploy Frontend** 🎨:
   ```bash
   cd src/Front
   dotnet publish -c Release
   az webapp up --name azureopscrew-web --resource-group YourRG
   ```

---

*Built with .NET 10, Microsoft Foundry & Microsoft Agent Framework* ✨
