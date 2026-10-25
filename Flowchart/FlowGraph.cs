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
        static readonly Regex TestRx = new Regex(
            @"^(X(=|≠|<|>|≤|≥|<=|>=|!=)(0|Y)\?|F[SC]\?C?|BIT\?|MAT\?|CPX\?|REAL\?|STR\?|ISG|DSE)(\s|$)", RegexOptions.IgnoreCase);

        sealed class Ins
        {
            public FlowLine Line;
            public string Op, Arg;
            public bool IsLabel, IsGlobalLabel, IsGto, IsTest, IsStop, IsCall, IsIO;
            public string Key;   // label défini (LBL) ou visé (GTO)
        }

        /// <summary>« 1 » et « 01 » désignent le même label local.</summary>
        static string LabelKey(string arg)
        {
            arg = arg.Trim();
            return int.TryParse(arg, NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n.ToString("00") : arg;
        }

        /// <summary>Sépare le code du commentaire (// # @ hors des guillemets).</summary>
        static void SplitComment(string s, out string code, out string comment) => SplitComment(s, out code, out _, out comment);

        /// <summary>Idem, avec la marque du commentaire (« // », « # » ou « @ », null s'il n'y en a pas).</summary>
        internal static void SplitComment(string s, out string code, out string marker, out string comment)
        {
            bool quote = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') quote = !quote;
                if (quote) continue;
                if (c == '#' || c == '@' || (c == '/' && i + 1 < s.Length && s[i + 1] == '/'))
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
                ins.IsGlobalLabel = ins.IsLabel && ins.Arg.StartsWith("\"");
                ins.IsGto = ins.Op == "GTO" && ins.Arg.Length > 0 && !ins.Arg.StartsWith("IND", StringComparison.OrdinalIgnoreCase);
                ins.IsTest = TestRx.IsMatch(code);
                ins.IsStop = ins.Op == "RTN" || ins.Op == "END";
                ins.IsCall = ins.Op == "XEQ";
                ins.IsIO = ins.Op is "INPUT" or "PROMPT" or "VIEW" or "AVIEW";
                if (ins.IsLabel || ins.IsGto) ins.Key = LabelKey(ins.Arg);
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

            // test suivi d'un GTO : le GTO devient la branche « O » du losange
            var folded = new bool[n];
            for (int i = 0; i + 1 < n; i++)
                if (ins[i].IsTest && ins[i + 1].IsGto && !folded[i]) folded[i + 1] = true;

            // une forme commence sur un test, la ligne qu'il peut sauter, et celle d'après
            var boundary = new HashSet<int>();
            for (int i = 0; i < n; i++)
                if (ins[i].IsTest) { boundary.Add(i + 1); boundary.Add(i + 2); }

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
                    // GTO absorbé : gardé avec le test (numéro de pas et commentaire)
                    insNode[i] = insNode[i - 1];
                    if (insNode[i] >= 0) g.Nodes[insNode[i]].Lines.Add(x.Line);
                    continue;
                }

                if (cur == null || boundary.Contains(i) || x.IsTest || x.IsStop || x.IsCall || x.IsIO)
                {
                    var kind = x.IsTest ? NodeKind.Test : x.IsStop ? NodeKind.Stop : x.IsCall ? NodeKind.Call : x.IsIO ? NodeKind.IO : NodeKind.Block;
                    int idx = AddNode(kind);
                    cur = g.Nodes[idx];
                }
                AddLine(cur, x.Line);
                int ci = g.Nodes.Count - 1;
                if (firstIns[ci] < 0) firstIns[ci] = i;
                lastIns[ci] = i;
                insNode[i] = g.Nodes.Count - 1;
                if (cur.Kind != NodeKind.Block || x.IsGto) cur = null;   // la suite commence une nouvelle forme
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

            for (int ni = 0; ni < g.Nodes.Count; ni++)
            {
                var node = g.Nodes[ni];
                if (node.Lines.Count == 0) continue;
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
                        if (last.IsGto) Edge(ni, Target(last.Key), EdgeKind.Jump, null, "GTO " + last.Arg);
                        else if (ni + 1 < g.Nodes.Count) Edge(ni, ni + 1, EdgeKind.Down);
                        break;
                    }
                    case NodeKind.Test:
                    {
                        int k = firstIns[ni];          // l'instruction de test
                        if (k + 1 < n && folded[k + 1])
                        {
                            Edge(ni, Target(ins[k + 1].Key), EdgeKind.Jump, "O", "GTO " + ins[k + 1].Arg);
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
