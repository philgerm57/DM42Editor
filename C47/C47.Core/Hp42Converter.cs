using System.Text;
using System.Text.RegularExpressions;

namespace C47.Core;

/// <summary>
/// Conversion d'un listing HP-42S / DM42 (Free42, Py42) en source C47. Les instructions ALPHA passent par les versions
/// de compatibilité 42α, 42ARCL, 42AVIEW… (sur la C47, une ligne ‘texte’ pousse le texte dans X au lieu de remplir ALPHA) ;
/// les autres noms prennent leur équivalent C47 (+/- → CHS, →RAD → deg→rad, INVRT → [M]⁻¹…).
/// </summary>
public static class Hp42Converter
{
    public sealed record Result(string Source, IReadOnlyList<string> Warnings);

    /// <summary>Nom HP-42S → écriture C47 (une ou plusieurs instructions séparées par des espaces ou des retours à la ligne).</summary>
    static readonly Dictionary<string, string> names = new(StringComparer.Ordinal)
    {
        // ALPHA : versions compatibles HP-42S (registre K)
        ["ARCL"] = "42ARCL", ["ASTO"] = "42ASTO", ["AROT"] = "42AROT", ["ASHF"] = "42ASHF", ["AIP"] = "42AIP",
        ["ALENG"] = "42ALENG", ["ATOX"] = "42ATOX", ["XTOA"] = "42XTOA", ["AVIEW"] = "42AVIEW", ["CLA"] = "42CLA",
        ["POSA"] = "42POSA", ["KEY"] = "42KEY", ["PRA"] = "42PRA", ["PROMPT"] = "42PRMPT", ["VARMENU"] = "42VRMNU", ["DIM?"] = "42DIM#",
        // calcul
        ["+/-"] = "CHS", ["N!"] = "x!", ["GAMMA"] = "Γ(x)", ["E↑X-1"] = "ⅇˣ-1", ["LN1+X"] = "LN(1+x)",
        ["E↑X"] = "ⅇˣ", ["10↑X"] = "10ˣ", ["X↑2"] = "x²", ["Y↑X"] = "yˣ",
        ["ACOSH"] = "arcosh", ["ASINH"] = "arsinh", ["ATANH"] = "artanh",
        ["RAN"] = "RAN#", ["RND"] = "ROUND", ["%CH"] = "Δ%",
        ["→RAD"] = "deg→rad", ["→DEG"] = "rad→deg", ["→HR"] = "HMS→", ["→HMS"] = "HR→",
        // HMS+ / HMS- : passage en heures décimales, opération, retour en H.MS
        ["HMS+"] = "HMS→ x⇄y HMS→ + HR→", ["HMS-"] = "HMS→ x⇄y HMS→ x⇄y - HR→",
        // matrices
        ["INVRT"] = "[M]⁻¹", ["DET"] = "|M|", ["TRANS"] = "[M]⊤", ["UVEC"] = "UNITV", ["FNRM"] = "ENORM", ["RNRM"] = "RNORM",
        // statistiques : modèle courant de la C47 (L.R.) pour les estimations
        ["MEAN"] = "x̄", ["SDEV"] = "s", ["WMEAN"] = "x̄_w", ["FCSTX"] = "x̂", ["FCSTY"] = "ŷ",
        ["YINT"] = "0 ŷ", ["SLOPE"] = "1 ŷ 0 ŷ -",
        // types, modes, pile, registres
        ["CPX?"] = "ℂ?", ["REAL?"] = "ℝ?", ["MAT?"] = "MATR?", ["STR?"] = "STRI?",
        ["REALRES"] = "CF ‘CPXRES’", ["CPXRES"] = "SF ‘CPXRES’",
        ["RDX."] = "RPER.", ["RDX,"] = "RCOM,",
        ["CLST"] = "CLSTK", ["CLRG"] = "CLREGS", ["CLV"] = "DELITM", ["PSE"] = "PAUSE 10", ["GETKEY"] = "KEY?",
        ["BIT?"] = "BS?", ["BASE+"] = "+", ["BASE-"] = "-", ["BASE×"] = "×", ["BASE÷"] = "÷", ["BASE+/-"] = "CHS",
        ["HEXM"] = "HEX", ["OCTM"] = "OCT", ["DECM"] = "DEC", ["BINM"] = "BIN",
        // impression
        ["PRX"] = "⎙x", ["PRSTK"] = "⎙STK", ["PRΣ"] = "⎙Σ", ["PRLCD"] = "⎙LCD", ["ADV"] = "⎙ADV", ["PRV"] = "⎙r",
        ["DELAY"] = "⎙DLAY",
    };

    /// <summary>Instructions HP-42S sans équivalent utile : retirées, avec un avertissement.</summary>
    static readonly Dictionary<string, string> dropped = new(StringComparer.Ordinal)
    {
        ["SIZE"] = "la C47 a 100 registres et des registres locaux (LocR) : SIZE est inutile",
        ["ALLΣ"] = "la C47 garde toujours toutes les sommes statistiques",
        ["LINΣ"] = "la C47 garde toujours toutes les sommes statistiques",
        ["LCLBL"] = "les étiquettes locales n'ont pas besoin d'être déclarées sur la C47",
        ["PRON"] = "réglez l'impression dans le menu ⎙ de la C47",
        ["PROFF"] = "réglez l'impression dans le menu ⎙ de la C47",
        ["ΣREG"] = "la C47 range les statistiques dans la matrice STATS, pas dans des registres",
    };

    public static Result Convert(string hp42)
    {
        var o = new StringBuilder();
        var warnings = new List<string>();
        int n = 0;
        foreach (var raw in hp42.Replace("\r\n", "\n").Split('\n'))
        {
            n++;
            string line = Regex.Replace(raw.Trim(), @"^\d{2,4}[▸ ]\s*", "");   // numéro de pas éventuel
            if (line == "" ) { o.Append("\r\n"); continue; }
            // en-tête { 123-Byte Prgm } : gardé en commentaire, une ligne de sortie par ligne lue (synchronisation avec Py42)
            if (line.StartsWith("{")) { o.Append("@ ").Append(line).Append("  → C47\r\n"); continue; }
            if (line.StartsWith("@") || line.StartsWith(";")) { o.Append(line).Append("\r\n"); continue; }
            o.Append("  ").Append(Step(line, n, warnings)).Append("\r\n");
        }
        return new Result(o.ToString().TrimEnd() + "\r\n", warnings);
    }

    static string Step(string line, int n, List<string> warnings)
    {
        // textes ALPHA : "ABC" remplit ALPHA, ⊢"ABC" (├"ABC") y ajoute
        var alpha = Regex.Match(line, "^([⊢├]?)\"(.*)\"$");
        if (alpha.Success) return (alpha.Groups[1].Value == "" ? "42α ‘" : "42⊢ ‘") + alpha.Groups[2].Value + "’";

        // « ST X » : registres de pile écrits sans ST ; "NOM" → ‘NOM’
        line = Regex.Replace(line, @"\bST ([XYZTL])\b", "$1");
        line = Regex.Replace(line, "\"([^\"]*)\"", "‘$1’");

        var m = Regex.Match(line, @"^(\S+)(.*)$");
        string name = m.Groups[1].Value, rest = m.Groups[2].Value;
        if (dropped.TryGetValue(name, out var why))
        {
            warnings.Add($"ligne {n} : {name} retiré ({why})");
            return "@ " + line + "   (HP-42S)";
        }
        if (names.TryGetValue(name, out var c47)) return c47 + rest;
        return line;
    }
}
