import struct, zlib, sys

def analyze(path):
    with open(path, 'rb') as f:
        data = f.read()
    print('file size:', len(data))
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        print('NOT A PNG (signature fail)'); return
    print('signature: OK')
    pos = 8
    chunks = []
    while pos < len(data):
        if pos + 8 > len(data):
            print('TRUNCATED: header bytes missing at', pos); break
        length = struct.unpack('>I', data[pos:pos+4])[0]
        ctype = data[pos+4:pos+8]
        if pos + 12 + length > len(data):
            print('TRUNCATED: chunk', ctype, 'declares', length, 'but only', len(data)-pos-12, 'remain')
            break
        body = data[pos+8:pos+8+length]
        crc = struct.unpack('>I', data[pos+8+length:pos+12+length])[0]
        calc = zlib.crc32(ctype + body) & 0xffffffff
        ok = 'CRC-OK' if crc == calc else 'CRC-FAIL'
        chunks.append((ctype, length, ok))
        pos += 12 + length
        if ctype == b'IEND':
            break
    print('chunks:', chunks)
    # IHDR fields
    if data[12:16] == b'IHDR':
        w, h = struct.unpack('>II', data[16:24])
        depth = data[24]; ctype = data[25]; comp = data[26]; filt = data[27]; interlace = data[28]
        print('IHDR: %dx%d depth=%d colortype=%d comp=%d filter=%d interlace=%d' % (w, h, depth, ctype, comp, filt, interlace))
        # try decompress IDAT
        idat = b''
        pos = 8
        while pos < len(data):
            length = struct.unpack('>I', data[pos:pos+4])[0]
            ctype = data[pos+4:pos+8]
            if ctype == b'IDAT':
                idat += data[pos+8:pos+8+length]
            pos += 12 + length
            if ctype == b'IEND':
                break
        try:
            raw = zlib.decompress(idat)
            bpp = 4 if ctype in (6, 4) else (3 if ctype in (2,) else 1)
            expected = (w * bpp + 1) * h
            print('IDAT: decompressed', len(raw), 'bytes, expected', expected, '->', 'MATCH' if len(raw) == expected else 'MISMATCH')
        except Exception as e:
            print('IDAT decompress FAILED:', e)

analyze(sys.argv[1])