# PMP — Frontend (React SPA)

Single-page web client for the Property Management Platform. It is a thin, role-aware presentation layer over
the PMP HTTP API: business rules are enforced server-side (see the root [`README.md`](../README.md) and
[`docs/adr/`](../docs/adr/)); the client only shapes navigation, forms and feedback.

## Stack

| Concern | Technology |
| --- | --- |
| UI library | React 19 + TypeScript |
| Build / dev server | Vite |
| Routing | React Router |
| Lint | Oxlint |
| Tests | *not yet configured* — tracked as `IMP-041` in [`docs/IMPLEMENTATION_PLAN.md`](../docs/IMPLEMENTATION_PLAN.md) |

There is no state-management library and no CSS framework: styling is a small token-based design system in
[`src/index.css`](src/index.css) and the in-house icon family in [`src/components/Icon.tsx`](src/components/Icon.tsx).

## Prerequisites

- Node.js 20+ and npm
- The backend running (see the root README) — the dev server proxies `/api` to `http://localhost:5070`.

## Getting started

```bash
npm install
npm run dev
```

The dev server runs at http://localhost:5173 and proxies `/api` to the API
(configured in [`vite.config.ts`](vite.config.ts)).

## Scripts

| Command | What it does |
| --- | --- |
| `npm run dev` | Start the Vite dev server with HMR on port 5173. |
| `npm run build` | Type-check (`tsc -b`) and produce a production build in `dist/`. |
| `npm run lint` | Run `oxlint`. |
| `npm run preview` | Serve the production build locally. |

## Layout

```
src/
├── api/              # Typed API client (single entry point to the backend)
├── auth/             # AuthContext: session hydration, tokens, sign-in/out
├── components/       # Shell (AppLayout), Icon, route guard, notification bell
├── pages/            # One component per screen / module
├── navigation.tsx    # Single declarative manifest: sidebar + route guards
├── App.tsx           # Route table
└── index.css         # Design tokens + shared primitives
```

A page is reached only through the guard defined for it in [`src/navigation.tsx`](src/navigation.tsx); the
guard mirrors the server-side role policy for the same endpoints. A denial renders the dedicated
[`ForbiddenPage`](src/pages/ForbiddenPage.tsx) rather than silently redirecting.

## Terminology and status

Domain terms (property, building, residential unit, resident, occupancy, maintenance request) follow
[`docs/glossary.md`](../docs/glossary.md). Feature completeness is tracked in
[`docs/IMPLEMENTATION_PLAN.md`](../docs/IMPLEMENTATION_PLAN.md); the frontend has **no automated tests yet**.
