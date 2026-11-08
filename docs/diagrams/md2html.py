#!/usr/bin/env python3
"""md->html with mermaid divs for docs/diagrams. Usage: python md2html.py <file.md>"""
import re, sys, html as H

def esc(s):
    return H.escape(s, quote=False)

def inline(s):
    s = esc(s)
    s = re.sub(r'\*\*(.+?)\*\*', r'<strong>\1</strong>', s)
    s = re.sub(r'`(.+?)`', r'<code>\1</code>', s)
    return s

def convert(md):
    parts = re.split(r'(```mermaid\n.*?```)', md, flags=re.S)
    out, in_ul = [], False
    def flush():
        nonlocal in_ul
        if in_ul: out.append('</ul>'); in_ul = False
    for p in parts:
        if p.startswith('```mermaid'):
            flush()
            out.append(f'<div class="mermaid">\n{p[len("```mermaid\n"):].rstrip("`").rstrip()}\n</div>')
            continue
        for line in p.splitlines():
            s = line.rstrip()
            if not s.strip():
                flush(); continue
            if s.startswith('## '): flush(); out.append(f'<h2>{inline(s[3:])}</h2>')
            elif s.startswith('### '): flush(); out.append(f'<h3>{inline(s[4:])}</h3>')
            elif s.startswith('# '): pass
            elif s.startswith('>'):
                flush(); out.append(f'<blockquote>{inline(s.lstrip("> "))}</blockquote>')
            elif s.lstrip().startswith('- '):
                if not in_ul: out.append('<ul>'); in_ul = True
                out.append(f'<li>{inline(s.lstrip()[2:])}</li>')
            elif re.match(r'^\d+\.\s', s.lstrip()):
                flush(); out.append(f'<p>{inline(s)}</p>')
            else:
                flush(); out.append(f'<p>{inline(s)}</p>')
    flush()
    return '\n'.join(out)

HDR = '''<!DOCTYPE html>
<html lang="vi"><head><meta charset="utf-8"><title>{t}</title>
<style>body{{font-family:system-ui,sans-serif;max-width:1150px;margin:2rem auto;padding:0 1rem;color:#1a202c;line-height:1.55}}h1{{font-size:1.5rem}}h2{{margin-top:2.2rem;border-bottom:1px solid #ddd;padding-bottom:.3rem}}h3{{margin-top:1.4rem}}blockquote{{color:#555;font-size:.9rem;border-left:3px solid #cbd5e0;padding-left:.8rem;margin-left:0}}.mermaid{{background:#fafafa;border:1px solid #e2e8f0;border-radius:8px;padding:1rem;overflow-x:auto}}code{{background:#edf2f7;padding:0 .3em;border-radius:3px}}ul{{padding-left:1.4rem}}</style>
<script src="../enterprise-operating-model/javascripts/mermaid.min.js"></script>
<script>document.addEventListener("DOMContentLoaded",()=>{{if(typeof mermaid!=="undefined")mermaid.initialize({{startOnLoad:true,securityLevel:"loose",flowchart:{{htmlLabels:true}}}});}});</script>
</head><body><h1>{t}</h1>
'''

if __name__ == '__main__':
    src = sys.argv[1]
    md = open(src, encoding='utf-8').read()
    title = re.search(r'^#\s+(.+)$', md, re.M).group(1)
    dst = src.rsplit('.', 1)[0] + '.html'
    with open(dst, 'w', encoding='utf-8') as f:
        f.write(HDR.format(t=esc(title)) + convert(md) + '\n</body></html>')
    print(dst)
