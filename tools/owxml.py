# -*- coding: utf-8 -*-
"""Shared parsing for the Outer Wilds TranslationTable_XML format.

Values are edited in place in the raw text so that keys, indentation,
XML escaping and self-closing tags are preserved exactly.
"""
import re

PAIR = re.compile(
    r'<key\s*/>|<key>(.*?)</key>\s*(?:<value>(.*?)</value>|<value\s*/>)', re.S)

def unescape(s):
    return s.replace('&lt;', '<').replace('&gt;', '>').replace('&amp;', '&')

def escape(s):
    return s.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')

def parse(raw):
    """Yield entries in document order."""
    sl = raw.find('<table_shipLog>')
    ui = raw.find('<table_ui>')
    counters = {'dialogue': 0, 'shiplog': 0, 'ui': 0}
    for m in PAIR.finditer(raw):
        if m.group(1) is None and m.group(2) is None and m.group(0).startswith('<key'):
            continue
        pos = m.start()
        section = 'dialogue' if pos < sl else ('shiplog' if pos < ui else 'ui')
        counters[section] += 1
        empty = m.group(2) is None
        yield {
            'section': section,
            'idx': counters[section],
            'key': unescape(m.group(1) or '').strip(),
            'value': '' if empty else unescape(m.group(2)).strip(),
            'empty': empty,
            'inner_span': None if empty else m.span(2),
            'full_span': m.span(0),
        }

def rebuild(raw, replacements):
    """replacements: {(section, idx): greek}. Returns the new raw text."""
    edits = []
    for e in parse(raw):
        new = replacements.get((e['section'], e['idx']))
        if not new:
            continue
        if e['empty']:
            a, b = e['full_span']
            edits.append(((a, b), '<key>' + escape(e['key']) + '</key>\n\t\t<value>'
                          + escape(new) + '</value>'))
        else:
            a, b = e['inner_span']
            inner = raw[a:b]
            lead = inner[:len(inner) - len(inner.lstrip())]
            trail = inner[len(inner.rstrip()):]
            edits.append(((a, b), lead + escape(new) + trail))
    out = raw
    for (a, b), new in sorted(edits, key=lambda x: -x[0][0]):
        out = out[:a] + new + out[b:]
    return out
