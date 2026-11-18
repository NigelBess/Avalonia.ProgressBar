---
name: public-surface
description: Summarise the public API surface of the ProgressBars.Avalonia package - every public type and each of its public members, with a one-line description. Trigger when the user asks for a "public surface summary", "public API", "what's public", or invokes /public-surface.
---

# Public surface summary

Report what a consumer of the `ProgressBars.Avalonia` NuGet package can see, and nothing else. Reply in chat; do not write a file unless asked.

## Gather it from the source, not from memory

The surface changes often, so re-read it every time.

1. Find the public types: search `ProgressBars.Avalonia/*.cs` for lines starting with `public ` (top-level declarations). The demo project is not part of the package; ignore it.
2. For each public type, list its public members: search that type's file for indented `public ` lines.
3. Leave out anything a consumer cannot use:
   - members of `internal` types, even if the member itself is marked `public` (for example an internal dialog class that shares a file with a public one);
   - `internal`, `private` and `protected` members of sealed types;
   - overrides of inherited members, such as `Render` or `MeasureOverride`.
4. Read enough of each member's body to describe what it actually does, including behaviour a caller would not guess from the signature (what it returns on failure, what it throws, defaults).

## Format

Start with one sentence saying how many public types there are and which namespace they are in, and that everything else is internal.

Then one block per type, most central type first:

```
**`TypeName`** (kind, base type or interface if any) — what it is, in a few words
- `signature` — what it does.
```

- Give full signatures: return type, parameter types and names, and `{ get; }` / `{ get; set; }` on properties.
- Note when a type has no public constructor and say where instances come from.
- For Avalonia controls, list the settable properties individually, then put all the static `...Property` fields on a single bullet instead of one each.
- Finish with one line saying that inherited Avalonia members (`IsVisible`, `Margin`, and so on) are not listed.

Keep descriptions to one or two plain sentences. Do not include internal implementation details, file paths, or how the surface changed since last time unless the user asks.
