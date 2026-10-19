# toc-builder

A web tool for building Theory of Constraints Current Reality Trees. It enforces TOC structure and offers on-request AI suggestions. See `CONTEXT.md` for the glossary and `docs/adr/` for design decisions.

## Running locally

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). From the repo root:

```sh
dotnet run --project src/TocBuilder.Web --launch-profile http
```

Then open <http://localhost:5006>. Trees are saved in your browser's localStorage, so there is nothing else to set up.

To use Suggestions, give the server an Anthropic API key. The key stays on the server and is never sent to the browser:

```sh
dotnet user-secrets set "Anthropic:ApiKey" "<your key>" --project src/TocBuilder.Web
```

`Anthropic:Model` optionally overrides the model (default `claude-sonnet-5-5`). Without a key the editor still works and Suggestions report that they are not set up. The server stores no trees: each request carries the tree.

Run the tests with `dotnet test`.

## Agent skills

This repo is worked on with the coding-agent skills from [`mattpocock/skills`](https://github.com/mattpocock/skills). Install them into your checkout with:

```sh
npx skills@latest add mattpocock/skills
```

Pick the skills you want and the agents to install them for. Make sure `setup-matt-pocock-skills` is one of them, because the repo's `docs/agents/` configuration was created by it.

- The skills are installed into `.agents/`, which is not committed. `skills-lock.json` is committed: it records which skills (and which content hash of each) the project was set up with, so you can see what has changed when you update.
- Update later with `npx skills update`.
- Install the skills this way **or** through the Claude Code plugin (`/plugin install mattpocock-skills`), not both, or every skill appears twice.
