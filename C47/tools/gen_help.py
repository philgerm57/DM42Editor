#!/usr/bin/env python3
"""Génère C47.Core/c47_help.tsv : catégorie et description de chaque instruction programmable.

Usage : python gen_help.py <dépôt c43> [fichier de sortie]

Source : docs/refdb47/data du dépôt C47 (items.utf8.tsv et menus.utf8.tsv).
La catégorie vient des menus de la calculatrice qui contiennent l'instruction ; la description
est la colonne doc_curated (RJvM, licence GFDL 1.3), en anglais.
"""
import re
import sys
from pathlib import Path

# menus de la calculatrice -> domaine affiché, dans l'ordre de priorité quand une instruction est dans plusieurs menus
DOMAINS = [
    ("Statistiques et probabilités", ["STAT", "REGR", "PLOT_STAT", "HIST", "PROB", "MODEL"]),
    ("Finances", ["FIN", "TVM", "AMORT", "TamRclTVM", "TamStoTVM"]),
    ("Nombres complexes", ["CPX"]),
    ("Matrices et vecteurs", ["MATX", "M_EDIT", "M_SIM_Q", "VECT", "VECCONV"]),
    ("Conversions d'unités", ["ConvA", "ConvAng", "ConvChef", "ConvE", "ConvFP", "ConvHum", "ConvM", "ConvMaterial", "ConvP", "ConvS",
                              "ConvSection", "ConvStruct", "ConvTemp", "ConvV", "ConvX", "ConvYmmv", "CONV_FLoad", "CONV_MLoad", "CONV_PLoad", "DRM"]),
    ("Trigonométrie et angles", ["TRG", "TRG_C47", "TRG_C47_MORE", "TRI", "AngleConv_43S"]),
    ("Solveur, intégrales, dérivées", ["ADV", "Solver_TOOL", "Sf_TOOL", "Sfdx", "1stDeriv", "2ndDeriv", "Dev", "EQN", "POLY"]),
    ("Fonctions spéciales", ["Ellipt", "Orthog", "XXFCNS"]),
    ("Entiers, bases et bits", ["BASE", "Base2", "BITS", "BITSET", "INTS", "NUMTHEORY"]),
    ("Graphiques", ["GRAPHS", "PLOTTING", "PLOTFUNC", "PLOT_LR", "PLOT_SCATR", "HPLOT"]),
    ("Date, heure et chronomètre", ["CLK", "Timer"]),
    ("Programmation", ["PFN_1", "PFN_2", "PFN_3", "MyPFN", "LOOP", "STRUCTPGM", "Inl_Tst", "KEYS", "TamLabel", "TamLabelOnly", "TamLocalLabel"]),
    ("Tests et indicateurs", ["TEST", "Flg", "FLAGS", "TamFlag", "TamCmp"]),
    ("Texte (ALPHA)", ["ALPHA", "alphaFN", "alphaMisc", "ALPHA_OMEGA", "alpha_omega"]),
    ("Affichage et modes", ["DISP", "MODE", "RMODE", "RNDMODES", "PREF", "GAP_L", "GAP_R", "GAP_RX", "RIBBONS", "Eim"]),
    ("Entrées, sorties et impression", ["IO", "PRINT", "Printer", "AUDIO", "INFO"]),
    ("Pile, registres et mémoire", ["STK", "Reg", "MULTSTK", "CLR", "DELETE", "TamSto", "TamRcl", "RESETS"]),
    ("Fonctions mathématiques", ["EXP", "XFN", "PARTS", "Misc", "HOME", "BLUE_C47", "PREFIX"]),
    ("Compatibilité HP-42S", ["42"]),
    ("Électrotechnique", ["EE"]),
]

# instructions absentes des menus fixes (menus dynamiques, touches) : rangées d'après leur nom interne
BY_ENUM = [
    (r"^(BINOM|CAUCH|EXPON|GEOM|HYPER|LGNRM|LOGIS|NBIN|NORML|POISS|WEIBL|GEV|PARETO2?|STDNORML|UNIFORM|DISUNIFORM)|^(FP?U?X|FM1P|FQX|FDQX|T(P?X|UX|M1P)|chi2.*|STO_[A-Z]_\w+)$", "Statistiques et probabilités"),
    (r"^EE_", "Électrotechnique"),
    (r"^RCL_(FV|PV|PMT|NPPER|IPonA|PPERonA|CPERonA)$", "Finances"),
    (r"^(i?PIn|i?SIGMAn|SIGMAnINF|PGMINT|PGMSLV|PGMPLT|PGMDRV|F[12]DRV|SOLVE|CALC)$", "Solveur, intégrales, dérivées"),
    (r"^(toPOL_CX|toREC_CX|op_a2?)$", "Nombres complexes"),
    (r"^(ACOS|ASIN|ATAN|toPOL_HP|toREC_HP)$", "Trigonométrie et angles"),
    (r"^(DET|INVRT|TRANS|MATX_A(_1)?|ENORM|RNORM|CNORM|NNZ|STKtoV3_\w|V3toSTK_\w|3x1TOSTK|STKTO3x1|M_CONCAT\w*|RCLVEL\w*|STOVEL\w*)$", "Matrices et vecteurs"),
    (r"^(STOMAX|RCLMAX|STOMIN|RCLMIN|RCLS|STOS|RCLCFG|STOCFG|N(DUP|SWAP|DROP)\w*)$", "Pile, registres et mémoire"),
    (r"^(KEY|42KEY|N_KEY_\w+|FROM_USER|TO_USER|BEGINP|ENDP|NOP|STOP|FOR\w*x|NEXTx)$", "Programmation"),
    (r"^(MSG\w*|OFF|AIM|SHOW|CLLCD_XY|HIDE\w*|SCR\w+|FGLNOFF|RMODE|S06|U06)$", "Affichage et modes"),
    (r"^(PLOT_REV|PLTf|YY_\w+|PLOTTING|RNG\w*)$", "Graphiques"),
    (r"^(caseUP|caseDN|XtoALPHA\w*|PRINTER\w+)$", "Texte (ALPHA)"),
    (r"^CONSTpi$", "Constantes"),
    (r"^(toINT|toREAL|toHR\w*|IDIVR|ABS|SQRT|CC|numL|numU|3R3P|BESTF\w*|DENMAX\w*|SETSIG\w*)$", "Fonctions mathématiques"),
]

# instructions qui ne sont dans aucun menu : elles ont leur propre touche
KEYS_STACK = {"ITM_ENTER", "ITM_XexY", "ITM_STO", "ITM_STOADD", "ITM_STOSUB", "ITM_STOMULT", "ITM_STODIV",
              "ITM_RCL", "ITM_RCLADD", "ITM_RCLSUB", "ITM_RCLMULT", "ITM_RCLDIV", "ITM_Rdown", "ITM_Rup", "ITM_LASTX", "ITM_CLX", "ITM_DROP"}
KEYS_MATH = {"ITM_ADD", "ITM_SUB", "ITM_MULT", "ITM_DIV", "ITM_CHS", "ITM_1ONX", "ITM_LN", "ITM_LOG10", "ITM_PI", "ITM_SQUAREROOTX", "ITM_SQUARE"}


def read(path):
    lines = [l.rstrip("\n") for l in path.open(encoding="utf-8") if not l.startswith("#")]
    head = lines[0].split("\t")
    return [dict(zip(head, l.split("\t"))) for l in lines[1:] if l]


def main():
    data = Path(sys.argv[1]) / "docs" / "refdb47" / "data"
    out = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(__file__).resolve().parents[1] / "C47.Core" / "c47_help.tsv"
    items = [r for r in read(data / "items.utf8.tsv") if r["variant"] == "C47" and "PTP_DISABLED" not in r["categories"]]
    menus = {}
    for r in read(data / "menus.utf8.tsv"):
        if r.get("kind") == "item" and r.get("item_opcode"):
            menus.setdefault(r["item_opcode"], set()).add(r["menu"])

    rank = {m: i for i, (_, ms) in enumerate(DOMAINS) for m in ms}
    counts, unknown = {}, set()
    with out.open("w", encoding="utf-8", newline="\n") as f:
        f.write("# Généré par tools/gen_help.py depuis docs/refdb47 de C47. Descriptions : doc_curated, RJvM, GNU FDL 1.3 ou ultérieure.\n")
        for r in items:
            op, enum = r["opcode"], r["enum_name"]
            ms = menus.get(op, set())
            unknown |= {m for m in ms if m not in rank and not m.startswith(("Tam", "alpha", "ASN", "CATALOG", "AIM", "EIM", "YESNO"))}
            # menus de commodité : le menu d'une conversion offre aussi FIX, celui d'électrotechnique x², etc.
            conv = r["fn_pointer"].startswith(("fnUnitConvert", "fnCvt"))
            known = sorted(rank[m] for m in ms if m in rank and (DOMAINS[rank[m]][0] != "Conversions d'unités" or conv)
                           and (DOMAINS[rank[m]][0] != "Électrotechnique" or enum.startswith("ITM_EE_")))
            if known:
                cat = DOMAINS[known[0]][0]
            elif any(re.search(rx, enum[4:]) for rx, _ in BY_ENUM):
                cat = next(c for rx, c in BY_ENUM if re.search(rx, enum[4:]))
            elif enum.startswith("CST_"):
                cat = "Constantes"
            elif enum in KEYS_STACK:
                cat = "Pile, registres et mémoire"
            elif enum in KEYS_MATH:
                cat = "Fonctions mathématiques"
            else:
                cat = "Autres"
            desc = r.get("doc_curated", "")
            if len(desc) > 1 and desc[0] == '"' and desc[-1] == '"':  # champ entre guillemets à la manière CSV
                desc = desc[1:-1].replace('""', '"')
            desc = " ".join(desc.split())
            counts[cat] = counts.get(cat, 0) + 1
            f.write(f"{op}\t{cat}\t{desc}\n")
    for c, n in sorted(counts.items(), key=lambda x: -x[1]):
        print(f"{n:5}  {c}")
    print("menus non classés :", ", ".join(sorted(unknown)))


main()
