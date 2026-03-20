# AquaSafe 🌊

Monitor de balneabilidade das praias do litoral de Santa Catarina - Vale do Itajaí.

## Stack

| Camada | Tecnologia |
|--------|-----------|
| Backend | ASP.NET Core 10 Minimal API |
| Frontend | HTML + CSS + Leaflet.js (em `wwwroot/`) |
| Cache | `IMemoryCache` (TTL 6h) |
| Dados | IMA/SC (`balneabilidade.ima.sc.gov.br`) |
| Deploy | Azure App Service F1 (Linux) |
| CI/CD | Azure DevOps Pipelines |

## Pré-requisitos

- [.NET 10 SDK]
- Visual Studio/code

## Rodar localmente

```bash
git clone <url-do-repo>
cd AquaSafe/AquaSafe.Api
dotnet run
```

Abra: http://localhost:5000

API docs (Scalar): http://localhost:5000/scalar/v1

## Endpoints

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/api/beaches` | Lista praias com status atual |
| GET | `/api/beaches/{id}/history` | Histórico de coletas de uma praia |
| GET | `/api/health` | Health check |

## Deploy

O deploy é automático via Azure DevOps Pipelines no push para `main`.

Antes do primeiro uso, altere as variáveis no `azure-pipelines.yml`:

```yaml
azureSubscription: 'ScAquaSafe'   # Service Connection no Azure DevOps
webAppName:        'AquaSafe'   # Nome do App Service
resourceGroup:     'AquaSafe-Resource-Group'    # Nome do Resource Group
```

## Estrutura do projeto

```
AquaSafe/
├── AquaSafe.Api/
│   ├── wwwroot/          # Frontend estático (index.html, Leaflet.js)
│   ├── Endpoints/        # Minimal API endpoints
│   ├── Models/           # Modelos de dados
│   ├── Services/         # ImaService (scraping + cache)
│   ├── Program.cs        # Entry point + DI
│   └── appsettings.json
├── azure-pipelines.yml   # CI/CD Azure DevOps
├── .gitignore
└── AquaSafe.sln
```

## Dados

Os dados de balneabilidade são coletados do portal oficial do IMA/SC e
cacheados em memória por 6 horas.

## Equipe

Gabriel Guisloti · Luiza Emmerich Côrte  
Curso de Análise e Desenvolvimento de Sistemas — UNIVALI
