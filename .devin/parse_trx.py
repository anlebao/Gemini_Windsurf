import re, glob, sys

files = glob.glob(r'6_Tests/VanAn.Core.Tests/TestResults/imp.trx')
if not files:
    print('no trx found')
    sys.exit(0)
s = open(files[0], encoding='utf-8').read()
for m in re.finditer(r'<UnitTestResult[^>]*outcome="Failed"[^>]*>.*?</UnitTestResult>', s, re.S):
    name = re.search(r'testName="([^"]+)"', m.group(0))
    err = re.search(r'<Message>(.*?)</Message>', m.group(0), re.S)
    print('FAILED:', name.group(1) if name else '?')
    if err:
        print('  MSG:', err.group(1)[:600].replace('\n', ' '))
    print()
