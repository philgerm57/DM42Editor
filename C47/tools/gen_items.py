#!/usr/bin/env python3
"""Génère C47.Core/c47_items.tsv à partir des sources du firmware C47.

Usage : python gen_items.py <dépôt c43> [fichier de sortie]
À relancer quand le firmware ajoute ou renomme des instructions.

Colonnes : numéro, type de paramètre (PTP_ sans préfixe), valeur max TAM,
nom catalogue, nom menu (en Unicode, les numéros suffisent à l'encodage),
nom complet des conversions (vide sinon).
"""
import re
import sys
from pathlib import Path


def strip_comments(s):
    return re.sub(r"/\*.*?\*/", " ", s)


def split_args(s):
    """Découpe sur les virgules de premier niveau, hors chaînes."""
    args, cur, depth, q = [], "", 0, False
    i = 0
    while i < len(s):
        c = s[i]
        if q:
            cur += c
            if c == "\\":
                cur += s[i + 1]
                i += 1
            elif c == '"':
                q = False
        elif c == '"':
            q = True
            cur += c
        elif c in "({":
            depth += 1
            cur += c
        elif c in ")}":
            depth -= 1
            cur += c
        elif c == "," and depth == 0:
            args.append(cur.strip())
            cur = ""
        else:
            cur += c
        i += 1
    args.append(cur.strip())
    return args


def c_bytes(lit):
    out = bytearray()
    i = 0
    while i < len(lit):
        c = lit[i]
        if c == "\\":
            i += 1
            e = lit[i]
            if e == "x":
                m = re.match(r"[0-9a-fA-F]{1,2}", lit[i + 1:])
                out.append(int(m.group(0), 16))
                i += len(m.group(0))
            else:
                out.append({"n": 10, "t": 9, "0": 0, '"': 34, "\\": 92}.get(e, ord(e)))
        else:
            out += c.encode("utf-8")
        i += 1
    return bytes(out)


class Macros:
    def __init__(self, text):
        self.defs = {}
        for m in re.finditer(r"^[ \t]*#define[ \t]+(\w+)[ \t]+(\"[^\n]*|[A-Z]\w*(?:[ \t]+[A-Z\"]\w*[^\n]*)?)$", text, re.M):
            self.defs.setdefault(m.group(1), strip_comments(re.sub(r"//.*", "", m.group(2))).strip())

    def expand(self, expr, depth=0):
        """Évalue une concaténation de chaînes C et de macros ; None si impossible."""
        out = b""
        pos = 0
        expr = expr.strip()
        while pos < len(expr):
            c = expr[pos]
            if c.isspace():
                pos += 1
            elif c == '"':
                m = re.compile(r'"((?:[^"\\]|\\.)*)"').match(expr, pos)
                out += c_bytes(m.group(1))
                pos = m.end()
            else:
                m = re.compile(r"\w+").match(expr, pos)
                if not m or depth > 8 or m.group(0) not in self.defs:
                    return None
                sub = self.expand(self.defs[m.group(0)], depth + 1)
                if sub is None:
                    return None
                out += sub
                pos = m.end()
        return out


def to_unicode(b):
    """Même conversion que stringToUtf8() : 2 octets si le bit de poids fort est présent."""
    s, i = "", 0
    while i < len(b):
        if b[i] & 0x80:
            s += chr((b[i] & 0x7f) * 256 + b[i + 1])
            i += 2
        else:
            s += chr(b[i])
            i += 1
    return s


def main():
    root = Path(sys.argv[1])
    out = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(__file__).resolve().parents[1] / "C47.Core" / "c47_items.tsv"
    src = root / "src" / "c47"
    items_c = (src / "items.c").read_text(encoding="utf-8", errors="replace")
    fonts = (src / "fonts.h").read_text(encoding="utf-8", errors="replace")
    macros = Macros(fonts + "\n" + items_c)

    headers = "\n".join(p.read_text(encoding="utf-8", errors="replace") for p in src.glob("*.h"))

    def number(word):
        """Valeur max TAM : un nombre ou une macro simple (DSP_MAX) ; 0 si inconnue."""
        for _ in range(5):
            if word.isdigit():
                return int(word)
            m = re.search(r"#define\s+%s\s+(\w+)" % re.escape(word), headers)
            if not m:
                return 0
            word = m.group(1)
        return 0

    start = items_c.index("indexOfItems[] = {")
    rows, problems = {}, []
    for line in items_c[start:].splitlines():
        m = re.match(r"\s*/\*\s*(\d+)\s*\*/\s*(.*)$", line)
        if not m:
            continue
        num = int(m.group(1))
        body = strip_comments(re.sub(r"//.*$", "", m.group(2))).strip().rstrip(",").strip()
        if body.startswith("UNIT_CONV("):
            a = split_args(body[len("UNIT_CONV("):-1])
            cat, menu, ptp, tam = a[2], a[3], "PTP_NONE", "0"
        elif body.startswith("{"):
            a = split_args(body[1:body.rindex("}")])
            cat, menu, tam = a[2], a[3], a[4]
            p = re.search(r"PTP_\w+", a[5] if len(a) > 5 else "")
            if not p:
                problems.append(f"{num}: pas de PTP")
                continue
            ptp = p.group(0)
        else:
            problems.append(f"{num}: forme inconnue {body[:50]}")
            continue
        cb, mb = macros.expand(cat), macros.expand(menu)
        if cb is None or mb is None:
            problems.append(f"{num}: nom non évalué {cat} / {menu}")
            cb, mb = cb or b"", mb or b""
        t = re.search(r"\|\s*(\w+)\s*$", tam)
        rows[num] = (ptp[4:], number(t.group(1)) if t else 0, to_unicode(cb), to_unicode(mb), "")

    names = {}
    h = (src / "items.h").read_text(encoding="utf-8", errors="replace")

    # nom complet d'une conversion : fullConvSoftMenuItemNameInclHPCONV(), gauche→droite
    conv = (src / "conversionUnits.c").read_text(encoding="utf-8", errors="replace")
    itm = {m.group(1): int(m.group(2)) for m in re.finditer(r"#define\s+(ITM_\w+)\s+(\d+)", h)}
    pairs = conv[conv.index("convertPairs[NUM_CONVERT_PAIRS] = {"):]
    for m in re.finditer(r"\{\s*(ITM_\w+)\s*/\*.*?\*/\s*,\s*(ITM_\w+)", pairs):
        a, b = itm.get(m.group(1)), itm.get(m.group(2))
        if a in rows and b in rows:
            r = rows[a]
            rows[a] = (r[0], r[1], r[2], r[3], r[3].split("→")[0] + "→" + rows[b][3].split("→")[0])

    for n in ["ITM_LITERAL", "ITM_END", "ITM_KEY", "ITM_42KEY", "ITM_REG_X", "ITM_REG_M", "SFL_TDM24", "SFL_MONIT", "FIRST_CONSTANT", "LAST_CONSTANT",
              "ITM_op_j", "ITM_op_j_pol", "ITM_BESTF_OLD", "ITM_RNG_OLD", "ITM_YY_DFLT_OLD", "ITM_DENMAX2_OLD", "LAST_ITEM"]:
        m = re.search(r"#define\s+%s\s+(\S+)" % n, h)
        v = m.group(1) if m else ""
        while v and not v.isdigit():
            m = re.search(r"#define\s+%s\s+(\S+)" % re.escape(v), h)
            v = m.group(1) if m else ""
        names[n] = v

    # exposants et indices : la police C47 les place hors de leur code Unicode (STD_SUP_2 en U+2162)
    sup = dict(zip("0123456789", "⁰¹²³⁴⁵⁶⁷⁸⁹")) | dict(zip("abcdefghijklmnoprstuvwxyz", "ᵃᵇᶜᵈᵉᶠᵍʰⁱʲᵏˡᵐⁿᵒᵖʳˢᵗᵘᵛʷˣʸᶻ")) | {"T": "ᵀ", "O": "ᴼ", "P": "ᴾ", "MINUS": "⁻", "PLUS": "⁺"}
    below = dict(zip("0123456789", "₀₁₂₃₄₅₆₇₈₉")) | dict(zip("aehijklmnoprstuvx", "ₐₑₕᵢⱼₖₗₘₙₒₚᵣₛₜᵤᵥₓ")) | {"MINUS": "₋", "PLUS": "₊"}
    glyphs = {}
    for m in re.finditer(r'#define\s+STD_(SUP|SUB)_(\w+)\s+"\\x([0-9a-f]{2})\\x([0-9a-f]{2})"', fonts):
        ch = (sup if m.group(1) == "SUP" else below).get(m.group(2))
        if ch:
            code = ((int(m.group(3), 16) & 0x7f) << 8) | int(m.group(4), 16)
            if code != ord(ch):
                glyphs.setdefault(code, ch)

    out.parent.mkdir(parents=True, exist_ok=True)
    with out.open("w", encoding="utf-8", newline="\n") as f:
        for k, v in names.items():
            f.write(f"!{k}\t{v}\n")
        for code, ch in sorted(glyphs.items()):
            f.write(f"~{code:X}\t{ch}\n")
        for n in sorted(rows):
            r = rows[n]
            f.write(f"{n}\t{r[0]}\t{r[1]}\t{r[2]}\t{r[3]}\t{r[4]}\n")
    prog = sum(1 for r in rows.values() if r[0] != "DISABLED")
    print(f"{len(rows)} lignes, {prog} programmables -> {out}")
    print("constantes:", names)
    print(f"{len(problems)} problème(s)")
    for p in problems[:40]:
        print("  ", p)


main()
