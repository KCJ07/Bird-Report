# BirdReport

A personal tool I'm planning to build that will check eBird for me and send back a plain-English summary, instead of me refreshing the site every morning.

## Why

I watch the same handful of eBird spots daily, and most days nothing's changed. The idea is to automate that checking and only get bothered with what actually matters: what showed up, anything notable, how much activity there was, and eventually whether it's worth heading out. Starting this as a design doc before writing a line of code, since I've been burned before by scripts that grow into services nobody wants to maintain.

## What it should do

- Pull recent and notable sightings for saved locations (lat/lng + radius), any number of them
- Keep a local archive so it's not re-querying the same window twice
- Generate a daily summary and a longer weekly report
- Flag species from a personal watchlist when they turn up
- Track checklist volume per location as a rough activity signal (see note below — it won't be quite "number of people")

## How it's meant to be built

Three layers, kept deliberately separate so any one can be swapped without touching the others.

**eBird MCP server** — plan is to use [birdingkit/ebird-mcp-server](https://github.com/birdingkit/ebird-mcp-server), a third-party Python MCP server wrapping the eBird API (recent/notable observations, historic-by-date, regional stats, taxonomy, hotspots). Not my code — I'll treat it like any API client library: infrastructure to depend on, not own. It's stateless, so every call would be a live round trip.

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
- Compute all the stats itself — counts, deltas, etc. — before anything reaches the LLM. I'd rather Claude write the paragraph than do arithmetic over raw JSON.
- Serve the report through a small local dashboard

## Planned stack

| Layer | Choice |
|---|---|
| Backend | C# / .NET 8, ASP.NET Core |
| Scheduling | Quartz.NET |
| Persistence | EF Core, SQLite |
| Bird data | eBird API v2.0, via birdingkit's MCP server (Python) |
| Report writing | Anthropic API (Claude) |
| Frontend | Blazor Server — minimal, this is a personal tool |
| Protocol | MCP, stdio transport |

## Proposed project structure

```
BirdReport.Api            ASP.NET Core host, dashboard, admin endpoints
BirdReport.Jobs           Quartz.NET jobs (collect, daily report, weekly report)
BirdReport.Data           EF Core models, migrations, DbContext
BirdReport.Orchestrator   MCP client, stats computation, LLM call
```

The MCP server won't live in this repo — it'll be a separate Python process, cloned and run per its own instructions, referenced by path in config.

## Setup (once this exists)

1. Get an eBird API key: https://ebird.org/api/keygen
2. Clone [birdingkit/ebird-mcp-server](https://github.com/birdingkit/ebird-mcp-server) and get it running with your key.
3. Clone this repo, set the MCP server path and your Anthropic API key in `appsettings.json` (or user secrets, ideally from the start this time).
4. `dotnet ef database update`
5. `dotnet run --project BirdReport.Api`

## Things to keep in mind about the data

- No eBird endpoint returns *your own* submission history — the API key just authenticates, it doesn't scope to you. Getting that in would mean importing eBird's "Download My Data" CSV by hand — not something this will do out of the gate.
- "Checklists submitted" will be what gets reported, not "number of people" — those aren't the same thing, since one person can submit several checklists and submitter identity isn't reliably exposed outside notable/full-detail results.
- The historic endpoint is per-date, not range-based, so a multi-year baseline costs one call per year per location — hence the plan for `HistoricCache`.
- eBird coordinates are precise to ~1km (two decimal places). Fine for regional queries, not for anything tighter.

## Roadmap

- [ ] Set up the base project structure (API host, jobs, data layer, orchestrator)
- [ ] Get the MCP server talking to the orchestrator over stdio
- [ ] Build out the EF Core models and initial migration
- [ ] Daily collect job + basic daily summary
- [ ] Weekly report, grounded in real week-over-week deltas, not just a bigger daily summary
- [ ] Watchlist flagging
- [ ] Minimal dashboard to view reports
- [ ] Import personal submission history from eBird's CSV export
- [ ] UI for managing locations instead of editing the DB directly
- [ ] Check whether regional/species endpoints give usable arrival-date data, or whether that needs eBird's full Basic Dataset
- [ ] Fix the "daily" window to use local timezone, not naive UTC, from day one
- [ ] Tests for the dedupe logic before it ships, not after
- [ ] Decide if reports stay dashboard-only or also get emailed
- [ ] Maybe write my own MCP server in C# someday, just to keep one language — not urgent

## Acknowledgments

Bird data from [eBird](https://ebird.org), Cornell Lab of Ornithology. MCP server by [birdingkit](https://github.com/birdingkit/ebird-mcp-server) — not my work. Report writing planned via Anthropic's Claude API.

## License

MIT
