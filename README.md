# Fyora API (ASP.NET Core 8 + EF Core + Swagger + Gemini + UI)

Web API do **Fyora**, projeto acadêmico com impacto social, que apoia pessoas em luta com **jogo problemático**.  
Stack: **ASP.NET Core 8**, **Entity Framework Core (SQLite)**, **Swagger/OpenAPI**, **Google Gemini (REST)** e **UI estática** em `wwwroot/` para testar os endpoints.

> **Status da Entrega (Rubrica da Sprint)**  
> - ✅ CRUD completo com Entity Framework (**35%**)  
> - ✅ Pesquisas com LINQ (**10%**)  
> - ✅ Endpoints conectando com API externa (Gemini) (**20%**)  
> - ✅ Documentação do projeto (**10%**) **→ este README**  
> - ✅ Arquitetura em diagramas (**10%**) **→ ver seção _Diagramas_**  
> - ✅ Publicação em ambiente Cloud (Azure) (**15%**) **→ ver seção _Publicação em Azure_**  

---

## Sumário
- [Arquitetura (visão geral)](#arquitetura-visão-geral)
- [Endpoints](#endpoints)
- [UI embutida (wwwroot)](#ui-embutida-wwwroot)
- [Configuração (dev e prod)](#configuração-dev-e-prod)
- [Execução local](#execução-local)
- [Testes rápidos (cURL / Swagger)](#testes-rápidos-curl--swagger)
- [Publicação em Azure](#publicação-em-azure)
- [Troubleshooting (erros comuns)](#troubleshooting-erros-comuns)
- [Consultas LINQ implementadas](#consultas-linq-implementadas)
- [Estrutura de pastas](#estrutura-de-pastas)
- [Diagramas](#diagramas)
- [Checklist da Rubrica](#checklist-da-rubrica)
- [Licença](#licença)

---

## Arquitetura (visão geral)

- **ASP.NET Core 8** com Controllers (Web API).
- **EF Core + SQLite** para persistência: banco é criado no primeiro run (`EnsureCreated`).  
  - **Local:** `Data Source=fyora_api.db` (raiz do projeto)  
  - **Azure:** `Data Source=/home/site/wwwroot/app_data/fyora_api.db`
- **Swagger/OpenAPI** para documentação e testes (`/swagger`).
- **ChatController** → integra com **Google Gemini** (REST via `HttpClient`).
- **UI estática** em `wwwroot/index.html` (tema **Fênix** 🔥) para testar: Chat + CRUD Usuários + ProgressLogs.

**Pontos importantes do `Program.cs`:**
- `UseDefaultFiles` + `UseStaticFiles` → **`/` abre o `index.html`**.
- Swagger JSON sempre; **UI em Dev** ou quando **`Swagger__Enabled=true`** (App Setting).
- `HttpClient` nomeado `"gemini"` com base `https://generativelanguage.googleapis.com/`.
- CORS opcional por `Cors:AllowedOrigins`.
- `EnsureCreated` protegido por `try/catch` (não derruba a app se falhar).

---

## Endpoints

### Chat (Gemini)
- `POST /api/Chat/ask` — body: `{ "message": "..." }` → retorna texto (string).  
- `GET /health` — healthcheck simples (`"healthy"`).  
> *O endpoint `/api/Chat/ping` de testes foi mantido no código apenas para diagnóstico, mas não é listado na UI.*

### Usuários
- `GET /api/Users?search={nickname}` — lista (filtro opcional por nickname).
- `GET /api/Users/{id}` — detalhe.
- `POST /api/Users` — cria (`{ nickname, email }`).
- `PUT /api/Users/{id}` — atualiza (`{ id, nickname, email }`).
- `DELETE /api/Users/{id}` — remove.
- `POST /api/Users/{userId}/progresslogs` — cria um log de progresso para o usuário.

### ProgressLogs (suporte/fallback)
- `GET /api/ProgressLogs` — lista todos (usado pela UI para exibir).
- `POST /api/ProgressLogs` — cria (fallback caso a rota aninhada não exista).

---

## UI embutida (`wwwroot`)

Há uma **interface simples e profissional (tema Fênix)** acessível na **raiz** da aplicação:
```
https://localhost:7219/         (dev)
https://<seuapp>.azurewebsites.net/ (prod)
```
Ela inclui:
- **Chat**: envia prompts ao Gemini.
- **Usuários**: CRUD com busca por apelido.
- **Progresso**: criação e listagem de ProgressLogs.

A UI também permite **alterar a base da API** (útil quando a UI está em produção e você quer apontar para outra instância).

> Caso publique e o `index.html` não apareça na raiz, verifique o `.csproj` para incluir a cópia da pasta `wwwroot` no publish:
```xml
<ItemGroup>
  <Content Include="wwwroot\**\*">
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
  </Content>
</ItemGroup>
```

---

## Configuração (dev e prod)

### AppSettings (dev)
`appsettings.json` (ou User Secrets) — **NÃO** versione chaves reais.
```json
{
  "ConnectionStrings": { "DefaultConnection": "Data Source=fyora_api.db" },
  "Gemini": {
    "ApiKey": "SUA_CHAVE_AQUI",
    "Model": "gemini-2.0-flash",
    "MaxTokens": 512,
    "Temperature": 0.7
  },
  "Cors": { "AllowedOrigins": [ "https://localhost:7219", "http://localhost:3000" ] }
}
```

Configure a chave local com **User Secrets**:
```bash
dotnet user-secrets set "Gemini:ApiKey" "<SUA_CHAVE_REAL>"
```

### App Settings (Azure)
No App Service, use **dois underlines `__`** em nomes hierárquicos:

| Chave | Valor |
|---|---|
| `ConnectionStrings__DefaultConnection` | `Data Source=/home/site/wwwroot/app_data/fyora_api.db` |
| `Swagger__Enabled` | `true` |
| `GEMINI_API_KEY` **ou** `Gemini__ApiKey` | `<sua chave>` |
| *(Opcional)* `Cors__AllowedOrigins__0` | `https://seu-front.com` |
| *(Opcional)* `Cors__AllowedOrigins__1` | `http://localhost:3000` |

> **Importante:** crie a pasta persistente no Kudu:  
> `site/wwwroot/app_data` (DebugConsole → `mkdir -p /home/site/wwwroot/app_data`)

---

## Execução local

```bash
dotnet restore
dotnet build
dotnet dev-certs https --trust
dotnet run
```

- UI: `https://localhost:7219/`
- Swagger: `https://localhost:7219/swagger`
- Health: `https://localhost:7219/health`

Se quiser linha de comando para **efetuar chamadas**:

```bash
# Criar usuário
curl -k -X POST https://localhost:7219/api/Users \
  -H "Content-Type: application/json" \
  -d "{\"nickname\":\"ana\",\"email\":\"ana@exemplo.com\"}"

# Listar usuários (busca por apelido)
curl -k "https://localhost:7219/api/Users?search=ana"

# Chat
curl -k -X POST https://localhost:7219/api/Chat/ask \
  -H "Content-Type: application/json" \
  -H "Accept: text/plain" \
  -d "{\"message\":\"Estou no dia 7 sem apostar e ansioso pelo fim de semana. Alguma dica?\"}"
```

---

## Publicação em Azure

**Pré-requisitos**
- Assinatura ativa (ex.: **Azure for Students**).
- Plano de App Service (Linux) **B1 ou superior** recomendado; funciona no F1, mas com limites.
- Visual Studio **Publish** (Zip Deploy) ou **GitHub Actions**.

**Passos (Zip Deploy via VS)**  
1. Build **Release**.  
2. `Publicar` → **Serviço de Aplicativo do Azure (Linux)** → Criar ou selecionar App.  
3. Em **Variáveis de ambiente** do App Service, adicione:
   - `ConnectionStrings__DefaultConnection = Data Source=/home/site/wwwroot/app_data/fyora_api.db`
   - `Swagger__Enabled = true`
   - `GEMINI_API_KEY` (ou `Gemini__ApiKey`)  
4. Abra o **Kudu** e crie a pasta `site/wwwroot/app_data`.  
5. **Restart** o App Service.  
6. Acesse:
   - UI: `https://<seuapp>.azurewebsites.net/`
   - Swagger: `https://<seuapp>.azurewebsites.net/swagger`
   - Health: `https://<seuapp>.azurewebsites.net/health`

**Observações**
- **NÃO defina** Startup Command manualmente para apps .NET “built-in”.  
- Se usar **GitHub Actions** (Deployment Center), mantenha os **App Settings** no portal/KeyVault.

---

## Troubleshooting (erros comuns)

**`Application Error / 503` após publicar**  
Geralmente é o SQLite tentando escrever em área **read-only**.  
✔ Garanta:
- `ConnectionStrings__DefaultConnection` apontando para `/home/site/wwwroot/app_data/fyora_api.db`  
- Pasta `site/wwwroot/app_data` **existe** (Kudu)  
- **Restart** após salvar variáveis

**`/swagger` não abre em produção**  
✔ Adicione `Swagger__Enabled = true` e reinicie.

**CORS no navegador (frontend externo)**  
✔ Adicione `Cors__AllowedOrigins__N` com a(s) origem(ns).

**Segredos no GitHub**  
✔ Nunca commit da chave. Use **User Secrets** local e **App Settings** no Azure.

---

## Consultas LINQ implementadas

- **Filtro por `Nickname` (case-insensitive)**:
  ```csharp
  var query = _ctx.Users.AsQueryable();
  if (!string.IsNullOrWhiteSpace(search))
      query = query.Where(u => u.Nickname.ToLower().Contains(search.ToLower()));
  var result = await query.OrderBy(u => u.Nickname).ToListAsync();
  ```

- **Logs de um usuário ordenados por data**:
  ```csharp
  var logs = await _ctx.ProgressLogs
      .Where(p => p.UserId == userId)
      .OrderByDescending(p => p.LogDate)
      .ToListAsync();
  ```

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
├─ wwwroot/
│  └─ index.html          # UI (tema Fênix) para Chat/CRUD
├─ Properties/
│  └─ launchSettings.json
├─ Program.cs
├─ appsettings.json
└─ README.md
```

---

## Diagramas 

### (C4 + Sequência)

<img width="423" height="650" alt="C4-Container" src="https://github.com/user-attachments/assets/16b08a76-f53a-45d5-919d-397f11550397" />

### C4 - Container 
<img width="810" height="365" alt="Sequência — `POST apiChatask`" src="https://github.com/user-attachments/assets/f783b445-aa40-48f7-8db9-0480e58ca666" />

---

## Checklist da Rubrica

| Critério | Status | Evidência |
|---|---|---|
| CRUD completo com EF Core | ✅ | `UsersController` e `ProgressLogs` (+ EnsureCreated) |
| Pesquisas com LINQ | ✅ | Filtro por `Nickname`, ordenações, join por `UserId` |
| Publicação em Cloud | ✅ | App Service (Linux). Passo a passo + troubleshooting |
| Endpoint externo (APIs) | ✅ | Gemini REST (`/api/Chat/ask`, health `/health`) |
| Documentação do projeto | ✅ | Este README + Swagger |
| Arquitetura em diagramas | ✅ | C4 Container + Sequência (PlantUML) |
| Versionador (repositório) | ✅ | GitHub |
| Link do plano/projeto na nuvem | ✅ | Inclua URL do App Service/Portal na entrega |

---

## Licença

Projeto acadêmico (FIAP). Uso educacional, sem garantias.
