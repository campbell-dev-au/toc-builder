# toc-builder

A web tool for building Theory of Constraints Current Reality Trees. It enforces TOC structure and offers on-request AI suggestions. See `CONTEXT.md` for the glossary and `docs/adr/` for design decisions.

## Agent skills

This repo is worked on with the coding-agent skills from [`mattpocock/skills`](https://github.com/mattpocock/skills). Install them into your checkout with:

```sh
npx skills@latest add mattpocock/skills
```

Pick the skills you want and the agents to install them for. Make sure `setup-matt-pocock-skills` is one of them, because the repo's `docs/agents/` configuration was created by it.

- The skills are installed into `.agents/`, which is not committed. `skills-lock.json` is committed: it records which skills (and which content hash of each) the project was set up with, so you can see what has changed when you update.
- Update later with `npx skills update`.
- Install the skills this way **or** through the Claude Code plugin (`/plugin install mattpocock-skills`), not both, or every skill appears twice.
