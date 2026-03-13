# 🚀 AzureOpsCrew

> **Agentic DevOps Platform** — A multi-agent system where AI agents and humans collaborate as teammates to automate software delivery workflows.

[![.NET](https://img.shields.io/badge/.NET-10-purple)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Azure](https://img.shields.io/badge/Azure-Deploy-blue)](https://azure.microsoft.com/)

![Demo](demo.png)

---

## 📑 Table of Contents

- [Overview](#overview)
- [What It Does](#what-it-does)
- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Development](#development)
- [Deploy to Azure](#deploy-to-azure)

---

## 📖 Overview

AzureOpsCrew is a **hybrid collaboration platform** where specialized AI agents 🤖 work alongside human developers 👨‍💻 in shared conversations. It automates CI/CD pipelines, incident response, and reliability engineering workflows through a chat-based interface where multiple agents and humans collaborate in real-time ⚡.

**The key insight:** AI agents aren't isolated tools — they're teammates 🤝. AzureOpsCrew creates a chat interface where multiple agents and multiple humans all participate together.

 ## Production-Grade AI Principles (long)

AzureOpsCrew implements enterprise-level multi-agent architecture from the ground up:

- **🎯 Intelligent Orchestration** — Trigger-based agent activation with signal coordination ensures agents respond to events, not polls. Multiple agents execute in parallel across conversations while maintaining sequential consistency within each chat.
- **🔒 Isolated Agent Context** — Every agent operates within its own memory scope and context window. No cross-agent data leakage — each agent reasons independently with its own facts, tools, and instructions.
- **🧠 Persistent Long-Term Memory** — Graph-based knowledge store that persists across sessions. Agents build and query their own memory — they learn, not just respond.
- **📐 Smart Context Management** — Automatic token-aware context windowing with adaptive truncation. The system keeps conversations sharp and relevant regardless of how long they run.
- **🛡️ Human-in-the-Loop Control** — Two-phase tool approval system lets users inspect, approve, or reject any agent action before execution. Full visibility into agent reasoning with real-time streaming of thinking/reasoning blocks.
- **🔌 Multi-Provider LLM Support** — Plug-and-play support for **OpenAI, Anthropic, Ollama, OpenRouter, and Azure Foundry**. Zero vendor lock-in — switch models per agent without changing a single line of code.
- **🧰 MCP Tool Ecosystem** — Connect any external tool via **Model Context Protocol** servers with granular per-tool approval policies. Agents share a unified tool layer while respecting individual access boundaries.
- **🧩 Composable Prompt Architecture** — Modular prompt chunks dynamically assembled based on agent role, conversation type, and channel context. Fully customizable agent personas, system prompts, and behavioral policies.

- - ## Production-Grade AI Principles(short)

AzureOpsCrew implements enterprise-level multi-agent architecture from the ground up:

- **🎯 Intelligent Orchestration** — Trigger-based activation, parallel execution across chats, sequential consistency within each.
- **🔒 Isolated Agent Context** — Each agent has its own memory scope, tools, and instructions. No cross-agent data leakage.
- **🧠 Persistent Long-Term Memory** — Graph-based knowledge store that persists across sessions. Agents learn, not just respond.
- **📐 Smart Context Management** — Token-aware windowing with adaptive truncation keeps conversations sharp regardless of length.
- **🛡️ Human-in-the-Loop Control** — Tool approval before execution. Real-time visibility into agent reasoning and thinking.
- **🔌 Multi-Provider LLM Support** — OpenAI, Anthropic, Ollama, OpenRouter, Azure Foundry. Switch models per agent, zero vendor lock-in.
- **🧰 MCP Tool Ecosystem** — Connect any tool via Model Context Protocol with per-tool approval policies.
- **🧩 Composable Prompt Architecture** — Modular prompt chunks assembled dynamically by agent role and context.

---

## ✨ What It Does

### Core Capabilities

| Feature | Description |
|---------|-------------|
| **💬 Real-time Collaboration** | SignalR-powered chat where agents and humans participate in the same conversation |
| **🎭 Agent Orchestration** | Create specialized agents with custom roles, prompts, and tool access |
| **⚙️ Workflow Automation** | Agents execute tools, respond to events, and coordinate with each other |
| **☁️ Azure Integration** | Native support for MCP servers, Azure services, and tool-based extensibility |
| **🧠 Long-term Memory** | Neo4j-powered memory that persists across conversations |

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

## 🚀 Quick Start (Docker compose)

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/products/docker-desktop)
- Azure OpenAI API key 🔑
- (Optional) Neo4j instance for long-term memory

### 1️⃣ Clone and Configure

```bash
git clone https://github.com/your-org/AzureOpsCrew.git
cd AzureOpsCrew
cp .env.example .env
```

### 2️⃣ Configure Environment Variables

#### Edit `.env` with your settings:

```bash
# API Configuration
API_BASE_URL=http://localhost:42000
API_PORT=42000

#Frontend Configuration
FRONTEND_PORT=42080

# Seeding
SEEDING_ENABLED=true
SEEDING_AZURE_OPENAI_API_ENDPOINT=https://your-resource.openai.azure.com/
SEEDING_AZURE_OPENAI_API_KEY=your-api-key
SEEDING_AZURE_OPENAI_API_DEFAULTMODEL=gpt-5-2-chat
SEEDING_USER_EMAIL=AzureOpsCrew@mail.xyz
SEEDING_USER_USERNAME=BossUSer
SEEDING_USER_PASSWORD=Pass1234

# Database
SQLSERVER_SA_PASSWORD=YourStrong@Password

# JWT
JWT_SIGNING_KEY=your-256-bit-secret-key

# Email Verification (optional)
EMAIL_VERIFICATION_ENABLED=false
BREVO_API_BASE_URL=https://api.brevo.com
BREVO_API_KEY=your-brevo-api-key
BREVO_SENDER_EMAIL=azureopscrew@aoc-app.com
BREVO_SENDER_NAME=Azure Ops Crew

# Long-term memory
LONG_TERM_MEMORY_TYPE=InMemory
```

| Variable | Description | Required |
|----------|-------------|----------|
| `API_BASE_URL` | API base URL | ❌ No |
| `API_PORT` | API port | ❌ No |
| `FRONTEND_PORT` | Frontend port | ❌ No |
| `SQLSERVER_SA_PASSWORD` | SQL Server SA password | ❌ No |
| `JWT_SIGNING_KEY` | JWT signing key (min 32 chars) | ✅ Yes |
| `SEEDING_ENABLED` | Enable database seeding | ✅ Yes(Quick Start recommended `true`) |
| `SEEDING_AZURE_OPENAI_API_ENDPOINT` | Azure OpenAI endpoint for Provider entity seeding | ⚠️ Yes if `SEEDING_ENABLED=true` |
| `SEEDING_AZURE_OPENAI_API_KEY` | Azure OpenAI API key for Provider entity seeding | ⚠️ Yes if `SEEDING_ENABLED=true` |
| `SEEDING_AZURE_OPENAI_API_DEFAULTMODEL` | Azure OpenAI default model name | ❌ No |
| `SEEDING_USER_EMAIL` | Seed user email | ❌ No |
| `SEEDING_USER_USERNAME` | Seed user username | ❌ No |
| `SEEDING_USER_PASSWORD` | Seed user password | ❌ No |
| `EMAIL_VERIFICATION_ENABLED` | Enable email verification | ✅ Yes(Quick Start recommended `false`) |
| `BREVO_API_KEY` | Brevo API key for email sending | ⚠️ Yes if `EMAIL_VERIFICATION_ENABLED=true` |
| `BREVO_API_BASE_URL` | Brevo API base URL | ⚠️ Yes if `EMAIL_VERIFICATION_ENABLED=true` |
| `BREVO_SENDER_EMAIL` | Brevo sender email | ⚠️ Yes if `EMAIL_VERIFICATION_ENABLED=true` |
| `BREVO_SENDER_NAME` | Brevo sender name | ⚠️ Yes if `EMAIL_VERIFICATION_ENABLED=true` |
| `LONG_TERM_MEMORY_TYPE` | Memory type (`None` or `InMemory` or `Neo4j`) | ✅ Yes(Quick Start recommended `None`/`InMemory`) |
| `NEO4J_URI` | Neo4j connection URI | ⚠️ Yes if `LONG_TERM_MEMORY_TYPE=Neo4j` |
| `NEO4J_USERNAME` | Neo4j username | ⚠️ Yes if `LONG_TERM_MEMORY_TYPE=Neo4j` |
| `NEO4J_PASSWORD` | Neo4j password | ⚠️ Yes if `LONG_TERM_MEMORY_TYPE=Neo4j` |

See `appsettings.json` files for additional configuration:
- Database provider selection 🗄️
- JWT authentication settings 🔐
- Email verification configuration 📧
- Azure Foundry seed settings 🌱
- Long-term memory configuration 🧠

### 3️⃣ Run with Docker

```bash
docker-compose up -d
```

Services will be available at:
- **Frontend**: http://localhost:42080
- **API**: http://localhost:42000

---

## 🚀 Quick Start (Locally)

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (LocalDB, Express, or Developer edition) 🗄️
- Azure OpenAI API key 🔑
- (Optional) [Neo4j](https://neo4j.com/download/) instance for long-term memory 🧠

### 1️⃣ Clone and Configure

```bash
git clone https://github.com/your-org/AzureOpsCrew.git
cd AzureOpsCrew
cp .env.example .env
```

### 2️⃣ Configure minimal User Secrets

#### API

```json
{
  "SqlServer": {
    "ConnectionString": "Server=localhost;Database=AzureOpsCrew;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "SigningKey": "",
  },
  "EmailVerification": {
    "IsEnabled": false,
  },
  "Brevo": {
    "ApiKey": "",
    "ApiBaseUrl": "https://api.brevo.com",
    "SenderEmail": "azureopscrew@aoc-app.com",
    "SenderName": "Azure Ops Crew"
  },
  "Seeding": {
    "IsEnabled": true,
    "AzureFoundrySeed": {
      "ApiEndpoint": "",
      "Key": "",
      "DefaultModel": "gpt-5-2-chat"
    },
    "UserSeed": {
      "Email": "AzureOpsCrew@mail.xyz",
      "Username": "BossUser",
      "Password": "Pass1234"
    }
  },
  "LongTermMemory": {
    "Type": "InMemory"
  }
}
```

| Setting | Description | Required |
|---------|-------------|----------|
| `SqlServer:ConnectionString` | SQL Server connection string | ✅ Yes |
| `Jwt:SigningKey` | JWT signing key (min 32 chars) | ✅ Yes |
| `Seeding:IsEnabled` | Enable database seeding | ✅ Yes(Quick Start recommended `true`) |
| `Seeding:AzureFoundrySeed:ApiEndpoint` | Azure OpenAI endpoint for Provider entity seeding | ⚠️ Yes if `Seeding:IsEnabled=true` |
| `Seeding:AzureFoundrySeed:Key` | Azure OpenAI API key for Provider entity seeding | ⚠️ Yes if `Seeding:IsEnabled=true` |
| `Seeding:AzureFoundrySeed:DefaultModel` | Azure OpenAI default model name | ❌ No |
| `Seeding:UserSeed:Email` | Seed user email | ❌ No |
| `Seeding:UserSeed:Username` | Seed user username | ❌ No |
| `Seeding:UserSeed:Password` | Seed user password | ❌ No |
| `EmailVerification:IsEnabled` | Enable email verification | ✅ Yes(Quick Start recommended `false`) |
| `Brevo:ApiKey` | Brevo API key for email sending | ⚠️ Yes if `EmailVerification:IsEnabled=true` |
| `Brevo:ApiBaseUrl` | Brevo API base URL | ⚠️ Yes if `EmailVerification:IsEnabled=true` |
| `Brevo:SenderEmail` | Brevo sender email | ⚠️ Yes if `EmailVerification:IsEnabled=true` |
| `Brevo:SenderName` | Brevo sender name | ⚠️ Yes if `EmailVerification:IsEnabled=true` |
| `LongTermMemory:Type` | Memory type (`None` or `InMemory` or `Neo4j`) | ✅ Yes(Quick Start recommended `None`/`InMemory`) |
| `LongTermMemory:Neo4j:Uri` | Neo4j connection URI | ⚠️ Yes if `LongTermMemory:Type=Neo4j` |
| `LongTermMemory:Neo4j:Username` | Neo4j username | ⚠️ Yes if `LongTermMemory:Type=Neo4j` |
| `LongTermMemory:Neo4j:Password` | Neo4j password | ⚠️ Yes if `LongTermMemory:Type=Neo4j` |

See `appsettings.json` for additional configuration:
- Database provider selection 🗄️
- JWT authentication settings 🔐
- Email verification configuration 📧
- Azure Foundry seed settings 🌱
- Long-term memory configuration 🧠
 
#### Frontend
appsettings.json
```json
{
  "ApiBaseUrl": ""
}
```

### 3️⃣ Run Locally (Development)

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

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Api.Tests
dotnet test tests/Infrastructure.Ai.Tests
```

### Database Migrations

```bash
cd src/Infrastructure.Db
dotnet ef migrations add MigrationName
dotnet ef database update
```

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
