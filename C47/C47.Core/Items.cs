using System.Text;
using System.Text.RegularExpressions;

namespace C47.Core;

public enum ParamKind { None, DeclareLabel, Label, Register, Flag, Number8, Number16, Compare, KeyGKeyX, SkipBack, Number8_16, Shuffle, Menu, Literal, Rem, Disabled }

public sealed record Item(int Number, ParamKind Kind, int TamMax, string Catalog, string Menu, string FullConversion)
{
    /// <summary>Nom affiché dans un listing, comme decodeOneStep() : conversion complète, sinon catalogue, sinon menu.</summary>
    public string Name => FullConversion != "" ? FullConversion : Catalog != "" ? Catalog : Menu;

    /// <summary>Nom lisible : vrais exposants, glyphes propres à la C47 écrits en clair (Draw_y^x, k_B), et x≠? plutôt que le « x≠ ? » du catalogue.</summary>
    public string Shown => Items.ShownName(this);
}

/// <summary>Table des instructions C47, générée depuis items.c par tools/gen_items.py.</summary>
public static class Items
{
    public static readonly Dictionary<int, Item> ByNumber = new();
    static readonly Dictionary<string, Item> byName = new();
    static readonly Dictionary<string, int> constants = new();
    /// <summary>Paramètre implicite des tests HP-42S : X≠0? équivaut à x≠? 0.</summary>
    public static readonly Dictionary<string, string> AliasParam = new();

    public static int Const(string name) => constants[name];
    public static int Literal => Const("ITM_LITERAL");
    public static int End => Const("ITM_END");

    static readonly Dictionary<string, ParamKind> kinds = new()
    {
        ["NONE"] = ParamKind.None, ["DECLARE_LABEL"] = ParamKind.DeclareLabel, ["LABEL"] = ParamKind.Label, ["REGISTER"] = ParamKind.Register,
        ["FLAG"] = ParamKind.Flag, ["NUMBER_8"] = ParamKind.Number8, ["NUMBER_16"] = ParamKind.Number16, ["COMPARE"] = ParamKind.Compare,
        ["KEYG_KEYX"] = ParamKind.KeyGKeyX, ["SKIP_BACK"] = ParamKind.SkipBack, ["NUMBER_8_16"] = ParamKind.Number8_16, ["SHUFFLE"] = ParamKind.Shuffle,
        ["MENU"] = ParamKind.Menu, ["LITERAL"] = ParamKind.Literal, ["REM"] = ParamKind.Rem, ["DISABLED"] = ParamKind.Disabled,
    };

    static Items()
    {
        foreach (var line in Lines("c47_items.tsv"))
        {
            var f = line.Split('\t');
            if (line[0] == '~')
            {
                C47Text.AddGlyph(Convert.ToInt32(f[0][1..], 16), f[1][0]);
                continue;
            }
            if (line[0] == '!')
            {
                if (f[1] != "") constants[f[0][1..]] = int.Parse(f[1]);
                continue;
            }
            var item = new Item(int.Parse(f[0]), kinds[f[1]], int.Parse(f[2]), f[3], f[4], f[5]);
            ByNumber[item.Number] = item;
        }
        // CST_01 à CST_84 : FIRST_CONSTANT / LAST_CONSTANT de defines.h
        constants["FIRST_CONSTANT"] = 128;
        constants["LAST_CONSTANT"] = 128 + 83;

        // le premier programmable gagne en cas de doublon, comme le catalogue
        foreach (var item in ByNumber.Values.OrderBy(i => i.Number))
        {
            if (item.Kind == ParamKind.Disabled) continue;
            if (item.FullConversion != "") byName.TryAdd(Normalize(item.FullConversion), item);
            if (item.Catalog != "") byName.TryAdd(Normalize(item.Catalog), item);
        }
        foreach (var item in ByNumber.Values.OrderBy(i => i.Number))
            if (item.Kind != ParamKind.Disabled && item.Menu != "") byName.TryAdd(Normalize(item.Menu), item);
        foreach (var line in Lines("c47_alias.tsv"))
        {
            if (line[0] == '#') continue;
            var f = line.Split('\t');
            var parts = f[1].Split(' ');
            bool param = parts.Length == 2 && parts[1] is "0." or "Y";
            // ITM:n impose le numéro quand deux instructions partagent un nom
            if (f[1].StartsWith("ITM:")) { byName[Normalize(f[0])] = ByNumber[int.Parse(f[1][4..])]; continue; }
            if (!byName.TryGetValue(Normalize(param ? parts[0] : f[1]), out var target)) throw new InvalidDataException("c47_alias.tsv : cible inconnue " + f[1]);
            if (byName.TryAdd(Normalize(f[0]), target) && param) AliasParam[Normalize(f[0])] = parts[1];
            else if (!param) rejig.TryAdd(target.Number, C47Text.Plain(f[0]));
            if (!param) (aliases.TryGetValue(target.Number, out var l) ? l : aliases[target.Number] = new()).Add(C47Text.Plain(f[0]));
        }
        // nom en clair accepté par l'encodeur ; s'il désigne déjà une autre instruction, on garde le glyphe
        foreach (var item in ByNumber.Values.OrderBy(i => i.Number))
        {
            if (item.Kind == ParamKind.Disabled) continue;
            string clear = Clear(item.Name);
            // notation inventée ici (FOR_y^x) : l'orthographe des listings rejig (FORyˣ) l'emporte
            if (clear != C47Text.Display(item.Name).Replace(" ?", "?") && rejig.TryGetValue(item.Number, out var r)) clear = r;
            if (byName.TryAdd(Normalize(clear), item) || byName[Normalize(clear)] == item) shown[item.Number] = clear;
        }
    }

    static readonly Dictionary<int, string> shown = new();

    /// <summary>Premier alias de c47_alias.tsv pour chaque instruction : orthographe des listings .p47u.</summary>
    static readonly Dictionary<int, string> rejig = new();

    /// <summary>
    /// Readable, sans les espaces fines (iℝ, PLTf, RWDOT⋅ comme rejig : elles ne se tapent pas)
    /// ni le point ou la virgule larges de FWPER ➉ / FWCOM ➈, qui n'ont pas de caractère simple.
    /// </summary>
    static string Clear(string name)
    {
        var s = Regex.Replace(C47Text.Readable(name).Replace(" ?", "?"), "[\\u2002-\\u200a]", "");
        return Regex.Replace(s, "^([A-Z]+)[．，]$", "$1");
    }

    public static string ShownName(Item i) => shown.TryGetValue(i.Number, out var s) ? s : Clear(i.Name);

    static IEnumerable<string> Lines(string resource)
    {
        using var s = typeof(Items).Assembly.GetManifestResourceStream(resource)!;
        using var r = new StreamReader(s, Encoding.UTF8);
        while (r.ReadLine() is { } l)
            if (l.Length > 0) yield return l;
    }

    /// <summary>
    /// Instruction d'après son nom ; à défaut, d'après son écriture au clavier (x<>y, ->, X^2, STO*, R^)
    /// puis sans tenir compte des majuscules quand une seule instruction correspond (abs, lastx).
    /// </summary>
    public static Item? Find(string name)
    {
        if (name.StartsWith("ITM:") && int.TryParse(name[4..], out int n)) return ByNumber.GetValueOrDefault(n);
        if (byName.TryGetValue(Normalize(name), out var i)) return i;
        var spellings = Spellings(name).ToList();
        foreach (var s in spellings)
            if (byName.TryGetValue(Normalize(s), out i)) return i;
        foreach (var s in spellings.Prepend(name))
            if (ByLower.TryGetValue(Normalize(s).ToLowerInvariant(), out i) && i != null) return i;
        return null;
    }

    /// <summary>Paramètre implicite d'un test HP-42S (X≠0? → x≠? 0.), y compris écrit au clavier (X#0?, x<=0?).</summary>
    public static string? ImpliedParam(string name)
    {
        if (AliasParam.TryGetValue(Normalize(name), out var exact)) return exact;
        if (byName.ContainsKey(Normalize(name))) return null;   // nom d'instruction exact : pas de paramètre implicite
        foreach (var s in Spellings(name))
            if (AliasParam.TryGetValue(Normalize(s), out var p)) return p;
        foreach (var s in Spellings(name))
            foreach (var kv in AliasParam)
                if (kv.Key.ToLowerInvariant() == Normalize(s).ToLowerInvariant()) return kv.Value;
        return null;
    }

    static readonly Dictionary<int, List<string>> aliases = new();

    /// <summary>
    /// Écritures au clavier d'une instruction, pour l'autocomplétion : alias de c47_alias.tsv (ABS, X<>Y, SQRT)
    /// et nom affiché en ASCII (x<>y, ->POLAR, STO*, x^2).
    /// </summary>
    public static IEnumerable<string> TypedForms(Item i)
    {
        var forms = new List<string>(aliases.GetValueOrDefault(i.Number) ?? []);
        string a = i.Shown;
        foreach (var (ascii, symbol) in asciiSymbols) if (ascii != "^x") a = a.Replace(symbol, ascii);
        a = a.Replace("÷", "/").Replace("√", "sqrt").Replace("π", "pi").Replace("ⅇ", "e").Replace("↑", "^");
        if (a != i.Shown) forms.Add(a);
        forms.AddRange(asciiNames.Where(kv => Find(kv.Value) == i).Select(kv => kv.Key));   // R^, RDN…
        return forms.Where(f => f != i.Shown).Distinct();
    }

    /// <summary>Suites de caractères ASCII et leur symbole C47, dans l'ordre d'essai.</summary>
    static readonly (string Ascii, string Symbol)[] asciiSymbols =
    [
        ("<->", "↔"), ("->", "→"), ("<-", "←"), ("<>", "⇄"), ("<=", "≤"), (">=", "≥"), ("!=", "≠"),
        ("^-1", "⁻¹"), ("^2", "²"), ("^3", "³"), ("^X", "ˣ"), ("^x", "ˣ"), ("*", "×"),
    ];

    /// <summary>Noms HP-42S écrits en ASCII qui n'ont pas d'équivalent par simple substitution.</summary>
    static readonly Dictionary<string, string> asciiNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["R^"] = "R↑", ["RUP"] = "R↑", ["RV"] = "R↓", ["RDN"] = "R↓", ["/"] = "÷", ["E^X-1"] = "eˣ-1",
    };

    /// <summary>Écritures de remplacement d'un nom tapé au clavier : symboles ASCII remplacés, / final en ÷ (STO/).</summary>
    public static IEnumerable<string> Spellings(string name)
    {
        if (asciiNames.TryGetValue(name, out var direct)) yield return direct;
        string s = name;
        foreach (var (a, b) in asciiSymbols) s = s.Replace(a, b);
        if (s != name) yield return s;
        if (s.Length > 1 && s.EndsWith('/')) yield return s[..^1] + "÷";
    }

    static Dictionary<string, Item?>? byLower;

    /// <summary>Noms en minuscules ; null quand plusieurs instructions ne diffèrent que par la casse (sinh / SINH…).</summary>
    static Dictionary<string, Item?> ByLower
    {
        get
        {
            if (byLower != null) return byLower;
            var d = new Dictionary<string, Item?>();
            foreach (var (key, item) in byName)
            {
                string k = key.ToLowerInvariant();
                if (d.TryGetValue(k, out var other) && other != item) d[k] = null;
                else d.TryAdd(k, item);
            }
            return byLower = d;
        }
    }

    static Dictionary<int, (string Category, string Description, string French)>? help;

    /// <summary>Domaine (clé française), description anglaise d'origine et sa traduction française, d'après c47_help.tsv.</summary>
    public static (string Category, string Description, string French) Help(Item i)
    {
        if (help == null)
        {
            var h = new Dictionary<int, (string, string, string)>();
            foreach (var line in Lines("c47_help.tsv"))
            {
                if (line[0] == '#') continue;
                var f = line.Split('	');
                // la doc écrit avec les codes de la police C47
                h[int.Parse(f[0])] = (f[1], f.Length > 2 ? C47Text.Readable(f[2]) : "", f.Length > 3 ? C47Text.Readable(f[3]) : "");
            }
            help = h;
        }
        return help.TryGetValue(i.Number, out var r) ? r : ("Autres", "", "");
    }

    /// <summary>Description dans la langue de l'interface (Tr.English), l'anglais d'origine à défaut de traduction.</summary>
    public static string Describe(Item i)
    {
        var h = Help(i);
        return Tr.English || h.French == "" ? h.Description : h.French;
    }

    /// <summary>Domaines de l'aide, dans l'ordre d'affichage.</summary>
    public static readonly string[] Categories =
    [
        "Fonctions mathématiques", "Trigonométrie et angles", "Nombres complexes", "Fonctions spéciales", "Matrices et vecteurs",
        "Statistiques et probabilités", "Finances", "Solveur, intégrales, dérivées", "Entiers, bases et bits", "Conversions d'unités",
        "Constantes", "Électrotechnique", "Date, heure et chronomètre", "Pile, registres et mémoire", "Programmation", "Tests et indicateurs",
        "Texte (ALPHA)", "Affichage et modes", "Graphiques", "Entrées, sorties et impression", "Compatibilité HP-42S", "Autres",
    ];

    public static IEnumerable<Item> Programmable => ByNumber.Values.Where(i => i.Kind != ParamKind.Disabled).OrderBy(i => i.Number);

    /// <summary>Ramène les variantes de glyphes (𝑥 italique des listings rejig, etc.) à l'orthographe de items.c.</summary>
    public static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        s = C47Text.Plain(s).Replace("x̄", "͸").Replace("x̂", "͹"); // 𝑥̂ : italique d'abord
        foreach (var r in s.EnumerateRunes())
            sb.Append(r.Value switch
            {
                0x1D4BE => "ⅈ", 0x1D4BF => "ⅉ", // 𝒾 𝒿
                >= 0x1D44E and <= 0x1D467 => ((char)('a' + r.Value - 0x1D44E)).ToString(), // italiques mathématiques
                0x1D70B => "π",
                0x037F => "x", // STD_x_UNDER_ROOT
                0x0232 => "y", // STD_y_UNDER_ROOT
                _ => Rune.IsWhiteSpace(r) ? " " : C47Text.ToFont(r.ToString()),
            });
        return sb.ToString().Replace(" ", "");
    }

    /// <summary>Nom que l'encodeur ramène à cette instruction : plusieurs instructions partagent parfois un libellé.</summary>
    public static string Unique(Item i)
    {
        if (i.Name.All(char.IsDigit) || i.Name == "#") return "ITM:" + i.Number; // serait lu comme un nombre ou un commentaire
        if (Find(i.Shown) == i) return i.Shown;
        if (Find(i.Name) == i) return C47Text.Display(i.Name);
        if (i.Menu != "" && Find(C47Text.Readable(i.Menu)) == i) return C47Text.Readable(i.Menu);
        if (i.Menu != "" && Find(i.Menu) == i) return C47Text.Display(i.Menu);
        return "ITM:" + i.Number;
    }
}

/// <summary>Chaînes internes C47 : 1 octet si ASCII, sinon 2 octets 0x80|haut, bas (stringToUtf8).</summary>
public static class C47Text
{
    static readonly Dictionary<char, char> fontToReal = new(), realToFont = new();

    internal static void AddGlyph(int font, char real)
    {
        fontToReal.TryAdd((char)font, real);
        realToFont.TryAdd(real, (char)font);
    }

    /// <summary>Code de la police C47 vers le caractère Unicode lisible.</summary>
    public static string Display(string s)
    {
        _ = Items.ByNumber;
        var sb = new StringBuilder(s);
        for (int i = 0; i < sb.Length; i++)
            if (fontToReal.TryGetValue(sb[i], out char c)) sb[i] = c;
        // lettres surmontées sans code Unicode précomposé : la police C47 leur donne un code libre
        return sb.Replace("͸", "x̄").Replace("͹", "x̂").Replace("Ϳ", "x").ToString();
    }

    /// <summary>Glyphes de la police C47 sans équivalent Unicode, écrits en clair dans les noms d'instructions.</summary>
    static readonly Dictionary<char, string> clearGlyphs = new()
    {
        ['⧰'] = "yˣ", ['⧱'] = "fg",                                    // STD_YX (bornes prises dans Y et X, FORyˣ de rejig), STD_fg
        ['΀'] = "ū", ['΁'] = "v̄", ['΂'] = "w̄", ['΃'] = "z̄", // ū v̄ w̄ z̄
        ['΋'] = "v̂", ['΍'] = "ẑ", ['΢'] = "û",                       // v̂ ẑ û
        ['➉'] = "．", ['➈'] = "，",                                      // STD_WPERIOD, STD_WCOMMA : point et virgule larges
        ['␭'] = "_UK", ['␮'] = "_US",                         // unités impériales et américaines (gal_UK→l)
        ['⁳'] = "_E", ['₏'] = "^*", ['₞'] = "^∞", ['₟'] = "_∞",
        ['ѐ'] = "_m^α", ['ё'] = "_n*", ['ђ'] = "_d⁻¹", ['ѓ'] = "_k*", ['є'] = "_1*",                 // Lᵅₘ, Bₙ*, g(d-1)…
        ['і'] = "²_p", ['ї'] = "²◭", ['ј'] = "²◮",                                                   // χ²
    };

    /// <summary>Lettre en indice (-1) ou en exposant (+1) de la police C47, 0 sinon.</summary>
    static (int Pos, char Letter) Script(char c) => c switch
    {
        >= '⒜' and <= '⒵' => (-1, (char)('a' + c - 0x249c)),
        >= 'ⓐ' and <= 'ⓩ' => (-1, (char)('A' + c - 0x24d0)),
        '⊖' => (-1, 'α'), '⊗' => (-1, 'δ'), '⊘' => (-1, 'μ'), 'Ɽ' => (-1, 'π'),
        >= '⒂' and <= '⒛' => (1, (char)('a' + c - 0x2482)),
        >= 'Ⓐ' and <= 'Ⓩ' => (1, (char)('A' + c - 0x24b6)),
        'ⱥ' => (1, 'π'),
        _ => (0, c),
    };

    /// <summary>
    /// Nom d'instruction lisible sous Windows : Display, plus les indices et exposants sans caractère Unicode
    /// écrits _abc / ^abc (kⓑ → k_B) et les glyphes composés de clearGlyphs (Draw⧰ → Draw_y^x).
    /// </summary>
    public static string Readable(string s)
    {
        _ = Items.ByNumber;
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length;)
        {
            var (pos, _) = Script(s[i]);
            if (pos != 0)
            {
                int j = i;
                while (j < s.Length && Script(s[j]).Pos == pos) j++;
                var run = s[i..j];
                if (run.All(fontToReal.ContainsKey)) sb.Append(Display(run));
                else sb.Append(pos < 0 ? '_' : '^').Append(run.Select(c => Script(c).Letter).ToArray());
                i = j;
                continue;
            }
            if (clearGlyphs.TryGetValue(s[i], out var clear)) sb.Append(clear);
            else sb.Append(Display(s[i].ToString()));
            i++;
        }
        return sb.ToString();
    }

    /// <summary>Caractère lisible vers son code dans la police C47.</summary>
    public static string ToFont(string s)
    {
        _ = Items.ByNumber;
        var sb = new StringBuilder(s);
        for (int i = 0; i < sb.Length; i++)
            if (realToFont.TryGetValue(sb[i], out char c)) sb[i] = c;
        return sb.Replace("x̄", "͸").Replace("x̂", "͹").ToString();
    }

    /// <summary>Remplace les italiques mathématiques hors BMP (𝑥) : l'éditeur affiche un caractère par case.</summary>
    public static string Plain(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var r in s.EnumerateRunes())
            if (r.Value is >= 0x1D44E and <= 0x1D467) sb.Append((char)('a' + r.Value - 0x1D44E));
            else sb.Append(r.ToString());
        return sb.ToString();
    }

    public static string ToUnicode(ReadOnlySpan<byte> b)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < b.Length; i++)
        {
            if ((b[i] & 0x80) != 0 && i + 1 < b.Length)
            {
                sb.Append((char)(((b[i] & 0x7f) << 8) | b[i + 1]));
                i++;
            }
            else sb.Append((char)b[i]);
        }
        return sb.ToString();
    }

    public static byte[] FromUnicode(string s)
    {
        var o = new List<byte>();
        foreach (var r0 in ToFont(Plain(s)).EnumerateRunes())
        {
            int r = r0.Value;
            if (r < 0x80) o.Add((byte)r);
            else if (r < 0x8000) { o.Add((byte)(0x80 | (r >> 8))); o.Add((byte)r); }
            else throw new ArgumentException($"caractère U+{r:X4} impossible dans une chaîne C47");
        }
        return o.ToArray();
    }
}
