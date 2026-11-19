#!/usr/bin/env python3
"""Decode EMV/VietQR payload tu response /v1/topups/{id}/qr.
Usage: python rv_p9_decode_qr.py '00020101021238...'  |  python rv_p9_decode_qr.py --from-api
"""
import sys, json, urllib.request

TAGS = {"00":"Payload Format","01":"Point Of Initiation","26":"MerchantAcct-GUI","27":"MerchantAcct-IIN",
        "28":"MerchantAcct-IBAN","29":"MerchantAcct-WALLET","30":"MerchantAcct-BANK",
        "31":"MerchantAcct-PARTNER","32":"MerchantAcct-ANY","51":"MerchantAcct-ANY2",
        "52":"MCC","53":"Currency","54":"Amount","55":"Tip","56":"Convenience",
        "57":"Value","58":"Country","59":"Merchant Name","60":"Merchant City",
        "61":"Postal","62":"Additional Data","63":"CRC","38":"MerchantAcct-Dynamic","64":"Merchant URL"}
GUID_NAMES = {"A000000727":"NAPAS/VietQR","A000000524":"EMVCo generic"}

def tlv(s, i=0):
    tag, ln = s[i:i+2], int(s[i+2:i+4]); return tag, s[i+4:i+4+ln], i+4+ln

def subtlv(v):
    out, i = {}, 0
    while i+4 <= len(v):
        t, val, i = tlv(v, i); out[t] = val
    return out

def decode(payload):
    i, fields = 0, []
    while i+4 <= len(payload):
        tag, val, i = tlv(payload, i); fields.append((tag, val))
    d = {}
    for tag, val in fields:
        name = TAGS.get(tag, f"tag{tag}")
        if tag in ("26","27","30","31","32","38","51") or (tag=="62"):
            sub = subtlv(val)
            d[name] = {f"sub{t}": v for t, v in sub.items()}
            if tag != "62":
                g = sub.get("00","")
                d[name]["_guid"] = GUID_NAMES.get(g, g)
        else:
            d[name] = val
    # flatten merchant acct info (usually tag 38 = 'MerchantAcct-Any' per EMV, but VietQR often uses 26-51 range)
    return d

def main():
    arg = sys.argv[1] if len(sys.argv) > 1 else ""
    if arg == "--from-api":
        # reads JSON from stdin: {"qrPayload":"...","billId":"..."}
        payload = json.load(sys.stdin)["qrPayload"]
    elif arg.startswith("http"):
        payload = json.load(urllib.request.urlopen(arg))["qrPayload"]
    else:
        payload = arg.strip().strip('"')
    d = decode(payload)
    print(f"payload len={len(payload)} crc={d.get('CRC','?')}")
    acct = next((v for k,v in d.items() if isinstance(v,dict) and k.startswith("MerchantAcct")), {})
    print(f"bankBin      : {acct.get('sub01','?')}")
    print(f"account      : {acct.get('sub02','?')}")
    print(f"guid         : {acct.get('_guid','?')}")
    print(f"amount(VND)  : {d.get('Amount','(empty = payer enters)')}")
    print(f"currency     : {d.get('Currency','?')}  country={d.get('Country','?')}")
    print(f"merchant     : {d.get('Merchant Name','?')} / {d.get('Merchant City','?')}")
    add = d.get("Additional Data", {})
    for k,v in add.items(): print(f"addData {k} : {v}")
    # sanity checks
    ok = True
    if acct.get("sub02") != "VQRQANSQG9134": print("!! VA mismatch"); ok=False
    if acct.get("sub01") != "970422":    print("!! bankBin mismatch"); ok=False
    print("VERDICT:", "OK — QR tro dung VA+BIN" if ok else "FAIL")

if __name__ == "__main__":
    main()
