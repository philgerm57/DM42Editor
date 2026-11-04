using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace dm42editor.Flowchart
{
    /// <summary>Forme du logigramme.</summary>
    enum NodeKind
    {
        Start,  // LBL "NOM" : ovale d'entrée
        Block,  // suite d'instructions : rectangle
        Test,   // X<Y?, FS? 01, ISG… : losange
        Stop,   // RTN, END : ovale de sortie
        Call,   // XEQ : sous-programme, rectangle à doubles barres
        IO      // INPUT, PROMPT, VIEW, AVIEW : entrée / sortie, parallélogramme
    }

    /// <summary>Liaison : Down = vers la forme suivante, Jump = couloir de gauche (GTO), Skip = couloir de droite (« N » d'un test).</summary>
    enum EdgeKind { Down, Jump, Skip }

    /// <summary>Une ligne de programme : ligne dans l'éditeur (0-based), numéro de pas, code et commentaire.</summary>
    sealed class FlowLine
    {
        public int TextLine;
        public string Step;
        public string Code;
        public string Comment;
    }

    sealed class FlowNode
    {
        public NodeKind Kind;
        public readonly List<FlowLine> Lines = new List<FlowLine>();
        /// <summary>Labels locaux qui pointent ici (cercles à gauche de la forme).</summary>
        public readonly List<string> Labels = new List<string>();
        /// <summary>Texte d'un losange quand il diffère de la première ligne (« X=Y?  IF » : test et structure C47).</summary>
        public string Caption;
        public string Text => Caption ?? Lines[0].Code;
        public RectangleF Bounds;   // calculé par la mise en page
        public int TextLine => Lines.Count > 0 ? Lines[0].TextLine : -1;
    }

    sealed class FlowEdge
    {
        public int From, To = -1;      // To = -1 : label introuvable
        public EdgeKind Kind;
        public string Mark;            // « O » / « N » pour un test
        public string Note;            // GTO nn (affiché si le label manque)
        public int Lane;               // couloir attribué par la mise en page
    }

    /// <summary>
    /// Programme RPN découpé en formes et liaisons. Sémantique HP-42S : un test vrai exécute la ligne suivante,
    /// faux la saute. Un test suivi d'un GTO devient un losange dont le « O » part directement vers le label.
    /// </summary>
    sealed class FlowGraph
    {
        public readonly List<FlowNode> Nodes = new List<FlowNode>();
        public readonly List<FlowEdge> Edges = new List<FlowEdge>();

        static readonly Regex StepRx = new Regex(@"^\s*(\d+)(?:▸|▶|>|\s+)(?=\S)");
        // tests HP-42S (X<Y?, FS?C…) et C47 (𝑥<? 0., x=? ‘S2’, PRIME?, FS? F00, ISE, DSL…) : toute instruction en « ? »
        // sauf Config?, plus les incréments / décréments à saut et les FS?C / FC?S / FC?F…
        static readonly Regex TestRx = new Regex(
            @"^(?!CONFIG\?)(\S+\?|F[SC]\?[CSF]|ISG|DSE|ISE|DSL|DSZ|ISZ)(\s|$)", RegexOptions.IgnoreCase);

        sealed class Ins
        {
            public FlowLine Line;
            public string Op, Arg;
            public bool IsLabel, IsGlobalLabel, IsGto, IsTest, IsStop, IsCall, IsIO;
            public string Key;   // label défini (LBL) ou visé (GTO)
            public int Rel;         // SKIP n / BACK n (C47) : saut relatif, en instructions (0 sinon)
            public string Struct;   // structure C47 : IF ELSE ENDIF DO WHILE ENDDO REPEAT UNTIL FOR FORTOP NEXT, sinon null
        }

        /// <summary>Mot de structure C47 (AN0007), les variantes de FOR ramenées à FOR (test en bas) ou FORTOP (test en tête).</summary>
        static string StructOf(string op)
        {
            switch (op)
            {
                case "IF": case "ELSE": case "ENDIF": case "DO": case "WHILE": case "ENDDO": case "REPEAT": case "UNTIL":
                    return op;
            }
            if (op.StartsWith("NEXT")) return "NEXT";
            if (op.StartsWith("FOR"))
                return op.Contains("TOP") || op.Contains("ᵀᴼᴾ") || op.Contains("ⓉⓄⓅ") ? "FORTOP" : "FOR";
            return null;
        }

        /// <summary>« 1 » et « 01 » désignent le même label local.</summary>
        static string LabelKey(string arg)
        {
            arg = arg.Trim();
            return int.TryParse(arg, NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n.ToString("00") : arg;
        }

        /// <summary>Sépare le code du commentaire (// # @, et ; des sources C47, hors des guillemets).</summary>
        static void SplitComment(string s, out string code, out string comment) => SplitComment(s, out code, out _, out comment);

        /// <summary>Idem, avec la marque du commentaire (« // », « # », « @ » ou « ; », null s'il n'y en a pas).</summary>
        internal static void SplitComment(string s, out string code, out string marker, out string comment)
        {
            bool quote = false, c47Quote = false;   // "texte" (DM42) ou ‘texte’ (C47)
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"' && !c47Quote) quote = !quote;
                else if (c == '‘' && !quote) c47Quote = true;
                else if (c == '’' && !quote) c47Quote = false;
                if (quote || c47Quote) continue;
                if (c == '#' || c == '@' || c == ';' || (c == '/' && i + 1 < s.Length && s[i + 1] == '/'))
                {
                    code = s.Substring(0, i);
                    marker = c == '/' ? "//" : c.ToString();
                    comment = s.Substring(i + marker.Length).Trim();
                    return;
                }
            }
            code = s;
            marker = null;
            comment = null;
        }

        static string Join(string a, string b) =>
            string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + " — " + b;

        static List<Ins> Read(string text)
        {
            var list = new List<Ins>();
            var raw = (text ?? "").Replace("\r\n", "\n").Split('\n');
            string pending = null;   // ligne de commentaire seule : jointe à l'instruction suivante
            int count = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                string s = raw[i];
                string t = s.Trim();
                if (t.Length == 0) continue;
                if (t.StartsWith("#") || t.StartsWith("@") || t.StartsWith("//"))
                {
                    pending = Join(pending, t.TrimStart('#', '@', '/').Trim());
                    continue;
                }
                string step = null, body = s;
                var m = StepRx.Match(s);
                if (m.Success) { step = m.Groups[1].Value; body = s.Substring(m.Length); }
                SplitComment(body, out string code, out string comment);
                code = code.Trim();
                if (code.Length == 0) { pending = Join(pending, comment); continue; }
                if (code.StartsWith("{")) { count = 0; pending = null; continue; }   // en-tête { nn-Byte Prgm }
                count++;
                var ins = new Ins
                {
                    Line = new FlowLine { TextLine = i, Step = step ?? count.ToString("00"), Code = code, Comment = Join(pending, comment) }
                };
                pending = null;
                int sp = code.IndexOf(' ');
                ins.Op = (sp < 0 ? code : code.Substring(0, sp)).ToUpperInvariant();
                ins.Arg = sp < 0 ? "" : code.Substring(sp + 1).Trim();
                ins.IsLabel = ins.Op == "LBL" && ins.Arg.Length > 0;
                // label global : "NOM" (DM42), ‘NOM’ ou 'NOM' (C47)
                ins.IsGlobalLabel = ins.IsLabel && ins.Arg[0] is '"' or '‘' or '\'';
                ins.IsGto = ins.Op == "GTO" && ins.Arg.Length > 0 && !ins.Arg.StartsWith("IND", StringComparison.OrdinalIgnoreCase);
                ins.IsTest = TestRx.IsMatch(code);
                ins.IsStop = ins.Op is "RTN" or "END" or "FINISH";   // FINISH (C47) : arrêt avec un message
                ins.IsCall = ins.Op == "XEQ";
                ins.IsIO = ins.Op is "INPUT" or "PROMPT" or "VIEW" or "AVIEW" or "42AVIEW" or "42PRMPT";   // + noms C47 des commandes HP-42S
                ins.Struct = StructOf(ins.Op);
                if (ins.IsLabel || ins.IsGto) ins.Key = LabelKey(ins.Arg);
                // SKIP n saute n pas, BACK n revient de n pas : un GTO sans label
                if (ins.Op is "SKIP" or "BACK" && int.TryParse(ins.Arg, NumberStyles.None, CultureInfo.InvariantCulture, out int rel) && rel > 0)
                {
                    ins.IsGto = true;
                    ins.Rel = ins.Op == "SKIP" ? rel + 1 : -rel;
                }
                list.Add(ins);
            }
            return list;
        }

        public static FlowGraph Parse(string text)
        {
            var g = new FlowGraph();
            var ins = Read(text);
            int n = ins.Count;
            if (n == 0) return g;

            // structures C47 : chaque mot relié à son ouverture (IF → ELSE, ENDIF ; DO → WHILE, ENDDO ; REPEAT → UNTIL ; FOR → NEXT)
            var opener = Enumerable.Repeat(-1, n).ToArray();   // mot de structure → son IF / DO / REPEAT / FOR
            var elseOf = new Dictionary<int, int>();           // IF → ELSE
            var closeOf = new Dictionary<int, int>();          // ouverture → ENDIF / ENDDO / UNTIL / NEXT
            var open = new List<int>();
            for (int i = 0; i < n; i++)
            {
                string s = ins[i].Struct;
                if (s == null) continue;
                string family = s switch { "ELSE" or "ENDIF" => "IF", "WHILE" or "ENDDO" => "DO", "UNTIL" => "REPEAT", "NEXT" => "FOR", _ => null };
                if (family == null) { open.Add(i); continue; }
                int at = open.FindLastIndex(o => ins[o].Struct == family || (family == "FOR" && ins[o].Struct == "FORTOP"));
                if (at < 0) { ins[i].Struct = null; continue; }   // sans ouverture : ligne ordinaire
                int op = open[at];
                opener[i] = op;
                if (s == "ELSE") elseOf[op] = i;
                else if (s != "WHILE")
                {
                    closeOf[op] = i;
                    open.RemoveRange(at, open.Count - at);
                }
            }
            bool Decides(int i) => ins[i].Struct is "IF" or "WHILE" or "UNTIL";

            // test suivi d'un GTO : le GTO devient la branche « O » du losange
            // test suivi d'un IF / WHILE / UNTIL (C47) : c'est la condition de la structure, un seul losange
            var folded = new bool[n];
            for (int i = 0; i + 1 < n; i++)
                if (ins[i].IsTest && (ins[i + 1].IsGto || Decides(i + 1)) && !folded[i]) folded[i + 1] = true;

            // une forme commence sur un test, la ligne qu'il peut sauter, et celle d'après ;
            // un mot de structure a sa propre forme
            var boundary = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                if (ins[i].IsTest) { boundary.Add(i + 1); boundary.Add(i + 2); }
                else if (ins[i].Struct != null) boundary.Add(i + 1);
                if (ins[i].Rel != 0) boundary.Add(i + ins[i].Rel);   // arrivée d'un SKIP / BACK
            }

            var insNode = Enumerable.Repeat(-1, n).ToArray();
            var firstIns = new List<int>();   // par forme : première et dernière instruction (hors labels)
            var lastIns = new List<int>();
            var labelNode = new Dictionary<string, int>();
            var pendingLabels = new List<(string Key, int Index)>();
            FlowNode cur = null;
            string labelComment = null;   // commentaire d'un LBL local : reporté sur la 1re ligne de sa forme

            int AddNode(NodeKind kind)
            {
                var node = new FlowNode { Kind = kind };
                g.Nodes.Add(node);
                int idx = g.Nodes.Count - 1;
                firstIns.Add(-1);
                lastIns.Add(-1);
                foreach (var (key, li) in pendingLabels)
                {
                    node.Labels.Add(key);
                    labelNode[key] = idx;
                    insNode[li] = idx;
                    labelComment = Join(labelComment, ins[li].Line.Comment);
                }
                pendingLabels.Clear();
                return idx;
            }

            void AddLine(FlowNode node, FlowLine line)
            {
                if (labelComment != null) { line.Comment = Join(labelComment, line.Comment); labelComment = null; }
                node.Lines.Add(line);
            }

            for (int i = 0; i < n; i++)
            {
                var x = ins[i];
                if (x.IsLabel)
                {
                    if (x.IsGlobalLabel)
                    {
                        int idx = AddNode(NodeKind.Start);
                        AddLine(g.Nodes[idx], x.Line);
                        firstIns[idx] = lastIns[idx] = i;
                        labelNode[x.Key] = idx;
                        insNode[i] = idx;
                    }
                    else pendingLabels.Add((x.Key, i));
                    cur = null;
                    continue;
                }
                if (folded[i])
                {
                    // GTO ou IF / WHILE / UNTIL absorbé : gardé avec le test (numéro de pas et commentaire)
                    insNode[i] = insNode[i - 1];
                    if (insNode[i] >= 0)
                    {
                        var tn = g.Nodes[insNode[i]];
                        tn.Lines.Add(x.Line);
                        if (x.Struct != null) tn.Caption = tn.Lines[0].Code + "  " + x.Op;
                    }
                    continue;
                }

                // losange : test, IF / WHILE / UNTIL sans test devant, FORᵀᴼᴾ (test en tête), NEXT d'un FOR (test en bas)
                bool decision = x.IsTest || Decides(i) || x.Struct == "FORTOP"
                                || (x.Struct == "NEXT" && opener[i] >= 0 && ins[opener[i]].Struct == "FOR");
                if (cur == null || boundary.Contains(i) || decision || x.Struct != null || x.IsStop || x.IsCall || x.IsIO)
                {
                    var kind = decision ? NodeKind.Test : x.IsStop ? NodeKind.Stop : x.IsCall ? NodeKind.Call : x.IsIO ? NodeKind.IO : NodeKind.Block;
                    int idx = AddNode(kind);
                    cur = g.Nodes[idx];
                }
                AddLine(cur, x.Line);
                int ci = g.Nodes.Count - 1;
                if (firstIns[ci] < 0) firstIns[ci] = i;
                lastIns[ci] = i;
                insNode[i] = g.Nodes.Count - 1;
                if (cur.Kind != NodeKind.Block || x.IsGto || x.Struct != null) cur = null;   // la suite commence une nouvelle forme
            }
            // labels en toute fin de programme : aucune forme à leur attacher
            pendingLabels.Clear();

            int NodeOf(int i) => i >= 0 && i < n ? insNode[i] : -1;
            void Edge(int from, int to, EdgeKind kind, string mark = null, string note = null)
            {
                if (kind == EdgeKind.Down && to != from + 1) kind = EdgeKind.Jump;
                if (kind == EdgeKind.Skip && to <= from) kind = EdgeKind.Jump;
                g.Edges.Add(new FlowEdge { From = from, To = to, Kind = kind, Mark = mark, Note = note });
            }
            int Target(string key) => labelNode.TryGetValue(key, out int t) ? t : -1;
            // destination d'un GTO, ou d'un SKIP / BACK (instruction i)
            int Dest(int i) => ins[i].Rel != 0 ? NodeOf(i + ins[i].Rel) : Target(ins[i].Key);
            string JumpNote(int i) => ins[i].Rel != 0 ? ins[i].Line.Code : "GTO " + ins[i].Arg;

            // liaisons d'un mot de structure C47 (s : son instruction, ni : sa forme)
            void StructEdges(int ni, int s)
            {
                int op = opener[s];
                int Close(int o) => o >= 0 && closeOf.TryGetValue(o, out int c) ? c : -1;
                int After(int j) => j >= 0 ? NodeOf(j + 1) : -1;
                void Out(int to, EdgeKind kind, string mark = null) { if (to >= 0) Edge(ni, to, kind, mark); }
                switch (ins[s].Struct)
                {
                    case "IF":       // vrai : « alors » ; faux : « sinon », ou après ENDIF
                        Out(After(s), EdgeKind.Down, "O");
                        Out(elseOf.TryGetValue(s, out int e) ? After(e) : NodeOf(Close(s)), EdgeKind.Skip, "N");
                        break;
                    case "ELSE":     // fin du « alors » : le « sinon » est sauté
                        Out(NodeOf(Close(op)), EdgeKind.Jump);
                        break;
                    case "WHILE":    // vrai : corps de la boucle ; faux : sortie après ENDDO
                        Out(After(s), EdgeKind.Down, "O");
                        Out(After(Close(op)), EdgeKind.Skip, "N");
                        break;
                    case "ENDDO":
                        Out(NodeOf(op), EdgeKind.Jump);
                        break;
                    case "UNTIL":    // vrai : sortie ; faux : retour au REPEAT
                        Out(After(s), EdgeKind.Down, "O");
                        Out(NodeOf(op), EdgeKind.Jump, "N");
                        break;
                    case "FORTOP":   // test avant chaque passage : corps, ou sortie après NEXT
                        Out(After(s), EdgeKind.Down, "O");
                        Out(After(Close(s)), EdgeKind.Skip, "N");
                        break;
                    case "NEXT":
                        if (ins[op].Struct == "FORTOP") Out(NodeOf(op), EdgeKind.Jump);
                        else
                        {
                            // FOR : test en bas de boucle, retour au corps ou sortie
                            Out(After(op), EdgeKind.Jump, "O");
                            Out(After(s), EdgeKind.Down, "N");
                        }
                        break;
                    default:         // ENDIF, DO, REPEAT, FOR : on continue en dessous
                        Out(After(s), EdgeKind.Down);
                        break;
                }
            }

            for (int ni = 0; ni < g.Nodes.Count; ni++)
            {
                var node = g.Nodes[ni];
                if (node.Lines.Count == 0) continue;
                // structure C47 : le mot lui-même, ou celui absorbé par le test qui le précède
                int first = firstIns[ni];
                int st = first < 0 ? -1
                       : ins[lastIns[ni]].Struct != null ? lastIns[ni]
                       : node.Kind == NodeKind.Test && first + 1 < n && folded[first + 1] && ins[first + 1].Struct != null ? first + 1 : -1;
                if (st >= 0) { StructEdges(ni, st); continue; }
                // instruction de la dernière ligne de la forme
                switch (node.Kind)
                {
                    case NodeKind.Stop:
                        break;
                    case NodeKind.Start:
                    case NodeKind.Call:     // retour du sous-programme : on continue en dessous
                    case NodeKind.IO:
                        if (ni + 1 < g.Nodes.Count) Edge(ni, ni + 1, EdgeKind.Down);
                        break;
                    case NodeKind.Block:
                    {
                        var last = ins[lastIns[ni]];
                        if (last.IsGto) Edge(ni, Dest(lastIns[ni]), EdgeKind.Jump, null, JumpNote(lastIns[ni]));
                        else if (ni + 1 < g.Nodes.Count) Edge(ni, ni + 1, EdgeKind.Down);
                        break;
                    }
                    case NodeKind.Test:
                    {
                        int k = firstIns[ni];          // l'instruction de test
                        if (k + 1 < n && folded[k + 1])
                        {
                            Edge(ni, Dest(k + 1), EdgeKind.Jump, "O", JumpNote(k + 1));
                            int no = NodeOf(k + 2);
                            if (no >= 0) Edge(ni, no, EdgeKind.Down, "N");
                        }
                        else
                        {
                            int yes = NodeOf(k + 1), no = NodeOf(k + 2);
                            if (yes >= 0) Edge(ni, yes, EdgeKind.Down, "O");
                            if (no >= 0) Edge(ni, no, EdgeKind.Skip, "N");
                        }
                        break;
                    }
                }
            }
            return g;
        }

        public int Count(NodeKind kind) => Nodes.Count(nd => nd.Kind == kind);
    }
}
