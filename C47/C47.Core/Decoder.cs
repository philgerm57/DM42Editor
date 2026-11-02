using System.Text;

namespace C47.Core;

/// <summary>Octets de programme vers listing texte (equivalent de decode.c). Un pas sans forme texte sûre sort en ⟦hex⟧.</summary>
public static class Decoder
{
    internal const int Lbl = 253, IndReg = 254, IndVar = 255, LocalLbl = 249, SysFlag = 250, Beyond250 = 250, Val0 = 251, Val1 = 252;
    internal const string Lettered1 = "XYZTABCDLIJK", Lettered2 = "MNPQRSEFGHOUVW";

    public static List<string> Decode(byte[] d)
    {
        var o = new List<string>();
        int i = 0;
        while (i < d.Length)
        {
            int start = i;
            string text;
            try { text = Step(d, ref i, true); }
            catch (Exception e) when (e is IndexOutOfRangeException or NotSupportedException)
            {
                // plus rien de décodable : le reste reste intact
                o.Add("⟦" + Convert.ToHexString(d, start, d.Length - start) + "⟧");
                break;
            }
            if (text == ".END.") break;
            o.Add(text);
        }
        return o;
    }

    static int ReadOp(byte[] d, ref int i)
    {
        int op = d[i++];
        if ((op & 0x80) != 0) op = ((op & 0x7f) << 8) | d[i++];
        return op;
    }

    static string Str(byte[] d, ref int i)
    {
        int n = d[i++];
        if (i + n > d.Length) throw new IndexOutOfRangeException();
        var s = C47Text.Display(C47Text.ToUnicode(d.AsSpan(i, n)));
        i += n;
        return s;
    }

    static string Step(byte[] d, ref int i, bool keyParam)
    {
        int start = i;
        int op = ReadOp(d, ref i);
        if (op == 0x7fff) return ".END.";
        if (!Items.ByNumber.TryGetValue(op, out var item)) throw new NotSupportedException();
        switch (item.Kind)
        {
            case ParamKind.None:
            case ParamKind.Disabled:
                return Items.Unique(item);
            case ParamKind.Literal:
                return Literal(d, ref i, start);
            case ParamKind.Rem:
            {
                if (d[i++] != Lbl) throw new NotSupportedException();
                string text = Str(d, ref i);
                // « # texte » comme les listings .p47u, sauf si l'encodeur ne le relirait pas à l'identique
                if (Items.Unique(item) == "REM" && text == text.Trim() && !text.Contains('\n')) return text == "" ? "#" : "# " + text;
                return Items.Unique(item) + " ‘" + text + "’";
            }
            case ParamKind.KeyGKeyX:
            {
                string first = Items.Unique(item) + " " + Param(d, ref i, item, ParamKind.Number8);
                if (!keyParam) return first;
                return first + " " + Step(d, ref i, false);
            }
            default:
                return Items.Unique(item) + " " + Param(d, ref i, item, item.Kind);
        }
    }

    static string Indirect(byte[] d, ref int i, int p)
    {
        if (p == IndReg) return "→" + Register(d[i++]);
        if (p == IndVar) return "→‘" + Str(d, ref i) + "’";
        throw new NotSupportedException();
    }

    internal static string Register(int p)
    {
        if (p < 100) return "R" + p.ToString("00");
        if (p <= 111) return Lettered1[p - 100].ToString();
        if (p <= 210) return "R." + (p - 112).ToString("00");
        if (p <= 224) return Lettered2[p - 211].ToString();
        throw new NotSupportedException();
    }

    static string LabelValue(int p) => p <= 99 ? p.ToString("00") : p <= 111 ? ((char)('A' + p - 100)).ToString() : ((char)('a' + p - 112)).ToString();

    // deux chiffres au moins, comme les listings .p47u (TONE 08) ; la calculatrice affiche TONE 8
    static string Width(int v, int max) => v.ToString(max <= 99 ? "00" : max <= 999 ? "000" : "0000");

    static string Param(byte[] d, ref int i, Item item, ParamKind kind)
    {
        int p = d[i++];
        switch (kind)
        {
            case ParamKind.DeclareLabel:
            case ParamKind.Label:
                if (p <= 123) return LabelValue(p);
                if (p == Lbl) return "‘" + Str(d, ref i) + "’";
                if (p == LocalLbl) return ":" + Str(d, ref i) + ":";
                return Indirect(d, ref i, p);
            case ParamKind.Register:
                if (p <= 224) return Register(p);
                if (p == Lbl) return "‘" + Str(d, ref i) + "’";
                return Indirect(d, ref i, p);
            case ParamKind.Compare:
                if (p <= 224) return Register(p);
                if (p == Lbl) return "‘" + Str(d, ref i) + "’";
                if (p == Val0) return "0.";
                if (p == Val1) return "1.";
                return Indirect(d, ref i, p);
            case ParamKind.Flag:
                if (p < 100) return "F" + p.ToString("00");
                if (p <= 111) return Lettered1[p - 100].ToString();
                if (p <= 143) return "F." + (p - 112).ToString("00");
                if (p >= 211 && p <= 224) return Lettered2[p - 211].ToString();
                if (p == SysFlag)
                {
                    int n = d[i++];
                    int it = n < 64 ? n + Items.Const("SFL_TDM24") : n - 64 + Items.Const("SFL_MONIT");
                    return "‘" + C47Text.Display(Items.ByNumber[it].Menu) + "’";
                }
                return Indirect(d, ref i, p);
            case ParamKind.Number8:
                if (p <= 249) return Width(p, item.TamMax);
                return Indirect(d, ref i, p);
            case ParamKind.Number8_16:
                if (p <= 249) return Width(p, item.TamMax);
                if (p == Beyond250) return (250 + d[i++]).ToString("000");
                return Indirect(d, ref i, p);
            case ParamKind.Number16:
                if (IsOldParam16(item.Number)) return (p + 256 * d[i++]).ToString();
                if (p == IndReg || p == IndVar) return Indirect(d, ref i, p);
                return (p * 256 + d[i++]).ToString();
            case ParamKind.SkipBack:
                return p.ToString(IsStruct(item) ? "00" : "000");
            case ParamKind.Shuffle:
                return new string([.. new[] { p & 3, (p >> 2) & 3, (p >> 4) & 3, (p >> 6) & 3 }.Select(k => "xyzt"[k])]);
            case ParamKind.Menu:
                if (p == Lbl) return "‘" + Str(d, ref i) + "’";
                return Indirect(d, ref i, p);
            default:
                throw new NotSupportedException();
        }
    }

    public static bool IsStructOp(Item i) => IsStruct(i);

    internal static bool IsStruct(Item i) => i.Kind == ParamKind.SkipBack && i.Number >= 2900;

    internal static bool IsOldParam16(int op) =>
        op == Items.Const("ITM_BESTF_OLD") || op == Items.Const("ITM_RNG_OLD") || op == Items.Const("ITM_YY_DFLT_OLD") || op == Items.Const("ITM_DENMAX2_OLD");

    /// <summary>Base en indices, comme les listings .p47u (3₁₆).</summary>
    static string Subscript(int b) => string.Concat(b.ToString().Select(c => (char)('₀' + c - '0')));

    static string Literal(byte[] d, ref int i, int start)
    {
        int t = d[i++];
        switch (t)
        {
            case 7:
            {
                int b = d[i++];
                string s = Str(d, ref i);
                // 8 caractères ou plus rangés en texte (firmware ancien) : # pour que l'encodeur garde cette forme
                return s.Length >= 8 && s[0] != '-' ? s + "#" + b : s + Subscript(b);
            }
            case 1 when d[i] is >= 2 and <= 16:
            {
                // entier court binaire : base puis valeur sur 8 octets ; écrit 00000123₁₆ pour être réencodé en binaire
                int b = d[i++];
                ulong v = BitConverter.ToUInt64(d, i);
                i += 8;
                var digits = new StringBuilder();
                do { digits.Insert(0, "0123456789ABCDEF"[(int)(v % (ulong)b)]); v /= (ulong)b; } while (v > 0);
                return digits.ToString().PadLeft(8, '0') + Subscript(b);
            }
            case 8:
            case 9:
                return Str(d, ref i);
            case 10:
                return Str(d, ref i);
            case 18: return Str(d, ref i) + "ʳ";
            case 19: return Str(d, ref i) + "ᵍ";
            case 20: return Str(d, ref i) + "°";
            case 22: return Str(d, ref i) + "ⱦ";
            case 21: return "dms:" + Str(d, ref i);
            case 11: return "hms:" + Str(d, ref i);
            case 12: return "jd:" + Str(d, ref i);
            case Lbl:
                return "‘" + Str(d, ref i) + "’";
            case 1: i += 9; break;
            case 3: i += 16; break;
            case 4: i += 32; break;
            default: throw new NotSupportedException();
        }
        return "⟦" + Convert.ToHexString(d, start, i - start) + "⟧";
    }
}
