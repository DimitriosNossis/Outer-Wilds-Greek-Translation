#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Merge the Greek column of the CSV back into the XML.

    python3 ow_import.py Translation.en.xml work.csv ../GreekTranslation/assets/Translation.xml

Always builds from the English master, so keys, indentation and XML escaping
are never touched. Rows with an empty greek cell stay English.

Refuses to write if a translation drops or adds markup tags, changes the
number of \\n line breaks, or touches an ASCII-art entry.
"""
import csv, re, sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import owxml

TAG = re.compile(r'<(?:/?(?:color|i|b|size)(?:=[^>]*)?|Pause(?:=[\d.]+)?/?)>')
TONOS = set('άέήίόύώΆΈΉΊΌΎΏΐΰ')


def main():
    if len(sys.argv) < 4:
        print(__doc__)
        return 2
    src, csv_path, out = sys.argv[1], sys.argv[2], sys.argv[3]

    raw = open(src, encoding='utf-8', newline='').read()
    entries = {(e['section'], e['idx']): e for e in owxml.parse(raw)}

    replacements, errors, warnings = {}, [], []
    with open(csv_path, encoding='utf-8-sig') as f:
        for r in csv.DictReader(f):
            greek = (r.get('greek') or '').strip()
            if not greek:
                continue
            try:
                sec, idx = r['row_id'].split(':')
                key = (sec, int(idx))
            except Exception:
                errors.append(f"{r.get('row_id')!r}: malformed row_id")
                continue
            if key not in entries:
                errors.append(f"{r['row_id']}: no such entry in the master XML")
                continue

            eng = entries[key]['value']

            if 'ART' in (r.get('flags') or ''):
                errors.append(f"{r['row_id']}: ASCII-art entry must stay untranslated")
                continue
            if sorted(TAG.findall(eng)) != sorted(TAG.findall(greek)):
                errors.append(f"{r['row_id']}: markup tags differ from the English")
                continue
            if eng.count('\\\\n') != greek.count('\\\\n'):
                errors.append(f"{r['row_id']}: number of \\\\n line breaks differs")
                continue

            if 'CAPS' in (r.get('flags') or '') and any(c in TONOS for c in greek):
                warnings.append(f"{r['row_id']}: tonos in an ALL-CAPS UI string")
            if r.get('section') == 'ui' and len(greek) > max(12, len(eng) * 1.35):
                warnings.append(f"{r['row_id']}: {len(greek)} chars vs {len(eng)} - may overflow")

            replacements[key] = greek

    for w in warnings[:20]:
        print("warn:", w)
    if len(warnings) > 20:
        print(f"warn: ... {len(warnings)-20} more")

    if errors:
        print(f"\n{len(errors)} ERROR(S) - nothing written:")
        for e in errors[:30]:
            print("  ", e)
        if len(errors) > 30:
            print(f"   ... {len(errors)-30} more")
        return 1

    open(out, 'w', encoding='utf-8', newline='').write(owxml.rebuild(raw, replacements))
    print(f"\n{len(replacements)} translations merged -> {out}")
    print(f"{len(entries)-len(replacements)} entries still English")
    return 0


if __name__ == '__main__':
    sys.exit(main())
