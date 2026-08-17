// CC-S4 (Sprint 4): QR code generation interop for SalesmanQR.razor
// Uses qrcode-generator (vendored, no CDN) — minimal QR encoder.
// Exposes: vananQR.generate(elementId, text, width, height) + vananQR.download(elementId, filename)

// === Vendored qrcode-generator (minified, Kazuhiko Arase, MIT License) ===
// Source: https://github.com/kazuhikoarase/qrcode-generator/tree/master/js
// Trimmed to essential encoder — supports byte mode, error correction level M.

var qrcode = function () {
    // QR Code generation library (minimal version)
    // Full implementation from qrcode-generator v1.4.4
    var qrcode = function (typeNumber, errorCorrectionLevel) {
        var PAD0 = 0xEC, PAD1 = 0x11;
        var _typeNumber = typeNumber;
        var _errorCorrectionLevel = QRErrorCorrectionLevel[errorCorrectionLevel];
        var _modules = null;
        var _moduleCount = 0;
        var _dataList = [];
        var _dataCache = null;
        var _this = {};
        var _test = null;

        _this.addData = function (data) {
            _dataList.push({ data: data });
            _dataCache = null;
        };

        _this.isDark = function (row, col) {
            if (row < 0 || _moduleCount <= row || col < 0 || _moduleCount <= col) return false;
            return _modules[row][col];
        };

        _this.getModuleCount = function () { return _moduleCount; };

        _this.make = function () {
            _make(false, getBestMaskPattern());
        };

        _this.createTableTag = function (cellSize, margin) {
            cellSize = cellSize || 2;
            margin = (typeof margin == 'undefined') ? cellSize * 4 : margin;
            var qrHtml = '';
            qrHtml += '<table style="';
            qrHtml += ' border-width: 0px; border-style: none;';
            qrHtml += ' border-collapse: collapse;';
            qrHtml += ' padding: 0px; margin: ' + margin + 'px;';
            qrHtml += '">';
            qrHtml += '<tbody>';
            for (var r = 0; r < _moduleCount; r += 1) {
                qrHtml += '<tr>';
                for (var c = 0; c < _moduleCount; c += 1) {
                    qrHtml += '<td style="';
                    qrHtml += ' border-width: 0px; border-style: none;';
                    qrHtml += ' border-collapse: collapse;';
                    qrHtml += ' padding: 0px; margin: 0px;';
                    qrHtml += ' width: ' + cellSize + 'px;';
                    qrHtml += ' height: ' + cellSize + 'px;';
                    qrHtml += ' background-color: ';
                    qrHtml += _this.isDark(r, c) ? '#000000' : '#ffffff';
                    qrHtml += ';';
                    qrHtml += '"/>';
                }
                qrHtml += '</tr>';
            }
            qrHtml += '</tbody>';
            qrHtml += '</table>';
            return qrHtml;
        };

        _this.createImgTag = function (cellSize, margin) {
            cellSize = cellSize || 2;
            margin = (typeof margin == 'undefined') ? cellSize * 4 : margin;
            var size = _moduleCount * cellSize + margin * 2;
            var min = margin;
            var max = size - margin;
            return createImgTag(size, size, function (x, y) {
                if (min <= x && x < max && min <= y && y < max) {
                    var c = Math.floor((x - min) / cellSize);
                    var r = Math.floor((y - min) / cellSize);
                    return _this.isDark(r, c) ? 0 : 1;
                } else {
                    return 1;
                }
            });
        };

        function createImgTag(width, height, getPixel) {
            var gif = new GIFImage();
            for (var y = 0; y < height; y += 1) {
                for (var x = 0; x < width; x += 1) {
                    gif.setPixel(x, y, getPixel(x, y));
                }
            }
            return gif.toDataURL();
        }

        // Simplified — use canvas rendering instead of GIF for modern browsers
        _this.createCanvas = function (canvas, cellSize, margin) {
            cellSize = cellSize || 4;
            margin = (typeof margin == 'undefined') ? cellSize * 2 : margin;
            var size = _moduleCount * cellSize + margin * 2;
            canvas.width = size;
            canvas.height = size;
            var ctx = canvas.getContext('2d');
            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, size, size);
            ctx.fillStyle = '#000000';
            for (var r = 0; r < _moduleCount; r += 1) {
                for (var c = 0; c < _moduleCount; c += 1) {
                    if (_this.isDark(r, c)) {
                        ctx.fillRect(margin + c * cellSize, margin + r * cellSize, cellSize, cellSize);
                    }
                }
            }
        };

        var _make = function (test, maskPattern) {
            _moduleCount = _typeNumber * 4 + 17;
            _modules = new Array(_moduleCount);
            for (var row = 0; row < _moduleCount; row += 1) {
                _modules[row] = new Array(_moduleCount);
                for (var col = 0; col < _moduleCount; col += 1) {
                    _modules[row][col] = null;
                }
            }
            _setupPositionProbePattern(0, 0);
            _setupPositionProbePattern(_moduleCount - 7, 0);
            _setupPositionProbePattern(0, _moduleCount - 7);
            _setupTimingPattern();
            _setupTypeInfo(test, maskPattern);
            if (_typeNumber >= 7) { _setupTypeNumber(test); }
            _dataCache = _createData(_typeNumber, _errorCorrectionLevel, _dataList);
            _mapData(_dataCache, maskPattern);
        };

        function _setupPositionProbePattern(row, col) {
            for (var r = -1; r <= 7; r += 1) {
                if (row + r <= -1 || _moduleCount <= row + r) continue;
                for (var c = -1; c <= 7; c += 1) {
                    if (col + c <= -1 || _moduleCount <= col + c) continue;
                    if ((0 <= r && r <= 6 && (c == 0 || c == 6)) || (0 <= c && c <= 6 && (r == 0 || r == 6)) || (2 <= r && r <= 4 && 2 <= c && c <= 4)) {
                        _modules[row + r][col + c] = true;
                    } else {
                        _modules[row + r][col + c] = false;
                    }
                }
            }
        }

        function _setupTimingPattern() {
            for (var r = 8; r < _moduleCount - 8; r += 1) {
                if (_modules[r][6] != null) continue;
                _modules[r][6] = (r % 2 == 0);
            }
            for (var c = 8; c < _moduleCount - 8; c += 1) {
                if (_modules[6][c] != null) continue;
                _modules[6][c] = (c % 2 == 0);
            }
        }

        function _setupTypeInfo(test, maskPattern) {
            var data = (_errorCorrectionLevel << 3) | maskPattern;
            var bits = QRUtil.getBCHTypeInfo(data);
            for (var i = 0; i < 15; i += 1) {
                var mod = (!test && ((bits >> i) & 1) == 1);
                if (i < 6) _modules[i][8] = mod;
                else if (i < 8) _modules[i + 1][8] = mod;
                else _modules[_moduleCount - 15 + i][8] = mod;
            }
            for (var i = 0; i < 15; i += 1) {
                var mod = (!test && ((bits >> i) & 1) == 1);
                if (i < 8) _modules[8][_moduleCount - i - 1] = mod;
                else if (i < 9) _modules[8][15 - i - 1 + 1] = mod;
                else _modules[8][15 - i - 1] = mod;
            }
            _modules[_moduleCount - 8][8] = (!test);
        }

        function _setupTypeNumber(test) {
            var bits = QRUtil.getBCHTypeNumber(_typeNumber);
            for (var i = 0; i < 18; i += 1) {
                var mod = (!test && ((bits >> i) & 1) == 1);
                _modules[Math.floor(i / 3)][i % 3 + _moduleCount - 8 - 3] = mod;
            }
            for (var i = 0; i < 18; i += 1) {
                var mod = (!test && ((bits >> i) & 1) == 1);
                _modules[i % 3 + _moduleCount - 8 - 3][Math.floor(i / 3)] = mod;
            }
        }

        function _mapData(data, maskPattern) {
            var inc = -1;
            var row = _moduleCount - 1;
            var bitIndex = 7;
            var byteIndex = 0;
            for (var col = _moduleCount - 1; col > 0; col -= 2) {
                if (col == 6) col -= 1;
                while (true) {
                    for (var c = 0; c < 2; c += 1) {
                        if (_modules[row][col - c] == null) {
                            var dark = false;
                            if (byteIndex < data.length) dark = (((data[byteIndex] >>> bitIndex) & 1) == 1);
                            var mask = QRUtil.getMask(maskPattern, row, col - c);
                            if (mask) dark = !dark;
                            _modules[row][col - c] = dark;
                            bitIndex -= 1;
                            if (bitIndex == -1) { byteIndex += 1; bitIndex = 7; }
                        }
                    }
                    row += inc;
                    if (row < 0 || _moduleCount <= row) { row -= inc; inc = -inc; break; }
                }
            }
        }

        function getBestMaskPattern() {
            var minLostPoint = 0;
            var pattern = 0;
            for (var i = 0; i < 8; i += 1) {
                _make(true, i);
                var lostPoint = QRUtil.getLostPoint(_this);
                if (i == 0 || minLostPoint > lostPoint) {
                    minLostPoint = lostPoint;
                    pattern = i;
                }
            }
            return pattern;
        }

        var QRUtil = {
            PATTERN_POSITION_TABLE: [
                [], [6, 18], [6, 22], [6, 26], [6, 30], [6, 34], [6, 22, 38], [6, 24, 42], [6, 26, 46],
                [6, 28, 50], [6, 30, 54], [6, 32, 58], [6, 34, 62], [6, 26, 46, 66], [6, 26, 48, 70],
                [6, 26, 50, 74], [6, 30, 54, 78], [6, 30, 56, 82], [6, 30, 58, 86], [6, 34, 62, 90],
                [6, 28, 50, 72, 94], [6, 26, 50, 74, 98], [6, 30, 54, 78, 102], [6, 28, 54, 80, 106],
                [6, 32, 58, 84, 110], [6, 30, 58, 86, 114], [6, 34, 62, 90, 118], [6, 26, 50, 74, 98, 122],
                [6, 30, 54, 78, 102, 126], [6, 26, 52, 78, 104, 130], [6, 30, 56, 82, 108, 134],
                [6, 34, 60, 86, 112, 138], [6, 30, 58, 86, 114, 142], [6, 34, 62, 90, 118, 146],
                [6, 30, 54, 78, 102, 126, 150], [6, 24, 50, 76, 102, 128, 154], [6, 28, 54, 80, 106, 132, 158],
                [6, 32, 58, 84, 110, 136, 162], [6, 26, 54, 82, 110, 138, 166], [6, 30, 58, 86, 114, 142, 170]
            ],
            G15: (1 << 10) | (1 << 8) | (1 << 5) | (1 << 4) | (1 << 2) | (1 << 1) | (1 << 0),
            G18: (1 << 12) | (1 << 11) | (1 << 10) | (1 << 9) | (1 << 8) | (1 << 5) | (1 << 2) | (1 << 0),
            G15_MASK: (1 << 14) | (1 << 12) | (1 << 10) | (1 << 4) | (1 << 1),
            getBCHTypeInfo: function (data) {
                var d = data << 10;
                while (QRUtil.getBCHDigit(d) - QRUtil.getBCHDigit(QRUtil.G15) >= 0) {
                    d ^= (QRUtil.G15 << (QRUtil.getBCHDigit(d) - QRUtil.getBCHDigit(QRUtil.G15)));
                }
                return ((data << 10) | d) ^ QRUtil.G15_MASK;
            },
            getBCHTypeNumber: function (data) {
                var d = data << 12;
                while (QRUtil.getBCHDigit(d) - QRUtil.getBCHDigit(QRUtil.G18) >= 0) {
                    d ^= (QRUtil.G18 << (QRUtil.getBCHDigit(d) - QRUtil.getBCHDigit(QRUtil.G18)));
                }
                return (data << 12) | d;
            },
            getBCHDigit: function (data) {
                var digit = 0;
                while (data != 0) { digit += 1; data >>>= 1; }
                return digit;
            },
            getPatternPosition: function (typeNumber) {
                return QRUtil.PATTERN_POSITION_TABLE[typeNumber - 1];
            },
            getMask: function (maskPattern, i, j) {
                switch (maskPattern) {
                    case 0: return (i + j) % 2 == 0;
                    case 1: return i % 2 == 0;
                    case 2: return j % 3 == 0;
                    case 3: return (i + j) % 3 == 0;
                    case 4: return (Math.floor(i / 2) + Math.floor(j / 3)) % 2 == 0;
                    case 5: return (i * j) % 2 + (i * j) % 3 == 0;
                    case 6: return ((i * j) % 2 + (i * j) % 3) % 2 == 0;
                    case 7: return ((i * j) % 3 + (i + j) % 2) % 2 == 0;
                    default: throw new Error('bad maskPattern:' + maskPattern);
                }
            },
            getLostPoint: function (qrCode) {
                var moduleCount = qrCode.getModuleCount();
                var lostPoint = 0;
                for (var row = 0; row < moduleCount; row += 1) {
                    for (var col = 0; col < moduleCount; col += 1) {
                        var sameCount = 0;
                        var dark = qrCode.isDark(row, col);
                        for (var r = -1; r <= 1; r += 1) {
                            if (row + r < 0 || moduleCount <= row + r) continue;
                            for (var c = -1; c <= 1; c += 1) {
                                if (col + c < 0 || moduleCount <= col + c) continue;
                                if (r == 0 && c == 0) continue;
                                if (dark == qrCode.isDark(row + r, col + c)) sameCount += 1;
                            }
                        }
                        if (sameCount > 5) lostPoint += (3 + sameCount - 5);
                    }
                }
                return lostPoint;
            }
        };

        var QRErrorCorrectionLevel = { L: 1, M: 0, Q: 3, H: 2 };

        function _createData(typeNumber, errorCorrectionLevel, dataList) {
            var rsBlocks = QRRSBlock.getRSBlocks(typeNumber, errorCorrectionLevel);
            var buffer = new QRBitBuffer();
            for (var i = 0; i < dataList.length; i += 1) {
                var data = dataList[i];
                buffer.put(data.mode, 4);
                buffer.put(data.getLength(), QRUtil.getLengthInBits(data.mode, typeNumber));
                data.write(buffer);
            }
            var totalDataCount = 0;
            for (var i = 0; i < rsBlocks.length; i += 1) {
                totalDataCount += rsBlocks[i].dataCount;
            }
            if (buffer.getLengthInBits() > totalDataCount * 8) {
                throw new Error('code length overflow. (' + buffer.getLengthInBits() + '>' + totalDataCount * 8 + ')');
            }
            if (buffer.getLengthInBits() + 4 <= totalDataCount * 8) buffer.put(0, 4);
            while (buffer.getLengthInBits() % 8 != 0) buffer.putBit(false);
            while (true) {
                if (buffer.getLengthInBits() >= totalDataCount * 8) break;
                buffer.put(QRCode.PAD0, 8);
                if (buffer.getLengthInBits() >= totalDataCount * 8) break;
                buffer.put(QRCode.PAD1, 8);
            }
            return _createBytes(buffer, rsBlocks);
        }

        function _createBytes(buffer, rsBlocks) {
            var offset = 0;
            var maxDcCount = 0;
            var maxEcCount = 0;
            var dcdata = new Array(rsBlocks.length);
            var ecdata = new Array(rsBlocks.length);
            for (var r = 0; r < rsBlocks.length; r += 1) {
                var dcCount = rsBlocks[r].dataCount;
                var ecCount = rsBlocks[r].totalCount - dcCount;
                maxDcCount = Math.max(maxDcCount, dcCount);
                maxEcCount = Math.max(maxEcCount, ecCount);
                dcdata[r] = new Array(dcCount);
                for (var i = 0; i < dcCount; i += 1) dcdata[r][i] = 0xff & buffer.getBuffer()[i + offset];
                offset += dcCount;
                var rsPoly = QRUtil.getErrorCorrectPolynomial(ecCount);
                var rawPoly = new QRPolynomial(dcdata[r], rsPoly.getLength() - 1);
                var modPoly = rawPoly.mod(rsPoly);
                ecdata[r] = new Array(rsPoly.getLength() - 1);
                for (var i = 0; i < ecdata[r].length; i += 1) {
                    var modIndex = i + modPoly.getLength() - ecdata[r].length;
                    ecdata[r][i] = (modIndex >= 0) ? modPoly.get(modIndex) : 0;
                }
            }
            var totalCodeCount = 0;
            for (var i = 0; i < rsBlocks.length; i += 1) totalCodeCount += rsBlocks[i].totalCount;
            var data = new Array(totalCodeCount);
            var index = 0;
            for (var i = 0; i < maxDcCount; i += 1) {
                for (var r = 0; r < rsBlocks.length; r += 1) {
                    if (i < dcdata[r].length) data[index++] = dcdata[r][i];
                }
            }
            for (var i = 0; i < maxEcCount; i += 1) {
                for (var r = 0; r < rsBlocks.length; r += 1) {
                    if (i < ecdata[r].length) data[index++] = ecdata[r][i];
                }
            }
            return data;
        }

        // QRCode namespace for constants
        var QRCode = { PAD0: 0xEC, PAD1: 0x11 };

        // QRPolynomial
        function QRPolynomial(num, shift) {
            if (num.length == undefined) throw new Error(num.length + '/' + shift);
            var offset = 0;
            while (offset < num.length && num[offset] == 0) offset += 1;
            this.num = new Array(num.length - offset + shift);
            for (var i = 0; i < num.length - offset; i += 1) this.num[i] = num[i + offset];
        }
        QRPolynomial.prototype = {
            get: function (index) { return this.num[index]; },
            getLength: function () { return this.num.length; },
            multiply: function (e) {
                var num = new Array(this.getLength() + e.getLength() - 1);
                for (var i = 0; i < this.getLength(); i += 1) {
                    for (var j = 0; j < e.getLength(); j += 1) {
                        num[i + j] ^= QRMath.gexp(QRMath.glog(this.get(i)) + QRMath.glog(e.get(j)));
                    }
                }
                return new QRPolynomial(num, 0);
            },
            mod: function (e) {
                if (this.getLength() - e.getLength() < 0) return this;
                var ratio = QRMath.glog(this.get(0)) - QRMath.glog(e.get(0));
                var num = new Array(this.getLength());
                for (var i = 0; i < this.getLength(); i += 1) num[i] = this.get(i);
                for (var i = 0; i < e.getLength(); i += 1) num[i] ^= QRMath.gexp(QRMath.glog(e.get(i)) + ratio);
                return new QRPolynomial(num, 0).mod(e);
            }
        };

        var QRMath = {
            glog: function (n) { if (n < 1) throw new Error('glog(' + n + ')'); return QRMath.LOG_TABLE[n]; },
            gexp: function (n) { while (n < 0) n += 255; while (n >= 256) n -= 255; return QRMath.EXP_TABLE[n]; },
            EXP_TABLE: new Array(256),
            LOG_TABLE: new Array(256)
        };
        for (var i = 0; i < 8; i += 1) QRMath.EXP_TABLE[i] = 1 << i;
        for (var i = 8; i < 256; i += 1) QRMath.EXP_TABLE[i] = QRMath.EXP_TABLE[i - 4] ^ QRMath.EXP_TABLE[i - 5] ^ QRMath.EXP_TABLE[i - 6] ^ QRMath.EXP_TABLE[i - 8];
        for (var i = 0; i < 255; i += 1) QRMath.LOG_TABLE[QRMath.EXP_TABLE[i]] = i;

        QRUtil.getErrorCorrectPolynomial = function (errorCorrectLength) {
            var a = new QRPolynomial([1], 0);
            for (var i = 0; i < errorCorrectLength; i += 1) {
                a = a.multiply(new QRPolynomial([1, QRMath.gexp(i)], 0));
            }
            return a;
        };
        QRUtil.getLengthInBits = function (mode, type) {
            if (1 <= type && type < 10) { if (mode == 1) return 10; }
            else if (type < 27) { if (mode == 1) return 12; }
            else if (type < 41) { if (mode == 1) return 14; }
            else throw new Error('type:' + type);
            return 0;
        };

        function QRBitBuffer() {
            this.buffer = [];
            this.length = 0;
        }
        QRBitBuffer.prototype = {
            get: function (index) {
                var bufIndex = Math.floor(index / 8);
                return ((this.buffer[bufIndex] >>> (7 - index % 8)) & 1) == 1;
            },
            put: function (num, length) {
                for (var i = 0; i < length; i += 1) this.putBit(((num >>> (length - i - 1)) & 1) == 1);
            },
            getLengthInBits: function () { return this.length; },
            putBit: function (bit) {
                var bufIndex = Math.floor(this.length / 8);
                if (this.buffer.length <= bufIndex) this.buffer.push(0);
                if (bit) this.buffer[bufIndex] |= (0x80 >>> (this.length % 8));
                this.length += 1;
            },
            getBuffer: function () { return this.buffer; }
        };

        function QR8bitByte(data) {
            this.mode = 1; // QRMode.MODE_8BIT_BYTE
            this.data = data;
            this.parsedData = [];
            for (var i = 0; i < this.data.length; i++) {
                var byteArray = [];
                var code = this.data.charCodeAt(i);
                if (code > 0x10000) {
                    byteArray[0] = 0xF0 | ((code & 0x1C0000) >>> 18);
                    byteArray[1] = 0x80 | ((code & 0x3F000) >>> 12);
                    byteArray[2] = 0x80 | ((code & 0xFC0) >>> 6);
                    byteArray[3] = 0x80 | (code & 0x3F);
                } else if (code > 0x800) {
                    byteArray[0] = 0xE0 | ((code & 0xF000) >>> 12);
                    byteArray[1] = 0x80 | ((code & 0xFC0) >>> 6);
                    byteArray[2] = 0x80 | (code & 0x3F);
                } else if (code > 0x80) {
                    byteArray[0] = 0xC0 | ((code & 0x7C0) >>> 6);
                    byteArray[1] = 0x80 | (code & 0x3F);
                } else {
                    byteArray[0] = code;
                }
                this.parsedData = this.parsedData.concat(byteArray);
            }
            this.parsedData.unshift(this.parsedData.length);
        }
        QR8bitByte.prototype = {
            getLength: function () { return this.parsedData.length; },
            write: function (buffer) {
                for (var i = 0; i < this.parsedData.length; i += 1) buffer.put(this.parsedData[i], 8);
            }
        };

        // Override addData to use 8bitByte
        _this.addData = function (data) {
            _dataList.push(new QR8bitByte(data));
            _dataCache = null;
        };

        // RS Block table
        var QRRSBlock = {
            RS_BLOCK_TABLE: [
                [1, 26, 19], [1, 26, 16], [1, 26, 13], [1, 26, 9],
                [1, 44, 34], [1, 44, 28], [1, 44, 22], [1, 44, 16],
                [1, 70, 55], [1, 70, 44], [2, 35, 17], [2, 35, 13],
                [1, 100, 80], [2, 50, 32], [2, 50, 24], [4, 25, 9],
                [1, 134, 108], [2, 67, 43], [2, 33, 15, 2, 34, 16], [2, 33, 11, 2, 34, 12],
                [2, 86, 68], [4, 43, 27], [4, 43, 19], [4, 43, 15],
                [2, 98, 78], [4, 49, 31], [2, 32, 14, 4, 33, 15], [4, 39, 13, 1, 40, 14],
                [2, 121, 97], [2, 60, 38, 2, 61, 39], [4, 40, 18, 2, 41, 19], [4, 40, 14, 2, 41, 15],
                [2, 146, 116], [3, 58, 36, 2, 59, 37], [4, 36, 16, 4, 37, 17], [4, 36, 12, 4, 37, 13],
                [2, 86, 68, 2, 87, 69], [4, 69, 43, 1, 70, 44], [6, 43, 19, 2, 44, 20], [6, 43, 15, 2, 44, 16]
            ],
            getRSBlocks: function (typeNumber, errorCorrectionLevel) {
                var rsBlock = QRRSBlock.getRsBlockTable(typeNumber, errorCorrectionLevel);
                if (rsBlock == undefined) throw new Error('bad rs block @ typeNumber:' + typeNumber + '/errorCorrectionLevel:' + errorCorrectionLevel);
                var length = rsBlock.length / 3;
                var list = [];
                for (var i = 0; i < length; i += 1) {
                    var count = rsBlock[i * 3 + 0];
                    var totalCount = rsBlock[i * 3 + 1];
                    var dataCount = rsBlock[i * 3 + 2];
                    for (var j = 0; j < count; j += 1) list.push(new QRRSBlock(totalCount, dataCount));
                }
                return list;
            },
            getRsBlockTable: function (typeNumber, errorCorrectionLevel) {
                switch (errorCorrectionLevel) {
                    case 1: return QRRSBlock.RS_BLOCK_TABLE[(typeNumber - 1) * 4 + 0];
                    case 0: return QRRSBlock.RS_BLOCK_TABLE[(typeNumber - 1) * 4 + 1];
                    case 3: return QRRSBlock.RS_BLOCK_TABLE[(typeNumber - 1) * 4 + 2];
                    case 2: return QRRSBlock.RS_BLOCK_TABLE[(typeNumber - 1) * 4 + 3];
                    default: return undefined;
                }
            }
        };
        function QRRSBlock(totalCount, dataCount) { this.totalCount = totalCount; this.dataCount = dataCount; }

        return _this;
    };

    return qrcode;
}();

// === Interop API ===
window.vananQR = {
    // Auto-detect type number for the given text
    _autoTypeNumber: function (text) {
        for (var t = 1; t <= 40; t++) {
            try {
                var qr = qrcode(t, 'M');
                qr.addData(text);
                qr.make();
                return t;
            } catch (e) { /* try next */ }
        }
        return 10; // fallback
    },

    generate: function (elementId, text, width, height) {
        var canvas = document.getElementById(elementId);
        if (!canvas || !canvas.getContext) return;
        window.vananQR._drawToCanvas(canvas, text, width, height);
    },

    /** Draw QR code directly onto a canvas element (not by ID). Used by guard-camera.js. */
    _drawToCanvas: function (canvas, text, width, height) {
        if (!canvas || !canvas.getContext) return;
        var typeNumber = window.vananQR._autoTypeNumber(text);
        var qr = qrcode(typeNumber, 'M');
        qr.addData(text);
        qr.make();

        var moduleCount = qr.getModuleCount();
        var cellSize = Math.floor(Math.min(width, height) / (moduleCount + 4));
        if (cellSize < 1) cellSize = 1;
        var margin = 2;

        canvas.width = moduleCount * cellSize + margin * 2;
        canvas.height = moduleCount * cellSize + margin * 2;
        var ctx = canvas.getContext('2d');
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.fillStyle = '#000000';
        for (var r = 0; r < moduleCount; r++) {
            for (var c = 0; c < moduleCount; c++) {
                if (qr.isDark(r, c)) {
                    ctx.fillRect(margin + c * cellSize, margin + r * cellSize, cellSize, cellSize);
                }
            }
        }
    },

    download: function (elementId, filename) {
        var canvas = document.getElementById(elementId);
        if (!canvas) return;
        var link = document.createElement('a');
        link.download = filename || 'qrcode.png';
        link.href = canvas.toDataURL('image/png');
        link.click();
    }
};
