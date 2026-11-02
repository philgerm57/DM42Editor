#!/usr/bin/env python3
"""Ajoute la description française à C47.Core/c47_help.tsv (4e colonne).

Usage : python translate_help.py

Les descriptions répétitives (conversions d'unités, constantes, lois de probabilité, fonctions en précision étendue)
sont traduites par règles ; les autres viennent de help_fr.tsv (anglais TAB français), tenu à la main.
Une description absente des deux reste en anglais et est listée dans help_fr_manquant.txt.
"""
import re
from pathlib import Path

HERE = Path(__file__).resolve().parent
HELP = HERE.parent / "C47.Core" / "c47_help.tsv"
MANUAL = HERE / "help_fr.tsv"
MISSING = HERE / "help_fr_manquant.txt"

UNITS = {
    "1x2 vector": "vecteur 1x2", "Banana length": "longueur de banane", "British Thermal Unit": "British Thermal Unit (BTU)",
    "Centimetre": "centimètre", "Joule": "joule", "Kelvin": "kelvin", "MegaPascal": "mégapascal", "Newton": "newton",
    "Newton per metre": "newton par mètre", "Newton-metre": "newton-mètre", "Pascal": "pascal", "UK fluid ounce": "once liquide britannique",
    "UK gallon": "gallon britannique", "UK horsepower": "cheval-vapeur britannique", "UK imperial cup": "tasse impériale britannique",
    "UK imperial pint": "pinte impériale britannique", "UK imperial tablespoon": "cuillère à soupe impériale britannique",
    "UK imperial teaspoon": "cuillère à café impériale britannique", "UK quart": "quart britannique", "US acre": "acre US",
    "US customary cup": "tasse US", "US customary tablespoon": "cuillère à soupe US", "US customary teaspoon": "cuillère à café US",
    "US fluid ounce": "once liquide US", "US gallon": "gallon US", "US liquid pint": "pinte liquide US", "US quart": "quart US",
    "US survey foot": "pied d'arpentage US", "Watt": "watt", "Watt-hour": "wattheure", "acre": "acre", "astronomical unit": "unité astronomique",
    "atmosphere": "atmosphère", "bar": "bar", "barrel": "baril", "beardsecond": "beard-seconde", "blob per cubic inch": "blob par pouce cube",
    "calorie": "calorie", "carat": "carat", "chĭ": "chĭ", "cubic foot": "pied cube", "cubic inch": "pouce cube", "cubic metre": "mètre cube",
    "cubic millimetre": "millimètre cube", "cùn": "cùn", "decibel": "décibel", "decimal hours": "heures décimales",
    "degree per second": "degré par seconde", "degrees Celsius": "degrés Celsius", "degrees Fahrenheit": "degrés Fahrenheit",
    "degrees Rankine": "degrés Rankine", "electrical horsepower": "cheval-vapeur électrique", "electronvolt": "électronvolt",
    "electronvolt/Boltzmann constant": "électronvolt/constante de Boltzmann", "erg": "erg", "fathom": "brasse", "field ratio": "rapport de champ",
    "firkin": "firkin", "foe": "foe", "foot": "pied", "foot per second": "pied par seconde", "foot per second squared": "pied par seconde carrée",
    "foot-pound force": "pied-livre force", "fortnight": "quinzaine", "furlong": "furlong", "furlong per fortnight": "furlong par quinzaine",
    "fēn": "fēn", "gram": "gramme", "hectare": "hectare", "hundredweight": "quintal britannique", "inch": "pouce",
    "inch of Mercury": "pouce de mercure", "inch per second squared": "pouce par seconde carrée",
    "inch to the power 4 to millimetre": "pouce puissance 4 en millimètre", "inch to the power 6 to millimetre": "pouce puissance 6 en millimètre",
    "inch-pound force": "pouce-livre force", "jīn": "jīn", "kilo-pound force per square inch": "kilo-livre force par pouce carré",
    "kiloWatt-hour per 100 kilometre": "kilowattheure aux 100 kilomètres", "kiloWatt-hour per 100 mile": "kilowattheure aux 100 miles",
    "kilogram": "kilogramme", "kilogram force": "kilogramme-force", "kilogram force per metre": "kilogramme-force par mètre",
    "kilogram per cubic metre": "kilogramme par mètre cube", "kilogram per metre": "kilogramme par mètre", "kilometre": "kilomètre",
    "kilometre per hour": "kilomètre par heure", "kilometre per kiloWatt-hour": "kilomètre par kilowattheure",
    "kilometre per litre": "kilomètre par litre", "kilometre per litre equivalent": "kilomètre par litre équivalent", "knot": "nœud",
    "light year": "année-lumière", "litre": "litre", "litre per 100 kilometre": "litre aux 100 kilomètres", "liăng": "liăng",
    "long ton": "tonne longue", "lĭ": "lĭ", "metre": "mètre", "metre per second": "mètre par seconde",
    "metre per second squared": "mètre par seconde carrée", "metric horsepower": "cheval-vapeur métrique", "metric tonne": "tonne",
    "metric tonne per cubic millimetre": "tonne par millimètre cube", "mile": "mile", "mile per gallon UK": "mile par gallon britannique",
    "mile per gallon US": "mile par gallon US", "mile per hour": "mile par heure", "mile per kiloWatt-hour": "mile par kilowattheure",
    "millilitre": "millilitre", "millimetre": "millimètre", "millimetre of Mercury": "millimètre de mercure",
    "millimetre to the power 4 to inch": "millimètre puissance 4 en pouce", "millimetre to the power 6 to inch": "millimètre puissance 6 en pouce",
    "mpg gasoline equivalent UK": "mpg équivalent essence britannique", "mpg gasoline equivalent US": "mpg équivalent essence US", "mŭ": "mŭ",
    "nautical mile": "mille marin", "ounce": "once", "parsec": "parsec", "point": "point", "pound": "livre", "pound force": "livre-force",
    "pound force per foot": "livre-force par pied", "pound force per square foot": "livre-force par pied carré",
    "pound force per square inch": "livre-force par pouce carré", "pound per cubic inch": "livre par pouce cube", "pound per foot": "livre par pied",
    "power ratio": "rapport de puissance", "radian per second": "radian par seconde", "rotation per minute": "tour par minute",
    "second": "seconde", "sexagesimal format hours": "heures au format sexagésimal", "short hundredweight": "quintal court (US)",
    "short ton": "tonne courte", "square foot": "pied carré", "square inch": "pouce carré", "square kilometre": "kilomètre carré",
    "square metre": "mètre carré", "square mile": "mile carré", "square millimetre": "millimètre carré", "square nautical mile": "mille marin carré",
    "stone": "stone", "the power 4": "la puissance 4", "the power 6": "la puissance 6", "torr": "torr", "troy ounce": "once troy",
    "yard": "yard", "year": "année", "yĭn": "yĭn", "zhàng": "zhàng", "ℂomplex number": "nombre ℂomplexe",
}

DISTRIBUTIONS = {
    "Binomial": "binomiale", "Cauchy": "de Cauchy", "Chi-squared": "du khi-deux", "Discrete uniform": "uniforme discrète",
    "Continuous uniform": "uniforme continue", "Exponential": "exponentielle", "F": "F de Fisher", "Geometric": "géométrique",
    "Hypergeometric": "hypergéométrique", "Log-normal": "log-normale", "Logistic": "logistique", "Negative binomial": "binomiale négative",
    "Normal": "normale", "Standard normal": "normale centrée réduite", "Poisson": "de Poisson", "Student's t": "de Student",
    "Weibull": "de Weibull", "Generalized extreme value": "des valeurs extrêmes généralisée", "Pareto type I": "de Pareto type I",
    "Pareto type II": "de Pareto type II", "Fisher's F": "F de Fisher", "Generalised extreme value": "des valeurs extrêmes généralisée",
    "Log normal": "log-normale", "Uniform": "uniforme",
}
DIST_KINDS = [
    ("cumulative distribution (lower tail)", "Loi {d} : fonction de répartition (queue inférieure)"),
    ("cumulative distribution (upper tail)", "Loi {d} : fonction de répartition (queue supérieure)"),
    ("inverse cumulative distribution (quantile)", "Loi {d} : fonction de répartition inverse (quantile)"),
    ("probability density function", "Loi {d} : densité de probabilité"),
    ("probability mass function", "Loi {d} : fonction de masse"),
    ("upper cumulative distribution function", "Loi {d} : fonction de répartition supérieure"),
    ("lower cumulative distribution function", "Loi {d} : fonction de répartition inférieure"),
    ("cumulative distribution function", "Loi {d} : fonction de répartition"),
]


def by_rule(en):
    m = re.fullmatch(r"Constant # (\d+) (.*)", en)
    if m:
        return f"Constante n° {m.group(1)} {m.group(2)}"
    m = re.fullmatch(r"Convert ([^,;:()]+) to ([^,;:()]+)", en)
    if m and m.group(1) in UNITS and m.group(2) in UNITS:
        return f"Convertit {UNITS[m.group(1)]} en {UNITS[m.group(2)]}"
    for kind, fr in DIST_KINDS:
        if en.endswith(" " + kind):
            d = en[: -len(kind) - 1]
            if d in DISTRIBUTIONS:
                return fr.format(d=DISTRIBUTIONS[d])
    return None


def main():
    manual = {}
    if MANUAL.exists():
        for line in MANUAL.read_text(encoding="utf-8").splitlines():
            if "\t" in line and not line.startswith("#"):
                en, fr = line.split("\t", 1)
                manual[en] = fr
    out, missing = [], []
    for line in HELP.read_text(encoding="utf-8").splitlines():
        if line.startswith("#"):
            out.append(line)
            continue
        f = line.split("\t")
        en = f[2] if len(f) > 2 else ""
        fr = "" if en == "" else manual.get(en) or by_rule(en)
        if en and not fr:
            missing.append(en)
            fr = ""
        out.append("\t".join(f[:3] + [fr]))
    HELP.write_text("\n".join(out) + "\n", encoding="utf-8", newline="\n")
    MISSING.write_text("\n".join(sorted(set(missing))) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(set(missing))} descriptions sans traduction -> {MISSING.name}")


main()
