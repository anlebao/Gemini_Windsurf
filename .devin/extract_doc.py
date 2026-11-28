import olefile, struct, sys

path = r'C:\VibeCoding\Gemini_Windsurf\docs\specs\Thông tư 71-2024-TT-BTC.doc'
out  = r'C:\VibeCoding\Gemini_Windsurf\.devin\tt71_extracted.txt'

ole = olefile.OleFileIO(path)
wd = ole.openstream('WordDocument').read()
tbl = ole.openstream('1Table').read()

# --- FIB ---
wIdent, nFib = struct.unpack_from('<HH', wd, 0)
print('wIdent=%04x nFib=%d' % (wIdent, nFib))

if nFib >= 193:   # Word 97/2000/2002/2003
    # fcClx/lcbClx at fixed offset 0x1A2 for Word 97-2003 (validated against 1Table bounds).
    fcClx, lcbClx = struct.unpack_from('<II', wd, 0x1A2)
else:
    raise SystemExit('unsupported nFib %d' % nFib)
print('fcClx=%d lcbClx=%d' % (fcClx, lcbClx))

clx = tbl[fcClx:fcClx+lcbClx]

# --- CLX: skip Prc entries (clxt==1), find Pcdt (clxt==2) ---
pos = 0
plcpcd = None
while pos < len(clx):
    clxt = clx[pos]
    if clxt == 1:
        cb = struct.unpack_from('<H', clx, pos+1)[0]
        pos += 3 + cb
    elif clxt == 2:
        lcb = struct.unpack_from('<I', clx, pos+1)[0]
        plcpcd = clx[pos+5:pos+5+lcb]
        pos += 5 + lcb
        break
    else:
        raise SystemExit('bad clxt %d at %d' % (clxt, pos))

n = (len(plcpcd) - 4) // (4 + 8)   # CPs array (n+1)*4 + PCDs n*8
cps = struct.unpack_from('<%dI' % (n+1), plcpcd, 0)
pcds = plcpcd[(n+1)*4:]

text_parts = []
for i in range(n):
    pcd = pcds[i*8:(i+1)*8]
    fc_raw = struct.unpack_from('<I', pcd, 2)[0]
    fCompressed = (fc_raw >> 30) & 1
    fc = fc_raw & 0x3FFFFFFF
    cch = cps[i+1] - cps[i]
    if fCompressed:
        raw = wd[fc//2: fc//2 + cch]
        text_parts.append(raw.decode('cp1258', errors='replace'))
    else:
        raw = wd[fc: fc + cch*2]
        text_parts.append(raw.decode('utf-16-le', errors='replace'))

full = ''.join(text_parts)
with open(out, 'w', encoding='utf-8') as f:
    f.write(full)
print('extracted chars=%d' % len(full))
print('sample:', full[:300].replace('\r', ' ').replace('\n', ' | '))
