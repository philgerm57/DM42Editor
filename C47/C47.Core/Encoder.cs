using System.Text.RegularExpressions;

namespace C47.Core;

public sealed record EncodeError(int Line, string Message);

public sealed class EncodeResult
{
    public byte[] Program = [];
    public List<EncodeError> Errors = [];
    public int Steps;
    /// <summary>Pour chaque pas : ligne source (1-based) et position de son premier octet dans Program.</summary>
    public List<(int Line, int Offset)> StepLines = [];
    public bool Ok => Errors.Count == 0;
}

/// <summary>Listing texte vers octets de programme. Accepte la syntaxe .p47u et les listings XPORTP.</summary>
public static partial class Encoder
{
    public sealed class Fail(string m) : Exception(m);

    /// <param name="keepStructNumbers">Garde les numéros écrits ; sinon ils sont refaits comme VALID.</param>
    public static EncodeResult Encode(string source, bool keepStructNumbers = false)
    {
        var res = new EncodeResult();
        var o = new List<byte>();
        var open = new List<(string Kind, int Number)>();
        numbers = [];
        keepNumbers = keepStructNumbers;
        int lineNo = 0;
        foreach (var raw in source.Replace("\r\n", "\n").Split('\n'))
        {
            lineNo++;
            try
            {
                var toks = Tokenize(raw);
                int k = 0;
                while (k < toks.Count)
                {
                    if (toks[k] == ".END.") { k++; continue; }
                    bool end = toks[k] == "END";
                    res.StepLines.Add((lineNo, o.Count));
                    EncodeStep(toks, ref k, o, open);
                    if (end) open.Clear();
                    res.Steps++;
                }
            }
            catch (Fail f) { res.Errors.Add(new(lineNo, f.Message)); }
            catch (ArgumentException a) { res.Errors.Add(new(lineNo, a.Message)); }
            catch (Exception x) when (x is FormatException or OverflowException) { res.Errors.Add(new(lineNo, "Nombre invalide : " + raw.Trim())); }
        }
        // un programme se termine toujours par END
        if (o.Count > 0 && !EndsWithEnd(o)) { res.StepLines.Add((0, o.Count)); Op(o, Items.End); res.Steps++; }
        // fnLoadProgram refuse un programme qui ne tient pas sur 16 bits, .END. compris
        if (o.Count + 2 > ushort.MaxValue) res.Errors.Add(new(0, $"Programme trop long : {o.Count} octets"));
        res.Program = o.ToArray();
        return res;
    }

    /// <summary>Jetons consommés par le pas en position k, 0 s'il n'est pas reconnu (recherche d'alias).</summary>
    public static int StepTokens(List<string> t, int k)
    {
        int p = k;
        try { EncodeStep(t, ref p, [], []); return p - k; }
        catch (Fail) { return 0; }
        catch (FormatException) { return 0; }
        catch (ArgumentException) { return 0; }
    }

    static bool EndsWithEnd(List<byte> o) => o.Count >= 2 && o[^2] == (0x80 | (Items.End >> 8)) && o[^1] == (Items.End & 0xff);

    [GeneratedRegex(@"^\s*\d{3,5}:\s")] private static partial Regex StepNumber();

    /// <summary>Découpe une ligne : jetons séparés par des blancs, chaînes entières, sans commentaires ni numéros de pas.</summary>
    public static List<string> Tokenize(string line)
    {
        var o = new List<string>();
        line = StepNumber().Replace(line, " ");
        string t = line.TrimStart();
        if (t.StartsWith('@') || t.StartsWith('{')) return o;
        int i = 0;
        while (i < line.Length)
        {
            if (char.IsWhiteSpace(line[i])) { i++; continue; }
            if (line[i] == ';') break;
            // « # texte » : commentaire conservé dans le programme, en pas REM (la forme rejig)
            if (line[i] == '#' && (i + 1 == line.Length || char.IsWhiteSpace(line[i + 1])))
            {
                o.Add("REM");
                o.Add("‘" + line[(i + 1)..].Trim() + "’");
                break;
            }
            int s = i;
            while (i < line.Length && !char.IsWhiteSpace(line[i]))
            {
                char c = line[i];
                if (i == s && (c == '‘' || c == '"' || c == '⟦' || c == '\'') || (c == '‘' || c == '"') && line[s] == '→')
                {
                    char close = c == '‘' ? '’' : c == '"' || c == '\'' ? c : '⟧';
                    int e = line.IndexOf(close, i + 1);
                    if (e < 0) throw new Fail("Chaîne non fermée");
                    i = e + 1;
                }
                else if (c == ';') break;
                else i++;
            }
            o.Add(line[s..i]);
        }
        return o;
    }

    static void Op(List<byte> o, int op)
    {
        if (op < 128) o.Add((byte)op);
        else { o.Add((byte)(0x80 | (op >> 8))); o.Add((byte)op); }
    }

    static void EncodeStep(List<string> t, ref int k, List<byte> o, List<(string, int)> open)
    {
        string tok = t[k];
        if (tok[0] == '⟦') { o.AddRange(Convert.FromHexString(tok[1..^1])); k++; return; }

        // un nombre seul est un littéral ; sinon le nom le plus long (jusqu'à 3 jetons) gagne
        Item? item = null;
        int used = 0;
        if (!IsNumber(tok))
            for (int n = Math.Min(3, t.Count - k); n >= 1 && item == null; n--)
            {
                item = Items.Find(string.Join(" ", t.Skip(k).Take(n)));
                // plusieurs jetons seulement pour un nom qui contient vraiment un espace (PLT f)
                if (item != null && n > 1 && !item.Name.Any(char.IsWhiteSpace)) item = null;
                if (item != null) used = n;
            }
        // IF₁ : numéro STRUCT en indices, collé au nom
        int? fused = null;
        if (item == null)
        {
            var m = Regex.Match(tok, "^(.*?)([₀-₉]+)$");
            if (m.Success && Items.Find(m.Groups[1].Value) is { Kind: ParamKind.SkipBack } st)
            {
                item = st;
                used = 1;
                fused = int.Parse(string.Concat(m.Groups[2].Value.Select(c => (char)('0' + c - '₀'))));
            }
        }
        if (item == null)
        {
            if (LiteralBytes(tok) is { } lit)
            {
                Op(o, Items.Literal);
                o.AddRange(lit);
                k++;
                return;
            }
            throw new Fail($"Instruction inconnue : {tok}");
        }
        // X≠0? : le test sur 0 ou Y porte son paramètre dans le nom
        if (used == 1 && Items.AliasParam.TryGetValue(Items.Normalize(tok), out var implied))
        {
            Op(o, item.Number);
            k++;
            RegisterParam(o, implied, item);
            return;
        }
        k += used;
        Op(o, item.Number);
        switch (item.Kind)
        {
            case ParamKind.None:
            case ParamKind.Disabled:
                return;
            case ParamKind.Literal:
                throw new Fail("Écrivez la valeur directement, sans LITE");
            case ParamKind.Rem:
                o.Add(Decoder.Lbl);
                PutStr(o, Unquote(Need(t, ref k, item)) ?? throw new Fail($"{item.Shown} : texte entre ‘ ’ attendu"));
                return;
            case ParamKind.KeyGKeyX:
                Number(o, item, Need(t, ref k, item), ParamKind.Number8);
                string second = Need(t, ref k, item);
                var s2 = Items.Find(second) ?? throw new Fail($"{item.Shown} : GTO ou XEQ attendu, pas {second}");
                if (s2.Number >= 128) throw new Fail($"{item.Shown} : {second} n'est pas accepté ici");
                o.Add((byte)s2.Number);
                LabelParam(o, Need(t, ref k, item), false, item);
                return;
            case ParamKind.SkipBack:
                SkipBack(o, item, fused, t, ref k, open);
                return;
            case ParamKind.Shuffle:
            {
                string p = Need(t, ref k, item).ToLowerInvariant();
                if (p.Length != 4 || p.Any(c => !"xyzt".Contains(c))) throw new Fail($"{item.Shown} : 4 lettres parmi x y z t attendues");
                int v = 0;
                for (int i = 0; i < 4; i++) v |= "xyzt".IndexOf(p[i]) << (2 * i);
                o.Add((byte)v);
                return;
            }
            case ParamKind.DeclareLabel:
                LabelParam(o, Need(t, ref k, item), true, item);
                return;
            case ParamKind.Label:
                LabelParam(o, Need(t, ref k, item), false, item);
                return;
            case ParamKind.Register:
            case ParamKind.Compare:
                RegisterParam(o, Need(t, ref k, item), item);
                return;
            case ParamKind.Flag:
                FlagParam(o, Need(t, ref k, item), item);
                return;
            case ParamKind.Menu:
            {
                string p = Need(t, ref k, item);
                if (Indirect(o, p)) return;
                o.Add(Decoder.Lbl);
                PutStr(o, Unquote(p) ?? throw new Fail($"{item.Shown} : nom de menu entre ‘ ’ attendu"));
                return;
            }
            default:
                Number(o, item, Need(t, ref k, item), item.Kind);
                return;
        }
    }

    /// <summary>Paramètre suivant ; la forme HP-42S « IND 05 » devient →05.</summary>
    static string Need(List<string> t, ref int k, Item item)
    {
        if (k >= t.Count) throw new Fail($"{item.Shown} : paramètre manquant");
        string p = t[k++];
        if (p == "IND" && k < t.Count) return "→" + t[k++];
        return p;
    }

    static readonly Dictionary<string, string> OpenerOf = new()
    {
        ["ELSE"] = "IF", ["ENDIF"] = "IF", ["WHILE"] = "DO", ["ENDDO"] = "DO", ["UNTIL"] = "REPEAT",
    };
    static readonly string[] Closers = ["ENDIF", "ENDDO", "UNTIL"];

    // VALID : une série par famille d'ouverture, dans l'ordre d'apparition (structWalkProgram)
    [ThreadStatic] static Dictionary<string, int>? numbers;
    [ThreadStatic] static bool keepNumbers;

    /// <summary>BACK/SKIP : un nombre. Structures : numéro donné, sinon profondeur d'imbrication (VALID exige un numéro non nul).</summary>
    static void SkipBack(List<byte> o, Item item, int? fused, List<string> t, ref int k, List<(string Kind, int Number)> open)
    {
        int? val = fused;
        if (val == null && k < t.Count && Regex.IsMatch(t[k], @"^\d+$")) val = int.Parse(t[k++]);
        if (!Decoder.IsStruct(item))
        {
            if (val == null) throw new Fail($"{item.Shown} : nombre de pas manquant");
            if (val > 255) throw new Fail($"{item.Shown} : {val} dépasse 255");
            o.Add((byte)val);
            return;
        }
        string name = item.Name;
        numbers ??= [];
        if (!keepNumbers) val = null;
        if (OpenerOf.TryGetValue(name, out var opener))
        {
            if (open.Count == 0 || open[^1].Kind != opener) throw new Fail($"{name} sans {opener} correspondant");
            val ??= open[^1].Number;
            if (Closers.Contains(name)) open.RemoveAt(open.Count - 1);
        }
        else
        {
            numbers[name] = numbers.GetValueOrDefault(name) + 1;
            val ??= numbers[name];
            open.Add((name, val.Value));
        }
        if (val is null or > 255) throw new Fail($"{name} : numéro de structure invalide");
        o.Add((byte)val);
    }

    static string? Unquote(string p)
    {
        if (p.Length >= 2 && ((p[0] == '‘' && p[^1] == '’') || (p[0] == '"' && p[^1] == '"') || (p[0] == '\'' && p[^1] == '\'')))
            return p[1..^1];
        return null;
    }

    static void PutStr(List<byte> o, string s)
    {
        var b = C47Text.FromUnicode(s);
        if (b.Length > 255) throw new Fail("Chaîne trop longue (255 octets au plus)");
        o.Add((byte)b.Length);
        o.AddRange(b);
    }

    static bool Indirect(List<byte> o, string p)
    {
        if (!p.StartsWith('→')) return false;
        string r = p[1..];
        if (Unquote(r) is { } name)
        {
            o.Add(Decoder.IndVar);
            PutStr(o, name);
            return true;
        }
        o.Add(Decoder.IndReg);
        o.Add((byte)(RegisterCode(r) ?? throw new Fail($"Registre indirect invalide : {p}")));
        return true;
    }

    static int? RegisterCode(string r)
    {
        if (r.Length >= 2 && r[0] is 'R' or 'F' && (char.IsDigit(r[1]) || r[1] == '.')) r = r[1..];
        if (Regex.IsMatch(r, @"^\d{1,2}$")) return int.Parse(r);
        if (Regex.IsMatch(r, @"^\.\d{1,2}$"))
        {
            int n = int.Parse(r[1..]);
            return n <= 98 ? 112 + n : null;
        }
        if (r.Length == 1)
        {
            int a = Decoder.Lettered1.IndexOf(r[0]);
            if (a >= 0) return 100 + a;
            int b = Decoder.Lettered2.IndexOf(r[0]);
            if (b >= 0) return 211 + b;
        }
        return null;
    }

    static void LabelParam(List<byte> o, string p, bool declare, Item item)
    {
        if (Unquote(p) is { } name)
        {
            if (declare && C47Text.FromUnicode(name).Length > 14) throw new Fail($"Nom d'étiquette trop long : {name}");
            o.Add(Decoder.Lbl);
            PutStr(o, name);
            return;
        }
        if (p.Length > 2 && p[0] == ':' && p[^1] == ':')
        {
            o.Add(Decoder.LocalLbl);
            PutStr(o, p[1..^1]);
            return;
        }
        if (!declare && Indirect(o, p)) return;
        if (Regex.IsMatch(p, @"^\d{1,2}$")) { o.Add(byte.Parse(p)); return; }
        if (p.Length == 1 && p[0] is >= 'A' and <= 'L') { o.Add((byte)(100 + p[0] - 'A')); return; }
        if (p.Length == 1 && p[0] is >= 'a' and <= 'l') { o.Add((byte)(112 + p[0] - 'a')); return; }
        throw new Fail($"{item.Shown} : étiquette invalide : {p}");
    }

    static void RegisterParam(List<byte> o, string p, Item item)
    {
        if (item.Kind == ParamKind.Compare && p == "0.") { o.Add(Decoder.Val0); return; }
        if (item.Kind == ParamKind.Compare && p == "1.") { o.Add(Decoder.Val1); return; }
        if (Indirect(o, p)) return;
        if (Unquote(p) is { } name)
        {
            o.Add(Decoder.Lbl);
            PutStr(o, name);
            return;
        }
        o.Add((byte)(RegisterCode(p) ?? throw new Fail($"{item.Shown} : registre invalide : {p}")));
    }

    static void FlagParam(List<byte> o, string p, Item item)
    {
        if (Indirect(o, p)) return;
        if (Unquote(p) is { } name)
        {
            o.Add(Decoder.SysFlag);
            o.Add((byte)(SystemFlag(name) ?? throw new Fail($"{item.Shown} : indicateur système inconnu : {name}")));
            return;
        }
        int code = RegisterCode(p) ?? throw new Fail($"{item.Shown} : indicateur invalide : {p}");
        if (code >= 144 && code <= 210) throw new Fail($"{item.Shown} : indicateur local limité à .31");
        o.Add((byte)code);
    }

    static int? SystemFlag(string name)
    {
        string n = Items.Normalize(name.Replace("_(HP)", "ⓗⓟ").Replace("_(US)", "ⓤⓢ"));
        int a = Items.Const("SFL_TDM24"), b = Items.Const("SFL_MONIT");
        for (int i = 0; i < 64; i++)
            if (Items.ByNumber.TryGetValue(a + i, out var it) && Items.Normalize(it.Menu) == n) return i;
        for (int i = 0; i < 192; i++)
            if (Items.ByNumber.TryGetValue(b + i, out var it) && Items.Normalize(it.Menu) == n) return 64 + i;
        return null;
    }

    static void Number(List<byte> o, Item item, string p, ParamKind kind)
    {
        if (Indirect(o, p)) return;
        if (!Regex.IsMatch(p, @"^\d+$") || !int.TryParse(p, out int v)) throw new Fail($"{item.Shown} : nombre attendu, pas {p}");
        switch (kind)
        {
            case ParamKind.Number8:
                int max = item.TamMax is > 0 and <= 249 ? item.TamMax : 249;
                if (v > max) throw new Fail($"{item.Shown} : {v} dépasse {max}");
                o.Add((byte)v);
                break;
            case ParamKind.Number8_16:
                if (v > 505) throw new Fail($"{item.Shown} : {v} dépasse 505");
                if (v <= 249) o.Add((byte)v);
                else { o.Add(Decoder.Beyond250); o.Add((byte)(v - 250)); }
                break;
            default: // Number16 : petit boutiste pour les anciennes fonctions, gros boutiste pour les autres
                if (v > 65535) throw new Fail($"{item.Shown} : {v} dépasse 65535");
                if (Decoder.IsOldParam16(item.Number)) { o.Add((byte)v); o.Add((byte)(v >> 8)); }
                else
                {
                    if (v >> 8 is Decoder.IndReg or Decoder.IndVar) throw new Fail($"{item.Shown} : {v} est réservé aux formes indirectes");
                    o.Add((byte)(v >> 8));
                    o.Add((byte)v);
                }
                break;
        }
    }

    static string Trim0(string v) => v.EndsWith('.') ? v[..^1] : v;

    static bool IsNumber(string t) => Regex.IsMatch(t, @"^-?\d+$") || Regex.IsMatch(t, @"^-?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$") || Regex.IsMatch(t, @"^-?[0-9A-F]+#\d+$");

    /// <summary>Type puis contenu d'un littéral (decodeLiteral) ; null si ce n'est pas un littéral.</summary>
    static byte[]? LiteralBytes(string t)
    {
        var o = new List<byte>();
        if (Unquote(t) is { } s) { o.Add(Decoder.Lbl); PutStr(o, s); return o.ToArray(); }
        var m = Regex.Match(t, @"^(-?[0-9A-F]+)(?:#(\d+)|([₀-₉]+))$");
        if (m.Success)
        {
            int b = int.Parse(m.Groups[2].Success ? m.Groups[2].Value : string.Concat(m.Groups[3].Value.Select(c => (char)('0' + c - '₀'))));
            if (b < 2 || b > 16) throw new Fail($"Base invalide : {b}");
            o.Add(7);
            o.Add((byte)b);
            PutStr(o, m.Groups[1].Value);
            return o.ToArray();
        }
        if (Regex.IsMatch(t, @"^-?\d+$")) { o.Add(8); PutStr(o, t); return o.ToArray(); }
        if (Regex.IsMatch(t, @"^-?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$")) { o.Add(9); PutStr(o, t); return o.ToArray(); }
        foreach (var (suffix, type) in new[] { ("ʳ", 18), ("ᵍ", 19), ("°", 20), ("ⱦ", 22) })
            if (t.EndsWith(suffix) && Regex.IsMatch(t[..^suffix.Length], @"^-?\d*\.?\d*([eE][-+]?\d+)?$"))
            {
                o.Add((byte)type);
                PutStr(o, t[..^suffix.Length]);
                return o.ToArray();
            }
        foreach (var (prefix, type) in new[] { ("dms:", 21), ("hms:", 11), ("jd:", 12) })
            if (t.StartsWith(prefix))
            {
                o.Add((byte)type);
                PutStr(o, t[prefix.Length..]);
                return o.ToArray();
            }
        if (Regex.IsMatch(t, @"^-?[\d.eE]*[+-]?[ij][\d.eE-]*$") && t.IndexOfAny(['i', 'j']) >= 0 && t.Any(char.IsDigit))
        {
            o.Add(10);
            PutStr(o, t);
            return o.ToArray();
        }
        // écriture rejig d'un complexe : 1.001+1.002𝒾 est stocké 1.001+i1.002
        m = Regex.Match(t, @"^(-?[\d.]+(?:[eE][-+]?\d+)?)([+-])([\d.]+(?:[eE][-+]?\d+)?)𝒾$");
        if (m.Success)
        {
            o.Add(10);
            PutStr(o, Trim0(m.Groups[1].Value) + m.Groups[2].Value + "i" + Trim0(m.Groups[3].Value));
            return o.ToArray();
        }
        return null;
    }
}
