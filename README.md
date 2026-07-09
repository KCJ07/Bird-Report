# BirdReport

A personal tool I'm planning to build that will check eBird for me and send back a plain-English summary and next-day outlook, instead of me refreshing the site every morning.

## Why

I watch the same handful of eBird spots daily, and most days nothing's changed. The idea is to automate that checking and only get bothered with what actually matters: what showed up, anything notable, how much activity there was, and whether tomorrow looks worth heading out for. Starting this as a design doc before writing a line of code.

## What it should do

- Pull recent and notable sightings for saved locations (lat/lng + radius), any number of them
- Keep a local archive so it's not re-querying the same window twice
- Compute the boring, factual stuff deterministically — species counts, week-over-week deltas, checklist volume — so the numbers in the report are always right
- Let Claude enrich that with live data of its own choosing (historic comparisons, regional notables, arrival trends) and write the actual narrative: a short daily writeup, an interesting observation or trend, and a prediction for the next day
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
- `ReportLog` — dedupe log so nothing gets reported twice

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

## Proposed project structure

```
BirdReport.Api            ASP.NET Core host, dashboard, admin endpoints
BirdReport.Jobs           Quartz.NET jobs (collect, daily report, weekly report)
BirdReport.Data           EF Core models, migrations, DbContext
BirdReport.Orchestrator   MCP client, stats computation, bounded LLM tool-calling loop, email dispatch
```

The MCP server won't live in this repo — it'll be a separate Python process, cloned and run per its own instructions, referenced by path in config.

## Setup (once this exists)

1. Get an eBird API key: https://ebird.org/api/keygen
2. Clone [birdingkit/ebird-mcp-server](https://github.com/birdingkit/ebird-mcp-server) and get it running with your key.
3. Clone this repo, set the MCP server path, Anthropic API key, and email/SMTP config in `appsettings.json` (or user secrets, ideally from the start this time).
4. `dotnet ef database update`
5. `dotnet run --project BirdReport.Api`

## Things to keep in mind about the data

- No eBird endpoint returns *your own* submission history — the API key just authenticates, it doesn't scope to you. Getting that in would mean importing eBird's "Download My Data" CSV by hand — not something this will do out of the gate.
- "Checklists submitted" will be what gets reported, not "number of people" — those aren't the same thing, since one person can submit several checklists and submitter identity isn't reliably exposed outside notable/full-detail results.
- The historic endpoint is per-date, not range-based, so a multi-year baseline costs one call per year per location — hence the plan for `HistoricCache`.
- eBird coordinates are precise to ~1km (two decimal places). Fine for regional queries, not for anything tighter.
- Once Claude has live tool access, report content stops being fully deterministic — run-to-run variance in what it notices or how many calls it makes is expected and fine for a personal daily email, just worth knowing going in.

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
- [ ] Ask for differences in birds seen past day vs. past week
- [ ] Ask why it thinks these new birds showed up
- [ ] Ask for notable observations (detail=full), and have it explain why/how it thinks they were observed, and whether they've been reviewed
- [ ] Flag "seen here but rare for the area"
- [ ] Give taxonomy or a fun fact for a bird seen yesterday
- [ ] Figure out how to incorporate web searching
- [ ] Figure out how to incorporate weather API

**MCP, via cache**
- [ ] Difference between yesterday and today — why, including weather context

### Weekly
- [ ] Deterministic stats, grounded in real week-over-week deltas, not just a bigger daily summary
- [ ] MCP-enriched reasoning
- [ ] Delivery via local app / dashboard

### Questions to explore
- [X] How to get the Claude API to query other APIs the way an MCP would
- [X] How the Claude API differs from Claude Desktop/terminal, and whether it can be used programmatically
- [X] Is there a better alternative to using a raw API key?

### Infra / setup
- [ ] Add `.gitignore` (`obj/`, `bin/`, IDE files, secrets)
- [ ] Configure environment variables (`.env`) for API keys
- [ ] Add caching
- [ ] Fork the MCP server and modify it so it doesn't depend on a Claude Desktop UI
- [ ] Summarize activity for any days that got skipped (missed run, outage, etc.)

### Base build
- [ ] Set up the base project structure (API host, jobs, data layer, orchestrator)
- [ ] Get the MCP server talking to the orchestrator over stdio
- [ ] Wire up the bounded agentic loop: stats as context, live MCP tool access, turn cap, forced final answer
- [ ] Daily email delivery
- [ ] Minimal dashboard to view reports
- [ ] Import personal submission history from eBird's CSV export
- [ ] Maybe write my own MCP server in C# someday, just to keep one language — not urgent

## Acknowledgments

Bird data from [eBird](https://ebird.org), Cornell Lab of Ornithology. MCP server by [birdingkit](https://github.com/birdingkit/ebird-mcp-server) — not my work. Report writing planned via Anthropic's Claude API.

## License

MIT
