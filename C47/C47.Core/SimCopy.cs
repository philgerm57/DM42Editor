using System.Text.RegularExpressions;

namespace C47.Core;

/// <summary>
/// Copie d'une source C47 préparée pour le simulateur sans fenêtre (F5 de la fenêtre Python). Le programme enregistré
/// ou envoyé n'est pas modifié.
/// <list type="bullet">
/// <item>Après chaque affichage (42AVIEW, 42PRMPT, VIEW), le message est ajouté à la variable texte PYLOG, relue à la fin.
/// Sur la pile de 8 niveaux, les deux valeurs empilées puis retirées ne touchent pas ce que le programme utilise.</item>
/// <item>Après chaque saisie, « 0 1 J_y(x) DROP » consomme la touche R/S : le simulateur la garde enfoncée, et le premier
/// calcul interruptible qui suit (Bessel, premiers, gamma, solve, ∫…) s'arrêtait aussitôt sur NaN.</item>
/// <item>Les menus deviennent des saisies, comme dans le simulateur DM42 de Py42 : le STOP d'un VARMENU demande une valeur
/// par variable du menu ; le STOP d'un MENU demande le numéro de la touche (1 à 6, 0 = EXIT) et saute à son action.</item>
/// <item>Sans fenêtre, aucune touche n'arrive : PAUSE 99 (attendre une touche) bloquait le simulateur. Une PAUSE juste
/// avant la fin (RTN, END) devient une capture de l'écran dessiné par le programme (SNAP) ; les autres sont retirées.
/// L'attente d'une touche (KEY? r, GETKEY) capture l'écran puis lit le code de touche comme une saisie : sans saisie,
/// le programme s'arrête là au lieu de tourner sans fin.</item>
/// <item>PYSIM vide le journal puis appelle l'étiquette lancée.</item>
/// </list>
/// </summary>
public static class SimCopy
{
    public const string Entry = "PYSIM";
    public const string LogVar = "PYLOG";
    public const char LogSep = '¦';

    const string ConsumeKey = "  0 1 J_y(x) DROP";
    static readonly Regex KeyRx = new(@"^(?:42)?KEY\s+(\d)\s+(GTO|XEQ)\s+(.+)$", RegexOptions.IgnoreCase);
    static readonly Regex LblRx = new(@"^LBL\s+(‘[^’]*’)$");
    static readonly Regex MvarRx = new(@"^MVAR\s+(‘[^’]*’)$");
    static readonly Regex PauseRx = new(@"(?<=^|\s)PAUSE\s+\d+(?=\s|$)");
    static readonly Regex KeyQRx = new(@"^KEY\?\s+(.+)$");
    static readonly Regex VarMenuRx = new(@"^(?:42VRMNU|VARMENU)\s+(‘[^’]*’)$");

    /// <summary>Instruction d'une ligne, sans le commentaire « ; … » d'un source C47 (hors des textes ‘…’ et "…").</summary>
    static string Code(string line)
    {
        bool q = false, c = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"' && !c) q = !q;
            else if (ch == '‘' && !q) c = true;
            else if (ch == '’' && !q) c = false;
            else if (ch == ';' && !q && !c) return line[..i].Trim();
        }
        return line.Trim();
    }

    public static string Build(string source, string entry) => Build(source, entry, out _);

    /// <param name="pauseSnap">vrai si une PAUSE de fin est devenue SNAP : l'appelant nomme sa capture (iofile)</param>
    public static string Build(string source, string entry, out bool pauseSnap)
    {
        pauseSnap = false;
        string Log(string what) => $"  RCL ‘{LogVar}’ RCL {what} + ‘{LogSep}’ + STO ‘{LogVar}’ DROP";
        var lines = source.Replace("\r\n", "\n").Split('\n');

        // variables de menu de chaque label global : les MVAR qui suivent son LBL
        var mvars = new Dictionary<string, List<string>>();
        for (int i = 0; i < lines.Length; i++)
        {
            var m = LblRx.Match(Code(lines[i]));
            if (!m.Success) continue;
            var list = new List<string>();
            for (int j = i + 1; j < lines.Length; j++)
            {
                var mv = MvarRx.Match(Code(lines[j]));
                if (!mv.Success) break;
                list.Add(mv.Groups[1].Value);
            }
            mvars[m.Groups[1].Value] = list;
        }

        var o = new List<string>();
        var keys = new List<(string N, string Op, string Target)>();   // touches du menu en cours (KEY n GTO/XEQ)
        var relays = new List<string>();                               // relais des sauts de menu, ajoutés en fin de programme
        string prev = null, varMenu = null;
        for (int li = 0; li < lines.Length; li++)
        {
            string raw = lines[li];
            string t = Code(raw);
            if (t == "END") continue;
            if (PauseRx.IsMatch(t))
            {
                string next = lines.Skip(li + 1).Select(Code).FirstOrDefault(c => c.Length > 0) ?? "END";
                if (PauseRx.Replace(t, "").Trim().Length == 0 && next is "RTN" or "END")
                {
                    // PAUSE juste avant la fin (« regardez l'écran ») : capture de l'écran tel que le programme l'a dessiné ;
                    // après la fin, la calculatrice réaffiche la pile par-dessus
                    t = "XEQ ‘PYSN’";
                    raw = "  XEQ ‘PYSN’";
                    pauseSnap = true;
                }
                else
                {
                    // ailleurs, PAUSE retirée (seule sur sa ligne, ou au milieu d'autres instructions)
                    t = PauseRx.Replace(t, "").Trim();
                    if (t.Length == 0) continue;
                    raw = "  " + t;
                }
            }
            var kq = KeyQRx.Match(t);
            if (kq.Success || t == "GETKEY")
            {
                // attente d'une touche : capture de l'écran (la première fois), puis le code de touche est lu comme une saisie
                o.Add("  XEQ ‘PYKY’");
                if (kq.Success)
                {
                    // KEY? r : le code va dans r, et le test est faux comme après une touche (vrai = aucune touche) ;
                    // x≠? compare X à une copie de lui-même : la pile ne change pas
                    o.Add($"  STO {kq.Groups[1].Value}");
                    o.Add("  DROP");
                    o.Add("  STO ‘PYKX’");
                    o.Add("  x≠? ‘PYKX’");
                }
                pauseSnap = true;
                prev = t;
                continue;
            }
            if (t == "CLMENU" || t == "EXITALL") { keys.Clear(); if (t == "EXITALL") varMenu = null; }
            var k = KeyRx.Match(t);
            if (k.Success) keys.Add((k.Groups[1].Value, k.Groups[2].Value.ToUpperInvariant(), k.Groups[3].Value.Trim()));
            var vm = VarMenuRx.Match(t);
            if (vm.Success) varMenu = vm.Groups[1].Value;

            if (t == "STOP" && prev == "MENU" && keys.Count > 0)
            {
                // choix du menu : saisi comme un nombre, puis saut vers l'action de la touche (0 = EXIT, touche 9)
                o.Add("  INPUT ‘PYKEY’");
                o.Add(ConsumeKey);
                o.Add("  DROP");
                foreach (var (n, op, target) in keys)
                {
                    o.Add("  RCL ‘PYKEY’");
                    o.Add(n == "9" ? "  0" : "  " + n);
                    o.Add("  X=Y?");
                    if (op == "GTO")
                    {
                        // relais : retire les deux valeurs du test avant de sauter, la pile reste celle du programme
                        string relay = $"‘PK{relays.Count + 1}’";
                        relays.Add($"LBL {relay}");
                        relays.Add("  DROP");
                        relays.Add("  DROP");
                        relays.Add($"  GTO {target}");
                        o.Add($"  GTO {relay}");
                    }
                    else o.Add($"  {op} {target}");
                    o.Add("  DROP");
                    o.Add("  DROP");
                }
                prev = t;
                continue;
            }
            if (t == "STOP" && varMenu != null && mvars.TryGetValue(varMenu, out var vars) && vars.Count > 0)
            {
                // menu de variables : une saisie par variable, dans l'ordre des MVAR
                foreach (var v in vars)
                {
                    o.Add($"  INPUT {v}");
                    o.Add(ConsumeKey);
                }
                varMenu = null;
                prev = t;
                continue;
            }

            o.Add(raw);
            if (t.StartsWith("INPUT ") || t == "STOP" || t.StartsWith("42PRMPT")) o.Add(ConsumeKey);
            if (t == "42AVIEW" || t.StartsWith("42PRMPT")) o.Add(Log("K"));   // ALPHA des instructions de compatibilité
            else if (t.StartsWith("VIEW "))
            {
                // VIEW X : X est remonté d'un niveau par RCL ‘PYLOG’
                string arg = t.Substring(5).Trim();
                o.Add(Log(arg switch { "X" => "Y", "Y" => "Z", "Z" => "T", "T" => "A", _ => arg }));
            }
            if (t.Length > 0) prev = t;
        }
        o.AddRange(relays);
        if (pauseSnap)
        {
            // PYSN : capture de l'écran, une seule fois (seule la première prend le nom donné par l'appelant) ;
            // PAUSE 1 laisse le simulateur recopier le dessin du programme dans l'écran capturé
            o.Add("LBL ‘PYSN’");
            o.Add("  RCL ‘PYSNF’");
            o.Add("  X=0?");
            o.Add("  XEQ ‘PYSN1’");
            o.Add("  DROP");
            o.Add("  RTN");
            o.Add("LBL ‘PYSN1’");
            o.Add("  PAUSE 1");
            o.Add("  SNAP");
            o.Add("  1 STO ‘PYSNF’ DROP");
            o.Add("  RTN");
            // PYKY : attente d'une touche (KEY?, GETKEY) : capture, puis code de touche saisi, laissé dans X
            o.Add("LBL ‘PYKY’");
            o.Add("  XEQ ‘PYSN’");
            o.Add("  INPUT ‘PYKEY’");
            o.Add(ConsumeKey);
            o.Add("  RTN");
        }
        o.Add($"LBL ‘{Entry}’");
        o.Add($"  ‘’ STO ‘{LogVar}’ DROP");
        if (pauseSnap) o.Add("  0 STO ‘PYSNF’ DROP");
        o.Add($"  XEQ ‘{entry}’");
        o.Add("  RTN");
        o.Add("END");
        return string.Join("\r\n", o);
    }
}
