# Translation workflow

The **CSV is the source of truth**. `Translation.xml` is a build artifact,
regenerated from `Translation.en.xml` every time. Keys, indentation and XML
escaping are never edited by hand, so they can't drift.

```
Translation.en.xml  ──export──>  work.csv  ──(you translate)──>  work.csv
                                     │
                                  import
                                     ▼
                    GreekTranslation/assets/Translation.xml  ──dotnet build──>  game
```

## Files

| File | What it is |
|---|---|
| `Translation.en.xml` | English master. **Never edit.** The original extract plus UI entries 1195-1198, which the game asks for but the extract lacked. |
| `owxml.py` | Shared parser. Not run directly. |
| `ow_export.py` | XML → CSV |
| `ow_import.py` | CSV → XML |
| `work.csv` | Your working file. 3,725 rows. |

## Daily loop

```bash
# translate in work.csv, then:
python3 ow_import.py Translation.en.xml work.csv ../GreekTranslation/assets/Translation.xml
cd ../GreekTranslation && dotnet build
```

Re-exporting is safe — pass your current CSV as a third argument and finished
rows carry over:

```bash
python3 ow_export.py Translation.en.xml work_new.csv work.csv
```

## The CSV

| Column | |
|---|---|
| `row_id` | `section:index`. The join key — **never edit, never reorder, never delete a row.** |
| `section` | `dialogue` (2,424), `shiplog` (483), `ui` (818) |
| `speaker` | Nomai wall-text speaker where the line has one — 657 rows. Filter on this to translate one character's voice in a single pass. |
| `chars` | Length of the English. Sort by it to find UI strings with no room. |
| `flags` | `TAGS` markup present · `NL` contains `\\n` · `ART` ASCII art, leave alone · `CAPS` all-caps UI string |
| `english` | Source. Read-only. |
| `greek` | Yours. Blank = untranslated, and the entry stays English in the game. |

Open it in LibreOffice or Excel as UTF-8. Sort and filter freely — `row_id` is
what matters, not row order.

## What the import refuses to write

Any of these aborts the whole import and writes nothing:

- markup tags added or dropped relative to the English
- a different number of `\\n` line breaks
- a translated `ART` row
- an unknown or malformed `row_id`

And it warns without blocking on: tonos in an ALL-CAPS UI string, and UI strings
more than 35% longer than the English.

## Suggested order

1. **Pilot** — filter to a spread: some Hearthian dialogue, some Nomai wall text
   (pick one speaker), some ship log, some UI. Enough to lock the voice before
   committing to 3,700 entries.
2. **UI** (818) — short, high visibility, catches font and overflow problems early.
3. **Ship log** (483) — self-contained, and it's where most proper nouns live, so
   it exercises the glossary hardest.
4. **Dialogue** (2,424) — the bulk. Filter by `speaker` to keep a voice consistent
   across a session.

Entries `ui:1`–`ui:8` are already done as the smoke test.
