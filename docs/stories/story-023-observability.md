---
id: story-023
title: Application errors and response times are tracked in Application Insights
status: backlog
created: 2026-03-14
---

## What

Unhandled exceptions and slow API responses are captured in Azure Application Insights so problems can be diagnosed without reading raw logs.

## Why

Without observability, production bugs are invisible until a player reports them.

## Acceptance criteria

- [ ] Azure Application Insights is connected to the API container via the `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable
- [ ] Unhandled exceptions are automatically captured and appear in the Failures blade
- [ ] API response times are tracked; p95 latency is visible in the Performance blade
- [ ] SignalR hub errors are captured (not swallowed silently)
- [ ] The Application Insights resource is provisioned in Azure and the connection string is added to GitHub Secrets

## Notes

- Spec: `docs/requirements.md` Phase 2 roadmap ("Application Insights: errors + response times")
- Status `backlog` — Phase 2; purely infrastructure
- Devops agent owns the Azure provisioning and GitHub Secrets update; backend agent adds the NuGet package and DI wiring
- Owner action required: provision the Application Insights resource in Azure. (See `docs/owner/TODO.md`)
