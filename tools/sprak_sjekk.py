# -*- coding: utf-8 -*-
# Finner alle L.T(...) / L.F(...) / Tip(...) nøkler i kildekoden og
# sammenligner mot tabellen i Lang.cs. Skriver ut det som mangler.
import io, os, re, sys

ROT = r"C:\Users\Mathias\Desktop\Vaktmester"
LANG = os.path.join(ROT, "src", "Lang.cs")

STR = r'"((?:[^"\\]|\\.)*)"'
PATTERNS = [
    re.compile(r'\bL\.T\(\s*' + STR),
    re.compile(r'\bL\.F\(\s*' + STR),
    re.compile(r'\bTip\(\s*[A-Za-z_][A-Za-z0-9_]*\s*,\s*' + STR),
]


def unescape(s):
    return (s.replace('\\"', '"').replace('\\r', '\r')
             .replace('\\n', '\n').replace('\\t', '\t').replace('\\\\', '\\'))


# --- nøkler fra tabellen ---
lang_src = io.open(LANG, encoding="utf-8").read()
start = lang_src.index("static readonly string[] Pairs =")
body = lang_src[start:]
literals = [unescape(m) for m in re.findall(STR, body)]
keys = set(literals[0::2])          # annenhver er nøkkel

# --- nøkler brukt i koden ---
used = {}
for sub in ("src", "installer"):
    d = os.path.join(ROT, sub)
    for f in sorted(os.listdir(d)):
        if not f.endswith(".cs") or f == "Lang.cs":
            continue
        path = os.path.join(d, f)
        text = io.open(path, encoding="utf-8").read()
        for pat in PATTERNS:
            for m in pat.finditer(text):
                k = unescape(m.group(1))
                used.setdefault(k, set()).add(f)

mangler = sorted(k for k in used if k not in keys)
ubrukt = sorted(k for k in keys if k not in used)

print("Nøkler i tabellen : %d" % len(keys))
print("Nøkler brukt i kode: %d" % len(used))
print()

if mangler:
    print("=== MANGLER ENGELSK (%d) ===" % len(mangler))
    for k in mangler:
        print('            "%s", "",   // %s' % (
            k.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n").replace("\r", "\\r"),
            ", ".join(sorted(used[k]))))
else:
    print("Ingen manglende oversettelser.")

print()
if ubrukt:
    print("=== I TABELLEN, MEN IKKE BRUKT DIREKTE (%d) ===" % len(ubrukt))
    print("   (kan være dynamiske: kategorinavn, helsestatus, merknader)")
    for k in ubrukt[:80]:
        print("   " + k[:70])

sys.exit(1 if mangler else 0)
