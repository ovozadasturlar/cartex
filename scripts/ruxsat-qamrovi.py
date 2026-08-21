import re, io, glob, collections

perm_file = 'src/backend/Cartex.Domain/Authorization/Permissions.cs'
src = io.open(perm_file, encoding='utf-8').read()
consts = {}
cls = None
for line in src.splitlines():
    m = re.search(r'public static class (\w+)', line)
    if m:
        cls = m.group(1)
    m = re.search(r'public const string (\w+)\s*=\s*"([^"]+)"', line)
    if m and cls:
        consts[cls + '.' + m.group(1)] = m.group(2)

used = collections.Counter()
for f in glob.glob('src/backend/Cartex.Api/Controllers/*.cs'):
    s = io.open(f, encoding='utf-8').read()
    for m in re.finditer(r'HasPermission\(([^)]*)\)', s):
        for part in m.group(1).split(','):
            part = part.strip().replace('AppPermissions.', '')
            if part in consts:
                used[consts[part]] += 1

clients = {
    'desktop': 'src/desktop/Cartex.UI',
    'web': 'src/web/Cartex.Web.Angular/src',
    'store': 'src/mobile/Cartex.Mobile.Store',
    'agent': 'src/mobile/Cartex.Mobile.Agent',
}
text = {}
for name, d in clients.items():
    buf = []
    for ext in ('*.cs', '*.axaml', '*.xaml', '*.ts', '*.html'):
        for f in glob.glob(d + '/**/' + ext, recursive=True):
            n = f.replace('\\', '/')
            if '/obj/' in n or '/bin/' in n:
                continue
            buf.append(io.open(f, encoding='utf-8', errors='ignore').read())
    text[name] = '\n'.join(buf)

missing = [p for p in sorted(used) if not any(p in t for t in text.values())]
print('Server permissions no client ever checks (%d of %d):' % (len(missing), len(used)))
for p in missing:
    print('  ', p)
