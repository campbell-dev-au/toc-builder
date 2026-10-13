---
status: accepted
---

# All-C# stack: Blazor Web App (WebAssembly interactive) hosted by ASP.NET Core

The editor runs as Blazor WebAssembly, and the same ASP.NET Core host serves a minimal API that holds the AI key and calls the model. The domain logic is a UI-free class library shared by both. We chose this over React + TypeScript, which has the richer diagram ecosystem, because the project owner wants a single language and to build .NET skills, and the AI feature already requires a server, which removes Blazor WASM's static-hosting objection.

The decision was gated by a prototype of the interaction layer, which passed (see `prototype/blazor-interaction`). Automatic layout, Tab-to-add, node drag, link with cycle rejection, wheel zoom at cursor, background pan, fit-to-view and SVG export all worked in a real browser on a 40-entity tree. Shipping a polished editor outranks learning .NET, so if the real implementation proves clunky, supersede this ADR and fall back to React + TypeScript.

## Considered Options

- **React + TypeScript with serverless functions on Vercel**: best diagram tooling (React Flow) and simplest hosting, but two languages and no .NET practice.
- **React with an ASP.NET proxy**: puts .NET in the lowest-risk part only.
- **Blazor Server interactivity**: rejected because every drag round-trips to the server.

## Consequences

- Hosting moves off Vercel to Azure App Service or a container host.
- Layout is MSAGL (`Microsoft.Msagl`, verified in WebAssembly: about 80 ms for 40 entities and 49 links) and interaction is custom SVG. Blazor.Diagrams was not tried.
- Not covered by the prototype: touch and pinch zoom, minimap, multi-select, edge routing on dense trees, and accessibility beyond basic keyboard entry.
