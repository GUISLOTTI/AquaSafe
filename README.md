# AquaSafe

Monitor de balneabilidade das praias do litoral de Santa Catarina - Regiao de Itajai e Penha.

## Sobre

O AquaSafe e uma aplicacao web que consolida e exibe os dados oficiais de balneabilidade fornecidos pelo IMA/SC para as praias entre Itajai e Penha (SC). A aplicacao apresenta um mapa interativo com 16 pontos de monitoramento, alertas de chuva recente e historico das ultimas 5 coletas de cada praia.

**Acesse em producao:** https://aquasafe.azurewebsites.net

## Funcionalidades

- Mapa interativo com marcadores coloridos por status (proprio/improprio)
- Dados reais do IMA/SC via integracao direta com a API oficial (`POST /relatorio/mapa`)
- Painel lateral com resumo geral (contagem de praias proprias/improprias) e lista navegavel por cidade
- Painel de detalhes ao clicar em uma praia (status, ultima coleta, historico)
- Alerta de chuva recente via Open-Meteo (banner quando precipitacao >= 5mm em 24h)
- Cache em memoria (TTL 6h) para reduzir chamadas a API externa
- Interface responsiva (desktop e mobile)

## Stack

| Camada | Tecnologia |
|--------|-----------|
| Backend | ASP.NET Core 10 Minimal API |
| Frontend | HTML + CSS + JavaScript + Leaflet.js (em `wwwroot/`) |
| Cache | `IMemoryCache` (TTL 6h) |
| Dados de balneabilidade | IMA/SC (`balneabilidade.ima.sc.gov.br`) |
| Dados meteorologicos | Open-Meteo API (gratuita, sem chave) |
| Deploy | Azure App Service F1 (Linux) |
| CI/CD | Azure DevOps Pipelines |
| Gerenciamento | SCRUM via ClickUp |

## Pre-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

## Rodar localmente

```bash
git clone https://github.com/GUISLOTTI/AquaSafe.git
cd AquaSafe/AquaSafe.Api
dotnet run
```

A aplicacao ira iniciar em `http://localhost:<porta>` e `https://localhost:<porta>` (as portas sao exibidas no terminal).

Documentacao interativa da API (Scalar): `https://localhost:<porta>/scalar/v1`

## Endpoints da API

| Metodo | Rota | Descricao |
|--------|------|-----------|
| GET | `/api/beaches` | Lista as 16 praias monitoradas com status atual de balneabilidade |
| GET | `/api/beaches/{id}/history` | Historico das ultimas 5 coletas de uma praia especifica |
| GET | `/api/weather/rain-alert` | Verifica precipitacao nas ultimas 24h na regiao |
| GET | `/api/health` | Health check da API |

### Exemplo de resposta - `/api/beaches`

```json
{
  "id": "bc-praia-de-laranjeiras",
  "name": "PRAIA DE LARANJEIRAS",
  "city": "Balneario Camboriu",
  "latitude": -26.997,
  "longitude": -48.591,
  "quality": "Proper",
  "lastSampleDate": "30/03/2026",
  "rawStatus": "PROPRIO"
}
```

## Estrutura do projeto

```
AquaSafe/
├── AquaSafe.Api/
│   ├── wwwroot/              # Frontend estatico
│   │   └── index.html        # SPA com mapa Leaflet.js
│   ├── Endpoints/
│   │   ├── BeachEndpoints.cs # GET /api/beaches, /api/beaches/{id}/history, /api/health
│   │   └── WeatherEndpoints.cs # GET /api/weather/rain-alert
│   ├── Models/
│   │   └── Beach.cs          # Records: Beach, BeachHistory, SampleEntry, RainAlert
│   ├── Services/
│   │   ├── ImaService.cs     # Integracao com API do IMA/SC (POST /relatorio/mapa)
│   │   └── WeatherService.cs # Integracao com Open-Meteo (alerta de chuva)
│   ├── Program.cs            # Entry point, DI, pipeline
│   └── appsettings.json
├── azure-pipelines.yml       # CI/CD Azure DevOps
├── AquaSafe.sln
└── README.md
```

## Como funciona a integracao com o IMA/SC

O `ImaService` faz uma unica chamada `POST` ao endpoint `/relatorio/mapa` do portal do IMA/SC, que retorna todos os pontos de coleta de Santa Catarina com as ultimas 5 analises de cada ponto. A aplicacao:

1. Filtra pelos 4 municipios alvo (Penha, Navegantes, Itajai, Balneario Camboriu)
2. Agrupa os pontos por praia (balneario)
3. Aplica criterio de seguranca: se qualquer ponto de coleta de uma praia esta improprio, a praia e marcada como impropria
4. Cacheia o resultado por 6 horas

## Deploy

O deploy e automatico via Azure DevOps Pipelines no push para `master`.

Variaveis do pipeline (`azure-pipelines.yml`):

```yaml
azureSubscription: 'ScAquaSafe'
webAppName:        'AquaSafe'
resourceGroup:     'AquaSafe-Resource-Group'
```

## Equipe

Gabriel Guislotti e Luiza Emmerich Corte
Curso de Analise e Desenvolvimento de Sistemas — UNIVALI (2026)

## Licenca

Projeto academico desenvolvido para a disciplina de extensao da UNIVALI.
Dados de balneabilidade: IMA/SC. Dados meteorologicos: Open-Meteo.
