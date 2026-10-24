using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace dm42editor
{
    /// <summary>
    /// Convertit l'écriture HP-41C / CV / CX vers celle acceptée par l'encodeur DM42 / Free42 :
    /// noms d'instructions (INT → IP, CHS → +/-, ST+ → STO+…), registres de pile (RCL X → RCL ST X),
    /// arguments sur deux chiffres (FIX 4 → FIX 04), nombres (.5 → 0.5), ajout à ALPHA ("|-AB" → ⊢"AB").
    /// Numéros de pas et commentaires sont conservés.
    /// </summary>
    static class Hp41Converter
    {
        public sealed class Change
        {
            public int Line;          // ligne de l'éditeur (0-based)
            public string Before, After, Note;
            public bool Warning;      // pas d'équivalent : laissé tel quel
        }

        /// <summary>Instructions renommées sur la HP-42S (sans argument ou avec le même argument).</summary>
        static readonly Dictionary<string, (string To, string Note)> Ops = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["INT"] = ("IP", "partie entière"),
            ["FRC"] = ("FP", "partie fractionnaire"),
            ["ENTER^"] = ("ENTER", null),
            ["ENTER↑"] = ("ENTER", null),
            ["CHS"] = ("+/-", "changement de signe"),
            ["RDN"] = ("R↓", null),
            ["X#Y?"] = ("X≠Y?", null),
            ["X#0?"] = ("X≠0?", null),
            ["*"] = ("×", null),
            ["/"] = ("÷", "sinon encodé comme +/- !"),
            ["P-R"] = ("→REC", null),
            ["R-P"] = ("→POL", null),
            ["D-R"] = ("→RAD", null),
            ["R-D"] = ("→DEG", null),
            ["HMS"] = ("→HMS", null),
            ["HR"] = ("→HR", null),
            ["OCT"] = ("→OCT", null),
            ["DEC"] = ("→DEC", null),
            ["CLS"] = ("CLΣ", null),
            ["FACT"] = ("N!", null),
            ["ST+"] = ("STO+", null),
            ["ST-"] = ("STO-", null),
            ["ST*"] = ("STO×", null),
            ["ST/"] = ("STO÷", null),
            ["STO*"] = ("STO×", null),
            ["STO/"] = ("STO÷", null),
            ["PSIZE"] = ("SIZE", null),
        };

        /// <summary>Instructions dont l'argument est un registre : X, Y, Z, T, L y désignent la pile (ST X…).</summary>
        static readonly HashSet<string> RegisterOps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "STO", "RCL", "STO+", "STO-", "STO×", "STO÷", "RCL+", "RCL-", "RCL×", "RCL÷",
            "ISG", "DSE", "VIEW", "ARCL", "ASTO", "X<>"
        };

        /// <summary>Instructions dont l'argument numérique s'écrit sur deux chiffres (registre, drapeau, label, chiffres).</summary>
        static readonly HashSet<string> TwoDigitOps = new HashSet<string>(RegisterOps, StringComparer.OrdinalIgnoreCase)
        {
            "SF", "CF", "FS?", "FC?", "FS?C", "FC?C", "FIX", "SCI", "ENG", "GTO", "XEQ", "LBL", "ΣREG"
        };

        /// <summary>Instructions HP-41 sans équivalent sur la HP-42S : signalées, laissées telles quelles.</summary>
        static readonly Dictionary<string, string> NoEquivalent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PRP"] = "impression de programme : pas d'équivalent",
            ["LIST"] = "impression de programme : PRP de la HP-42 n'existe pas en programme",
            ["COPY"] = "copie de programme du module : pas d'équivalent",
            ["PACK"] = "pas d'équivalent (mémoire gérée automatiquement)",
            ["DEL"] = "édition de programme : pas d'équivalent",
            ["CAT"] = "catalogue : pas d'équivalent en programme",
            ["RCLFLAG"] = "pas d'équivalent : sauvegardez les drapeaux un par un (FS?)",
            ["STOFLAG"] = "pas d'équivalent : rétablissez les drapeaux un par un (SF / CF)",
            ["X<>F"] = "pas d'équivalent sur HP-42",
            ["SIZE?"] = "pas d'équivalent sur HP-42",
        };

        static readonly Regex StepRx = new Regex(@"^(\s*)(\d+)([•◆♦▪]\s*|\s+)");
        static readonly Regex NotEqual = new Regex(@"\bX#(Y|0)\?");
        static readonly Regex DotNumber = new Regex(@"^(-?)\.(\d+)((?:E-?\d+)?)$", RegexOptions.IgnoreCase);
        static readonly Regex BareExponent = new Regex(@"^(-?)E(-?\d+)$", RegexOptions.IgnoreCase);
        static readonly Regex AppendText = new Regex(@"^(?:""(?:\|-|├|⊢)(.*)""|>""(.*)"")$");

        /// <summary>Texte converti ; changes reçoit chaque modification (et les instructions sans équivalent).</summary>
        public static string Convert(string text, List<Change> changes)
        {
            var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i];
                if (s.Trim().Length == 0) continue;
                var notes = new List<string>();

                // numéro de pas ; la puce HP-41 (« 01•LBL ») devient une espace
                string prefix = "", body = s;
                var m = StepRx.Match(s);
                if (m.Success && m.Length < s.Length)
                {
                    bool bullet = m.Groups[3].Value.Trim().Length > 0;
                    prefix = m.Groups[1].Value + m.Groups[2].Value + (bullet ? " " : m.Groups[3].Value);
                    body = s.Substring(m.Length);
                    if (bullet) notes.Add("puce HP-41");
                }

                // X#Y? / X#0? : ce # n'est pas un commentaire
                string ne = NotEqual.Replace(body, "X≠$1?");
                if (ne != body) { notes.Add("X# → X≠"); body = ne; }

                Flowchart.FlowGraph.SplitComment(body, out string codePart, out _, out _);
                string code = codePart.Trim();
                string newBody = body;
                if (code.Length > 0 && !code.StartsWith("{"))
                {
                    string converted = ConvertCode(code, notes, out string warning);
                    if (warning != null)
                        changes.Add(new Change { Line = i, Before = s.Trim(), After = s.Trim(), Note = warning, Warning = true });
                    int at = codePart.IndexOf(code, StringComparison.Ordinal);
                    newBody = codePart.Substring(0, at) + converted + body.Substring(at + code.Length);
                }

                string newLine = prefix + newBody;
                if (newLine != s)
                {
                    changes.Add(new Change { Line = i, Before = s.Trim(), After = newLine.Trim(), Note = string.Join(" ; ", notes) });
                    lines[i] = newLine;
                }
            }
            return string.Join("\r\n", lines);
        }

        /// <summary>Convertit une instruction (sans numéro ni commentaire).</summary>
        static string ConvertCode(string code, List<string> notes, out string warning)
        {
            warning = null;

            // textes : ajout à ALPHA écrit "|-ABC", "├ABC" ou >"ABC" sur HP-41
            if (code.StartsWith("\"") || code.StartsWith(">\""))
            {
                var t = AppendText.Match(code);
                if (t.Success)
                {
                    notes.Add("ajout à ALPHA");
                    return "⊢\"" + (t.Groups[1].Success ? t.Groups[1].Value : t.Groups[2].Value) + "\"";
                }
                return code;
            }

            // nombres : .5 → 0.5, E3 → 1E3
            var d = DotNumber.Match(code);
            if (d.Success) { notes.Add("zéro avant la virgule"); return d.Groups[1].Value + "0." + d.Groups[2].Value + d.Groups[3].Value; }
            var ex = BareExponent.Match(code);
            if (ex.Success) { notes.Add("mantisse 1"); return ex.Groups[1].Value + "1E" + ex.Groups[2].Value; }

            int sp = code.IndexOf(' ');
            string op = sp < 0 ? code : code.Substring(0, sp);
            string arg = sp < 0 ? "" : Regex.Replace(code.Substring(sp + 1).Trim(), @"\s+", " ");

            if (NoEquivalent.TryGetValue(op, out string why)) { warning = why; return code; }
            if (Ops.TryGetValue(op, out var to))
            {
                notes.Add(to.Note ?? (op.ToUpperInvariant() + " → " + to.To));
                op = to.To;
            }
            if (arg.Length == 0) return op;

            bool ind = arg.StartsWith("IND ", StringComparison.OrdinalIgnoreCase);
            string a = ind ? arg.Substring(4).Trim() : arg;

            if (Regex.IsMatch(a, "^[XYZTL]$") && (ind || RegisterOps.Contains(op)))
            {
                a = "ST " + a;
                notes.Add("registre de pile");
            }
            else if (op.Equals("TONE", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(a, @"^0\d$"))
            {
                a = a.Substring(1);
                notes.Add("TONE : un seul chiffre");
            }
            else if (Regex.IsMatch(a, @"^\d$") && TwoDigitOps.Contains(op))
            {
                a = "0" + a;
                notes.Add("deux chiffres");
            }
            return op + " " + (ind ? "IND " : "") + a;
        }
    }
}
