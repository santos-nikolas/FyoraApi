
# Fyora API (ASP.NET Core 8 + EF Core + Swagger + Gemini)

Uma Web API em **ASP.NET Core 8** com **Entity Framework Core (SQLite)**, **Swagger**, **LINQ** e integração com **Google Gemini (REST)** para o app **Fyora** — uma solução de impacto social que apoia pessoas em luta com jogo problemático.

> **Status da Entrega (Rubrica da Sprint)**  
> - ✅ CRUD completo com Entity Framework (**35%**)  
> - ✅ Pesquisas com LINQ (**10%**)  
> - ✅ Endpoints conectando com API externa (Gemini) (**20%**)  
> - ✅ Documentação do projeto (**10%**) **→ este README**  
> - ✅ Arquitetura em diagramas (**10%**) **→ ver seção Diagramas**  
> - ⏳ Publicação em ambiente Cloud (Azure) (**15%**) **→ ver seção _Publicação em Azure_ (passo a passo).**  
>
> **Ações pendentes do aluno para fechar a nota:** publicar no Azure e entregar o(s) links de acesso (API + plano/projeto na nuvem + repositório).

---

## Sumário
- [Arquitetura (visão geral)](#arquitetura-visão-geral)
- [Stack & Requisitos](#stack--requisitos)
- [Configuração](#configuração)
- [Execução local](#execução-local)
- [Como testar os endpoints (Users & ProgressLogs)](#como-testar-os-endpoints-users--progresslogs)
- [Endpoints principais](#endpoints-principais)
- [Consultas LINQ implementadas](#consultas-linq-implementadas)
- [Integração com Google Gemini (REST)](#integração-com-google-gemini-rest)
- [CORS e HTTPS](#cors-e-https)
- [Publicação em Azure (passo a passo)](#publicação-em-azure-passo-a-passo)
- [Diagramas (C4 + Sequência)](#diagramas-c4--sequência)
- [Estrutura de pastas](#estrutura-de-pastas)
- [Checklist da Rubrica](#checklist-da-rubrica)
- [Licença](#licença)

---

## Arquitetura (visão geral)

- **ASP.NET Core 8** com Controllers (Web API)
- **EF Core + SQLite** (arquivo local `fyora_api.db`) para persistência simples
- **Swagger/OpenAPI** para documentação e testes
- **ChatController** → chama **Google Gemini (REST)** via `HttpClient`
- **UsersController / ProgressLogs** → CRUD + consultas com LINQ
- **CORS** e **HTTPS** configuráveis para desenvolvimento/produção

---

## Stack & Requisitos

- .NET SDK **8.0+**
- ASP.NET Core Web API
- Entity Framework Core (**Microsoft.EntityFrameworkCore.Sqlite**)
- Swagger (**Swashbuckle.AspNetCore**)
- Google Gemini (REST) — **Generative Language API**
- (Opcional) Azure CLI / Visual Studio 2022 para publicação

---

## Configuração

### AppSettings
Arquivo `appsettings.json` (dev) contém:
```json
{
  "ConnectionStrings": { "DefaultConnection": "Data Source=fyora_api.db" },
  "Gemini": {
    "ApiKey": "SUA_CHAVE_DO_AI_STUDIO_AQUI",
    "Model": "gemini-2.0-flash",
    "MaxTokens": 512,
    "Temperature": 0.7
  },
  "Cors": {
    "AllowedOrigins": [
      "https://localhost:7219",
      "http://localhost:5200",
      "http://localhost:3000"
    ]
  }
}
```

> **Recomendado**: **NÃO** versionar a API key. Use **User Secrets** em dev e **App Settings** no Azure em prod:
```bash
# na pasta do .csproj
dotnet user-secrets set "Gemini:ApiKey" "SUA_CHAVE_REAL_DO_AI_STUDIO"
```
Também é suportada a variável de ambiente `GEMINI_API_KEY`.

---

## Execução local

```bash
dotnet restore
dotnet build
dotnet dev-certs https --trust
dotnet run
```
- Swagger: **https://localhost:7219/swagger**
- O banco SQLite é criado automaticamente (`EnsureCreated`).

### Testes rápidos (cURL)
```bash
# Healthcheck
curl -k https://localhost:7219/health

# Ping Gemini
curl -k https://localhost:7219/api/Chat/ping

# Chat
curl -k -X POST https://localhost:7219/api/Chat/ask \
  -H "Content-Type: application/json" \
  -d "{\"message\":\"Estou no dia 7 sem apostar e ansioso pelo fim de semana. Alguma dica?\"}"
```

---

## Como testar os endpoints (Users & ProgressLogs)

### Pelo Swagger (recomendado)
Acesse **https://localhost:7219/swagger** e execute na ordem:

1. **Criar um usuário** — `POST /api/Users`
   ```json
   {
     "nickname": "Ana",
     "email": "ana@example.com"
   }
   ```

2. **Listar usuários** (com **LINQ** de busca opcional) — `GET /api/Users`  
   - `GET /api/Users` → lista tudo  
   - `GET /api/Users?search=ana` → filtra por nickname (case-insensitive)

3. **Buscar por Id** — `GET /api/Users/{id}`

4. **Atualizar** — `PUT /api/Users/{id}`  
   ```json
   {
     "id": 1,
     "nickname": "Ana Clara",
     "email": "ana.clara@example.com",
     "createdAt": "2025-10-17T00:00:00Z"
   }
   ```

5. **Remover** — `DELETE /api/Users/{id}`

6. **Criar log de progresso (relacionado a um usuário)** — `POST /api/Users/{userId}/progress`  
   ```json
   {
     "daysWithoutGambling": 7,
     "achievement": "1 semana sem apostar 🎉"
   }
   ```

> Dica: depois, confira os logs do usuário (endpoint de listagem de logs, se houver) ou consulte pelo próprio usuário/SQLite.

### Via cURL
```bash
# Criar usuário
curl -k -X POST https://localhost:7219/api/Users \
  -H "Content-Type: application/json" \
  -d "{\"nickname\":\"Ana\",\"email\":\"ana@example.com\"}"

# Listar com busca
curl -k "https://localhost:7219/api/Users?search=ana"

# Buscar por Id
curl -k https://localhost:7219/api/Users/1

# Atualizar
curl -k -X PUT https://localhost:7219/api/Users/1 \
  -H "Content-Type: application/json" \
  -d "{\"id\":1,\"nickname\":\"Ana Clara\",\"email\":\"ana.clara@example.com\",\"createdAt\":\"2025-10-17T00:00:00Z\"}"

# Remover
curl -k -X DELETE https://localhost:7219/api/Users/1

# Criar log de progresso (userId = 1)
curl -k -X POST https://localhost:7219/api/Users/1/progress \
  -H "Content-Type: application/json" \
  -d "{\"daysWithoutGambling\":7,\"achievement\":\"1 semana sem apostar 🎉\"}"
```

### Via PowerShell
```powershell
# Criar usuário
Invoke-RestMethod -Uri "https://localhost:7219/api/Users" -Method Post `
  -Headers @{ "Content-Type"="application/json" } `
  -Body '{"nickname":"Ana","email":"ana@example.com"}'

# Listar com busca
Invoke-RestMethod -Uri "https://localhost:7219/api/Users?search=ana" -Method Get

# Criar log de progresso
Invoke-RestMethod -Uri "https://localhost:7219/api/Users/1/progress" -Method Post `
  -Headers @{ "Content-Type"="application/json" } `
  -Body '{"daysWithoutGambling":7,"achievement":"1 semana sem apostar 🎉"}'
```

**Erros comuns**
- `400 BadRequest`: campos obrigatórios ausentes/formatos inválidos.
- `404 NotFound`: Id inexistente.
- Certificado HTTPS em dev: rode `dotnet dev-certs https --trust`.

---

## Endpoints principais

### Chat (Gemini)
- `GET /api/Chat/ping` — teste de conectividade/credencial
- `POST /api/Chat/ask` — recebe `{ "message": "..." }` e retorna texto motivacional breve

### Usuários / Progresso
- `GET /api/Users` — lista usuários
- `GET /api/Users/{id}` — detalhe
- `POST /api/Users` — cria
- `PUT /api/Users/{id}` — atualiza
- `DELETE /api/Users/{id}` — remove

- `GET /api/ProgressLogs` — lista logs
- `POST /api/ProgressLogs` — cria log para um usuário existente

> **Swagger** expõe toda a coleção.

---

## Consultas LINQ implementadas

- **Filtro de usuários por `Nickname`** (contém; case-insensitive)
  ```csharp
  // GET /api/Users?search=ana
  var query = _ctx.Users.AsQueryable();
  if (!string.IsNullOrWhiteSpace(search))
      query = query.Where(u => u.Nickname.ToLower().Contains(search.ToLower()));
  var result = await query.OrderBy(u => u.Nickname).ToListAsync();
  ```

- **Logs de um usuário ordenados por data desc**
  ```csharp
  var logs = await _ctx.ProgressLogs
      .Where(p => p.UserId == userId)
      .OrderByDescending(p => p.LogDate)
      .ToListAsync();
  ```

---

## Integração com Google Gemini (REST)

- Modelo padrão: **`gemini-2.0-flash`** (compatível com `v1beta`)
- Chamada via `HttpClient` para `.../v1beta/models/{model}:generateContent?key=...`
- Payload: `contents[ { role: "user", parts: [ { text } ] } ]`
- Extração de resposta: `candidates[0].content.parts[0].text`

> **Atenção:** `safetySettings` tem categorias específicas no `v1beta`. Mantivemos **sem safety em Dev** e **moderada em Prod** (apenas categorias suportadas).

---

## CORS e HTTPS

- **Dev**: HTTPS habilitado (`dotnet dev-certs https --trust`).  
- Se for consumir a API a partir de um **frontend externo** (ex.: `http://localhost:3000`), adicione essa origem em `Cors:AllowedOrigins`.

---

## Publicação em Azure (passo a passo)

> **Observação sobre banco**: Em **Azure App Service Linux**, o diretório `/home` é **persistente**. Para um demo simples com SQLite, você pode apontar o caminho do DB para `/home/site/wwwroot/app_data/fyora_api.db`. Para produção, o ideal é migrar para **Azure SQL**/**PostgreSQL**.

### A) App Service (via Visual Studio)
1. **Build** em `Release`.
2. Clique direito no projeto → **Publish** → **Azure** → **Azure App Service (Linux)** → Create New.
3. Selecione **.NET 8 (LTS)**.
4. Em **Settings** do recurso criado:
   - **Configuration → Application settings**:
     - `Gemini:ApiKey` = `***` (ou `GEMINI_API_KEY`)
     - `ASPNETCORE_ENVIRONMENT` = `Production`
     - (Opcional) `ConnectionStrings:DefaultConnection` = `Data Source=/home/site/wwwroot/app_data/fyora_api.db`
   - **General settings**: Arrumar `WEBSITE_RUN_FROM_PACKAGE` (padrão) e `Always On` (se disponível).
5. **Deploy** e acesse `https://<seuapp>.azurewebsites.net/swagger`.

### B) App Service (via GitHub Actions)
- Crie repositório e push do código.
- No Azure Portal, **Deployment Center** → **GitHub** → selecione o repositório → **.NET 8**.
- Adicione **secrets** no Azure (mesmos da opção A). O workflow do GitHub fará o build e deploy a cada push.

### C) Azure SQL (opcional — recomendado p/ produção)
1. Crie um **Azure SQL Database** e pegue a string de conexão (ADO.NET).  
2. No projeto, adicione pacote `Microsoft.EntityFrameworkCore.SqlServer`.  
3. Troque o provider em `Program.cs` para `UseSqlServer(...)`.  
4. Rode migrações:
   ```bash
   dotnet tool install --global dotnet-ef
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```
5. Configure a connection string no Azure **(ConnectionStrings:DefaultConnection)**.

---

## Diagramas (C4 + Sequência)

<img width="423" height="650" alt="C4-Container" src="https://github.com/user-attachments/assets/16b08a76-f53a-45d5-919d-397f11550397" />

### C4 - Container (PlantUML)
<img width="810" height="365" alt="Sequência — `POST apiChatask`" src="https://github.com/user-attachments/assets/f783b445-aa40-48f7-8db9-0480e58ca666" />

---

## Estrutura de pastas

```
FyoraApi/
├─ Controllers/
│  ├─ ChatController.cs
│  ├─ UsersController.cs
│  └─ ProgressLogsController.cs
├─ Data/
│  └─ FyoraContext.cs
├─ Models/
│  ├─ User.cs
│  └─ ProgressLog.cs
├─ DTOs/
│  └─ CreateProgressLogDto.cs
├─ Properties/
│  └─ launchSettings.json
├─ Program.cs
├─ appsettings.json
├─ c4_container.puml
├─ sequence_chat_ask.puml
└─ README.md
```

---

## Checklist da Rubrica

| Critério | Status | Observações |
|---|---|---|
| CRUD completo com EF Core | ✅ | Users e ProgressLogs com endpoints CRUD (+ EnsureCreated) |
| Pesquisas com LINQ | ✅ | Filtro por nickname, ordenações, consultas por usuário |
| Publicação em Cloud | ⏳ | Seguir **Publicação em Azure** e anexar links (API e Portal/Plano) |
| Endpoint externo (APIs) | ✅ | Gemini REST (`/api/Chat/ask`, `/api/Chat/ping`) |
| Documentação do projeto | ✅ | Este README + Swagger |
| Arquitetura em diagramas | ✅ | C4 Container + Sequência (PlantUML) |
| Versionador (repositório) | ⏳ | Subir ao GitHub/DevOps e fornecer link |
| Link do plano/projeto na nuvem | ⏳ | Fornecer acesso ao professor (Portal/Subscription/Resource Group/App Service) |

---

## Licença

Projeto acadêmico (FIAP). Uso apenas educacional, sem garantias.
