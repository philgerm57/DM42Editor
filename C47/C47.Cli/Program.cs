using System.Text;
using C47.Core;
using Encoder = C47.Core.Encoder;
using Decoder = C47.Core.Decoder;

Console.OutputEncoding = Encoding.UTF8;
if (args.Length < 2)
{
    Console.WriteLine("c47 enc source.txt [sortie.p47]   texte vers .p47");
    Console.WriteLine("c47 dec prog.p47                  .p47 vers texte");
    Console.WriteLine("c47 test dossier                  aller-retour sur tous les .p47 et .p47u du dossier");
    return 2;
}
switch (args[0])
{
    case "enc":
    {
        var r = Encoder.Encode(File.ReadAllText(args[1], Encoding.UTF8));
        foreach (var e in r.Errors) Console.WriteLine($"{args[1]}:{e.Line}: {e.Message}");
        if (!r.Ok) return 1;
        string o = args.Length > 2 ? args[2] : Path.ChangeExtension(args[1], ".p47");
        File.WriteAllBytes(o, P47File.Write(r.Program));
        Console.WriteLine($"{o} : {r.Steps} pas, {r.Program.Length} octets");
        return 0;
    }
    case "dec":
        foreach (var l in Decoder.Decode(P47File.Read(File.ReadAllBytes(args[1])))) Console.WriteLine(l);
        return 0;
    case "test":
        return Test(args[1]);
    case "harvest":
        return Harvest(args[1]);
    default:
        return 2;
}

// Aligne chaque .p47u sur son .p47 pour lister les orthographes rejig encore inconnues.
static int Harvest(string dir)
{
    var found = new Dictionary<string, Dictionary<string, int>>();
    foreach (var f in Directory.EnumerateFiles(dir, "*.p47u", SearchOption.AllDirectories))
    {
        string bin = Path.ChangeExtension(f, ".p47");
        if (!File.Exists(bin)) continue;
        var text = Decoder.Decode(P47File.Read(File.ReadAllBytes(bin)));
        int j = 0;
        foreach (var line in File.ReadAllLines(f, Encoding.UTF8))
        {
            List<string> toks;
            try { toks = Encoder.Tokenize(line); } catch { continue; }
            int k = 0;
            while (k < toks.Count && j < text.Count)
            {
                if (toks[k] == ".END.") { k++; continue; }
                int n = Encoder.StepTokens(toks, k);
                if (n > 0) { k += n; j++; continue; }
                string want = text[j];
                string name = want.Split(' ')[0];
                if (!found.TryGetValue(toks[k], out var d)) found[toks[k]] = d = new();
                d[name] = d.GetValueOrDefault(name) + 1;
                j++;
                break; // pas inconnu : on suppose un pas par ligne
            }
        }
    }
    foreach (var kv in found.OrderByDescending(k => k.Value.Values.Sum()))
        Console.WriteLine(kv.Key + "	" + string.Join(" ", kv.Value.OrderByDescending(v => v.Value).Select(v => $"{v.Key}×{v.Value}")));
    return 0;
}

static string Diff(byte[] a, byte[] b)
{
    int i = 0;
    while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
    return $"octet {i}/{a.Length} : attendu {string.Join(" ", a.Skip(i).Take(10))} | obtenu {string.Join(" ", b.Skip(i).Take(10))}";
}

static int Test(string dir)
{
    int okRound = 0, koRound = 0, okSrc = 0, koSrc = 0, okAuto = 0, koAuto = 0, raw = 0, steps = 0;
    var reasons = new Dictionary<string, int>();
    void Why(string w) { reasons[w] = reasons.GetValueOrDefault(w) + 1; }

    foreach (var f in Directory.EnumerateFiles(dir, "*.p47", SearchOption.AllDirectories))
    {
        byte[] want = P47File.Read(File.ReadAllBytes(f));
        var text = Decoder.Decode(want);
        raw += text.Count(l => l.Contains('⟦'));
        steps += text.Count;
        if (Environment.GetEnvironmentVariable("C47_RAW") != null)
            foreach (var l in text.Where(l => l.Contains('⟦'))) Console.WriteLine($"BRUT {Path.GetFileName(f)} {l[..Math.Min(l.Length, 50)]}");
        var r = Encoder.Encode(string.Join("\n", text), keepStructNumbers: true);
        if (r.Ok && r.Program.SequenceEqual(want)) okRound++;
        else
        {
            koRound++;
            Console.WriteLine($"ALLER-RETOUR {Path.GetFileName(f)} : " + (r.Ok ? Diff(want, r.Program) : string.Join(" / ", r.Errors.Take(3))));
            foreach (var e in r.Errors) Why("dec→enc: " + e.Message);
        }

        // sans numéros de structure, l'encodeur doit retrouver les mêmes
        var bare = text.Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"^(IF|ELSE|ENDIF|DO|WHILE|ENDDO|REPEAT|UNTIL) \d+$", "$1"));
        if (text.Any(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^(IF|DO|REPEAT) \d+$")))
        {
            var a = Encoder.Encode(string.Join("\n", bare));
            if (a.Ok && a.Program.SequenceEqual(want)) okAuto++;
            else { koAuto++; Console.WriteLine($"NUMÉROTATION {Path.GetFileName(f)} : " + (a.Ok ? Diff(want, a.Program) : string.Join(" / ", a.Errors.Take(3)))); }
        }

        string src = Path.ChangeExtension(f, ".p47u");
        if (!File.Exists(src)) continue;
        var s = Encoder.Encode(File.ReadAllText(src, Encoding.UTF8));
        if (s.Ok && s.Program.SequenceEqual(want)) okSrc++;
        else
        {
            koSrc++;
            Console.WriteLine($"SOURCE {Path.GetFileName(src)} : " + (s.Ok ? Diff(want, s.Program) : string.Join(" / ", s.Errors.Take(4))));
            foreach (var e in s.Errors) Why("p47u: " + e.Message);
            foreach (var e in s.Errors.Take(1)) Console.WriteLine("   ligne " + e.Line + " : " + File.ReadLines(src).ElementAt(e.Line - 1).Trim());
        }
    }
    Console.WriteLine();
    foreach (var kv in reasons.OrderByDescending(k => k.Value).Take(60)) Console.WriteLine($"{kv.Value,5}  {kv.Key}");
    Console.WriteLine($"aller-retour .p47 : {okRound} OK, {koRound} KO ({steps} pas, {raw} bruts)");
    Console.WriteLine($"sources .p47u     : {okSrc} OK, {koSrc} KO");
    Console.WriteLine($"numérotation auto : {okAuto} OK, {koAuto} KO");
    return koRound + koSrc + koAuto == 0 ? 0 : 1;
}
