# BirdReport

A personal tool I'm planning to build that will check eBird for me and send back a plain-English summary and next-day outlook, instead of me refreshing the site every morning.

## Why

I watch the same handful of eBird spots daily, and most days nothing's changed. The idea is to automate that checking and only get bothered with what actually matters: what showed up, anything notable, how much activity there was, and whether tomorrow looks worth heading out for. Starting this as a design doc before writing a line of code.

## What it should do

- Pull recent and notable sightings for saved locations (lat/lng + radius), any number of them
- Keep a local archive so it's not re-querying the same window twice
- Compute the factual stuff deterministically (species counts, week-over-week deltas, checklist volume) so the numbers in the report are always right
- Let Claude enrich that with live data of its own choosing (historic comparisons, regional notables, arrival trends) and write the narrative in natural language: a short daily writeup, an interesting observation or trend, and a prediction for the next day
- Send the result as a daily email
- Generate a longer weekly report on the same deterministic-stats-first pattern
- Flag species from a personal watchlist when they turn up
- Track checklist volume per location as a rough activity signal (see note below — it won't be quite "number of people")

## How it's meant to be built

Three layers, kept deliberately separate so any one can be swapped without touching the others.

**eBird MCP server** — plan is to use [birdingkit/ebird-mcp-server](https://github.com/birdingkit/ebird-mcp-server), a third-party Python MCP server wrapping the eBird API (recent/notable observations, historic-by-date, regional stats, taxonomy, hotspots). Not my code — I'll treat it like any API client library: infrastructure to depend on, not own. It's stateless, so every call would be a live round trip. Unlike a plain API wrapper, this actually gets used for what MCP is for, calling any extra tools it deems neccesary.

**Local database (EF Core)** — everything persistent would live here, since nothing upstream remembers anything between calls.
- `Location` — saved spots
- `WatchSpecies` — the watchlist
- `ObservationLog` — running archive, keyed by checklist + species
- `HistoricCache` — cached historic-by-date results (a multi-year weekly baseline means one call per past year, so this should save a lot of repeat querying)
- `YesterdaysInfo` - yesterdays fun facts and recomendations so every day is slightly different even if environments don't change

**C# orchestrator (ASP.NET Core + Quartz.NET)** — where the actual application logic would live.
- Scheduled jobs for collection, daily reports, weekly reports
- Talk to the MCP server as a stdio subprocess (same pattern Claude Desktop uses)
- Check the local cache before deciding a call is even necessary
- Compute the deterministic stats itself — counts, deltas, watchlist matches, dedupe — before anything reaches the LLM. These numbers are ground truth and get passed to Claude as-is, not recomputed by it.
- Hand those stats to Claude along with live MCP tool access and a prompt to enrich, find something interesting, and predict the next day — then run a bounded tool-calling loop (hard cap on turns, e.g. 4-5) so a weird day can't turn into an unbounded chain of tool calls. After the cap, force a final-answer turn instead of honoring further tool requests.
- Take Claude's final text and send it as the daily email; also serve it through a small local dashboard
- Clearly separate "computed stat" from "Claude's prediction/insight" in the output, so a guess never reads like a verified number

## Planned stack

| Layer | Choice |
|---|---|
| Backend | C# / .NET 8, ASP.NET Core |
| Scheduling | Quartz.NET |
| Persistence | EF Core, SQLite |
| Bird data | eBird API v2.0, via birdingkit's MCP server (Python) |
| Report writing | Anthropic API (Claude), stats context + bounded agentic MCP tool loop |
| Delivery | Daily email + local dashboard |
| Frontend | Blazor Server — minimal, this is a personal tool |
| Protocol | MCP, stdio transport |

## Setup (once this exists)

1. Get an eBird API key: https://ebird.org/api/keygen
2. 

## Things to keep in mind about the data

- No eBird endpoint returns *your own* submission history — the API key just authenticates, it doesn't scope to you. Getting that in would mean importing eBird's "Download My Data" CSV by hand — not something this will do out of the gate.
- "Checklists submitted" will be what gets reported and used as the statistic to determine the "volume of birding". This is not neccesarily representative because one person can submit multiple checklists per day.
- The historic endpoint is per-date, not range-based, so a multi-year baseline costs one call per year per location — hence the plan for `HistoricCache`.
- eBird coordinates are precise to ~1km (two decimal places). Fine for regional queries, not for anything tighter.

## Roadmap

### Daily

**Deterministic**
- [X] Recent nearby birds, past 1 day
- [X] Count of different species seen, past day
- [X] Recent nearby birds, past 7 days
- [X] Count of different species seen, past 7 days
- [X] Total volume of checklists in your county, past day
- [X] Notbale Birds Seen in county with DTO for specifics 

**Deterministic, via cache**
- [ ] Checklist volume — difference between past day and the day before
- [ ] Top hotspot area in your county, past 3 days ( expensive — loops through every hotspot in the area)

**Claude, key reasoning**
- [X] Ask for differences in birds seen past day vs. day before
- [X] Ask why it thinks these new birds showed up
- [X] Ask for notable observations (detail=full), and have it explain why/how it thinks they were observed, and whether they've been reviewed
- [X] Flag "seen here but rare for the area"
- [X] Give taxonomy or a fun fact for a bird seen yesterday
- [X] Figure out how to incorporate web searching
- [] Figure out how to incorporate weather API

### Weekly
- [ ] Deterministic stats, grounded in real week-over-week deltas, not just a bigger daily summary
- [ ] MCP-enriched reasoning
- [ ] Delivery via local app / dashboard

### Questions to explore
- [X] How to get the Claude API to query other APIs the way an MCP would
- [X] How the Claude API differs from Claude Desktop/terminal, and whether it can be used programmatically
- [X] Is there a better alternative to using a raw API key?

### Infra / setup
- [X] Add `.gitignore` (`obj/`, `bin/`, IDE files, secrets)
- [X] Configure environment variables (`.env`) for API keys
- [ ] Add caching
- [X] Fork the MCP server and modify it so it doesn't depend on a Claude Desktop UI
- [ ] Summarize activity for any days that got skipped (missed run, outage, etc.)

### Base build
- [X] Set up the base project structure (API host, jobs, data layer, orchestrator)
- [X] Get the MCP server talking to the orchestrator over stdio
- [X] Wire up the bounded agentic loop: stats as context, live MCP tool access, turn cap, forced final answer
- [ ] Daily email delivery
- [ ] Minimal dashboard to view reports
- [ ] Import personal submission history from eBird's CSV export
- [ ] Maybe write my own MCP server in C# someday, just to keep one language — not urgent

## Prerequisites
- Python 3.10+ installed and on PATH (`python3 --version` to check)
- Install dependencies:
  cd MCP/mcp-server
  pip install -r requirements.txt
## Acknowledgments

Bird data from [eBird](https://ebird.org), Cornell Lab of Ornithology. MCP server by [birdingkit](https://github.com/birdingkit/ebird-mcp-server) — not my work. Report writing planned via Anthropic's Claude API.

## License

MIT

### Pricing
Using claudes API tokens it currently costs ~25 cents per summary - Too Expensive!
