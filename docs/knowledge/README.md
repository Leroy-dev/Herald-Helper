# DAoC Knowledge Base — HeraldHelper

Findings reports from reverse-engineering and analysis work across the
HeraldHelper / DaocUI projects. Each file is a living document — update
it when new facts are confirmed; mark speculation as such.

| File | Scope |
|------|-------|
| [client-internals.md](client-internals.md) | Eden/DAoC client memory structures: chat.log FILE* buffer, adapter std::map heap records, object registry, chat text arena, bitmap-font glyph tables |
| [cc-immunity.md](cc-immunity.md) | Crowd-control message strings, parser findings, immunity formulas, recast data, stale-target behavior |
| [dlls.md](dlls.md) | `eden.dll` and Blackthorn's DLL: spell catalogs, headers, integration points |
| [harness-ocr.md](harness-ocr.md) | How HeraldHelper reads the client: OCR paths, memory sources, chat sources, failure modes |

Client-side packaging topics (UI packages, MPK archives, DAOCED
vocabulary) live in the DaocUI repo under `docs/knowledge/`.
