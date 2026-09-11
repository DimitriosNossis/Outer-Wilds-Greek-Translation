#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Export the English master XML to a CSV for translating.

    python3 ow_export.py Translation.en.xml work.csv [existing_work.csv]

Pass an existing CSV as the third argument to carry over Greek you've already
written (matched by row_id), so re-exporting never loses work.

Columns:
  row_id     section:index - the join key. NEVER edit this.
  section    dialogue / shiplog / ui
  speaker    Nomai wall-text speaker, where the line has one
  chars      length of the English, for spotting UI strings with no room
  flags      TAGS = contains markup, NL = contains \\n, ART = ASCII art, CAPS = all-caps UI
  english    source text. Read-only.
  greek      YOUR COLUMN. Leave blank for untranslated.
"""
import csv, re, sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import owxml

SPEAKER = re.compile(r"^([A-Z][A-Z' ]{1,20}):")
TAG = re.compile(r'<(?:/?(?:color|i|b|size)(?:=[^>]*)?|Pause(?:=[\d.]+)?/?)>')


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    src, out = sys.argv[1], sys.argv[2]

    carried = {}
    if len(sys.argv) > 3 and os.path.exists(sys.argv[3]):
        with open(sys.argv[3], encoding='utf-8-sig') as f:
            for r in csv.DictReader(f):
                if r.get('greek', '').strip():
                    carried[r['row_id']] = r['greek']
        print(f"carrying over {len(carried)} translated rows")

    raw = open(src, encoding='utf-8', newline='').read()
    rows = []
    for e in owxml.parse(raw):
        v = e['value']
        row_id = f"{e['section']}:{e['idx']}"
        flags = []
        if TAG.search(v):
            flags.append('TAGS')
        if '\\\\n' in v:
            flags.append('NL')
        if re.search(r'[█▄░▀]', v) or v.count('W') > 40:
            flags.append('ART')
        if e['section'] == 'ui' and v.isupper() and len(v) > 2:
            flags.append('CAPS')
        m = SPEAKER.match(v)
        rows.append({
            'row_id': row_id,
            'section': e['section'],
            'speaker': m.group(1) if m else '',
            'chars': len(v),
            'flags': ' '.join(flags),
            'english': v,
            'greek': carried.get(row_id, ''),
        })

    with open(out, 'w', encoding='utf-8-sig', newline='') as f:
        w = csv.DictWriter(f, fieldnames=['row_id', 'section', 'speaker', 'chars',
                                          'flags', 'english', 'greek'])
        w.writeheader()
        w.writerows(rows)

    done = sum(1 for r in rows if r['greek'])
    print(f"{len(rows)} rows -> {out}  ({done} translated, {len(rows)-done} to go)")
    return 0


if __name__ == '__main__':
    sys.exit(main())
