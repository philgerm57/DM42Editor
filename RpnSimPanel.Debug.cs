using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Dm42.Editor;
using Py42.Core;

namespace dm42editor
{
    /// <summary>
    /// Pas à pas du simulateur DM42. Le programme est exécuté une fois en enregistrant l'état avant chaque instruction
    /// (SimTrace) ; les commandes se déplacent ensuite dans cet enregistrement :
    /// <list type="bullet">
    /// <item>pas détaillé (F11) : instruction suivante, en entrant dans les XEQ et dans la fonction de SOLVE / INTEG ;</item>
    /// <item>pas principal (F10) : exécute un XEQ, SOLVE ou INTEG d'un coup ;</item>
    /// <item>sortir (Maj+F11) : jusqu'au retour du sous-programme en cours ;</item>
    /// <item>pas précédent (Ctrl+F10) : revient d'une instruction ;</item>
    /// <item>continuer (F5) : jusqu'au prochain point d'arrêt (F9) ou à la fin.</item>
    /// </list>
    /// </summary>
    sealed partial class RpnSimPanel
    {
        public enum StepKind { Into, Over, Out, Back, Continue }

        FlowLayoutPanel debugBar;
        Label stepLabel;
        SimTrace trace;
        SimResult traceResult;
        RpnListing.Result traceListing;
        string traceSource;
        string[] traceLines;
        CodeEditor traceEditor;
        int cur;

        public bool Debugging => trace != null;

        FlowLayoutPanel BuildDebugBar(ToolTip tip)
        {
            var bar = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 0) };
            Button B(string text, string help, Action act)
            {
                var b = new Button { Text = text, AutoSize = true, Margin = new Padding(2, 1, 2, 1) };
                b.Click += (s, e) => act();
                tip.SetToolTip(b, Lang.T(help));
                bar.Controls.Add(b);
                return b;
            }
            bar.Controls.Add(new Label { Text = Lang.T("Pas à pas"), AutoSize = true, Margin = new Padding(6, 7, 4, 0), Font = new Font(Font, FontStyle.Bold) });
            B(Lang.T("Pas détaillé  F11"), "Instruction suivante, en entrant dans les sous-programmes (XEQ) et dans la fonction de SOLVE / INTEG", () => DoStep(StepKind.Into));
            B(Lang.T("Pas principal  F10"), "Instruction suivante ; un XEQ, SOLVE ou INTEG est exécuté d'un coup", () => DoStep(StepKind.Over));
            B(Lang.T("Sortir  Maj+F11"), "Jusqu'au retour (RTN) du sous-programme en cours", () => DoStep(StepKind.Out));
            B(Lang.T("Pas précédent  Ctrl+F10"), "Revient à l'instruction précédente", () => DoStep(StepKind.Back));
            B(Lang.T("Continuer  F5"), "Jusqu'au prochain point d'arrêt ou à la fin du programme", () => DoStep(StepKind.Continue));
            B(Lang.T("Arrêter  Maj+F5"), "Quitte le pas à pas", StopDebug);
            B(Lang.T("Point d'arrêt  F9"), "Pose ou retire un point d'arrêt sur la ligne du curseur", ToggleBreakpoint);
            stepLabel = new Label { AutoSize = true, Margin = new Padding(8, 7, 4, 0) };
            bar.Controls.Add(stepLabel);
            return bar;
        }

        /// <summary>F9 : point d'arrêt sur la ligne du curseur.</summary>
        public void ToggleBreakpoint()
        {
            var ed = editor();
            if (ed == null) return;
            ed.ToggleBreakpoint(ed.Selection.Start.iLine);
        }

        /// <summary>Commande de pas à pas ; lance l'enregistrement s'il n'y en a pas, ou si le programme a changé.</summary>
        public void DoStep(StepKind kind)
        {
            if (isC47()) { output.Text = Lang.T("Le pas à pas n'existe que pour les programmes DM42 : le simulateur C47 s'exécute d'un bloc."); return; }
            if (!Debugging || source() != traceSource || editor() != traceEditor)
            {
                if (kind == StepKind.Back) return;
                StartDebug(kind);
                return;
            }
            MoveTo(kind);
        }

        public void Continue() => DoStep(StepKind.Continue);

        /// <summary>Exécute le programme en l'enregistrant, puis se place sur la première instruction (ou le premier point d'arrêt).</summary>
        void StartDebug(StepKind kind)
        {
            StopDebug();
            Refresh(true);
            if (!ReadArgs(out string entry, out double[] stack, out double[] inputs)) return;
            ShowLcd(null);
            traceSource = source() ?? "";
            traceLines = traceSource.Replace("\r\n", "\n").Split('\n');
            traceListing = RpnListing.Parse(traceSource);
            traceEditor = editor();
            trace = new SimTrace();
            Cursor = Cursors.WaitCursor;
            try { traceResult = Simulator.Run(traceListing.Code, entry, stack, inputs, trace: trace); }
            finally { Cursor = Cursors.Default; }
            if (trace.Steps.Count == 0)
            {
                // label introuvable : rien n'a été exécuté
                output.Text = Dm42Report(traceListing, traceResult, true);
                StopDebug();
                return;
            }
            cur = 0;
            if (kind == StepKind.Continue)
            {
                // F5 avec des points d'arrêt : premier arrêt, sinon résultat normal
                int hit = NextBreakpoint(-1);
                if (hit < 0) { output.Text = Dm42Report(traceListing, traceResult, true); StopDebug(); return; }
                cur = hit;
            }
            ShowStep();
        }

        public void StopDebug()
        {
            if (Emulator.C47Busy) Emulator.C47Cancel = true;   // Maj+F5 interrompt aussi une simulation C47
            if (traceEditor != null && !traceEditor.IsDisposed) traceEditor.ExecLine = -1;
            trace = null;
            traceResult = null;
            traceListing = null;
            traceEditor = null;
            traceSource = null;
            if (stepLabel != null) stepLabel.Text = "";
        }

        int Last => trace.Steps.Count - 1;

        void MoveTo(StepKind kind)
        {
            var steps = trace.Steps;
            int d = steps[cur].Depth;
            int Find(Func<SimTrace.Step, bool> ok)
            {
                for (int j = cur + 1; j < steps.Count; j++) if (ok(steps[j])) return j;
                return Last;
            }
            switch (kind)
            {
                case StepKind.Into: cur = Math.Min(cur + 1, Last); break;
                case StepKind.Over: cur = Find(s => s.Depth <= d); break;
                case StepKind.Out: cur = d == 0 ? Last : Find(s => s.Depth < d); break;
                case StepKind.Back: cur = Math.Max(cur - 1, 0); break;
                case StepKind.Continue:
                {
                    int hit = NextBreakpoint(cur);
                    cur = hit < 0 ? Last : hit;
                    break;
                }
            }
            ShowStep();
        }

        /// <summary>Premier pas après <paramref name="from"/> dont l'instruction est sur une ligne à point d'arrêt (-1 sinon).</summary>
        int NextBreakpoint(int from)
        {
            var stops = new HashSet<int>(traceEditor?.BreakpointLines ?? Enumerable.Empty<int>());
            if (stops.Count == 0) return -1;
            for (int j = from + 1; j < trace.Steps.Count; j++)
            {
                int pc = trace.Steps[j].Pc;
                if (pc >= 0 && stops.Contains(traceListing.Code[pc].Line - 1)) return j;
            }
            return -1;
        }

        static string Txt(Val v) => v.IsStr ? "\"" + v.S + "\"" : Simulator.Fmt(v);

        /// <summary>État au pas courant : ligne surlignée dans l'éditeur, pile, ALPHA, variables, messages.</summary>
        void ShowStep()
        {
            var st = trace.Steps[cur];
            bool end = st.Pc < 0 || (cur == Last && trace.Truncated);
            int line = st.Pc >= 0 ? traceListing.Code[st.Pc].Line : 0;
            stepLabel.Text = Lang.F("pas {0} / {1}", cur + 1, trace.Steps.Count) + (trace.Truncated ? "+" : "");

            var sb = new StringBuilder();
            if (st.Pc < 0)
            {
                // fin : le résultat complet, l'éditeur montre la ligne de l'erreur s'il y en a une
                int errLine = traceResult.Error != null && traceResult.ErrorPc >= 0 ? traceListing.Code[traceResult.ErrorPc].Line : 0;
                if (traceEditor != null) traceEditor.ExecLine = errLine - 1;
                sb.AppendLine(Lang.T("Pas à pas : fin du programme (Ctrl+F10 pour revenir en arrière, Maj+F5 pour quitter)"));
                sb.Append(Dm42Report(traceListing, traceResult, false));
                output.Text = sb.ToString();
                return;
            }
            if (traceEditor != null) traceEditor.ExecLine = line - 1;
            string code = line > 0 && line <= traceLines.Length ? traceLines[line - 1].Trim() : "";
            sb.AppendLine(Lang.F("Prochaine instruction, ligne {0} :  {1}", Shown(line), code));
            if (st.Depth > 0) sb.AppendLine(Lang.F("Niveau d'appel {0}", st.Depth));
            if (end) sb.AppendLine(Lang.F("Enregistrement limité aux {0} premiers pas : la suite n'est pas parcourue.", trace.Limit));
            sb.AppendLine();
            sb.AppendLine($"T: {Txt(st.T)}");
            sb.AppendLine($"Z: {Txt(st.Z)}");
            sb.AppendLine($"Y: {Txt(st.Y)}");
            sb.AppendLine($"X: {Txt(st.X)}");
            sb.AppendLine($"ALPHA: \"{st.Alpha}\"");
            var snap = trace.Snaps[st.Snap];
            if (snap.Vars.Length > 0)
                sb.AppendLine().AppendLine(Lang.T("Variables : ") + string.Join("   ", snap.Vars.Select(kv => $"{kv.Key}={Txt(kv.Value)}")));
            if (snap.Regs.Length > 0)
                sb.AppendLine().AppendLine(Lang.T("Registres : ") + string.Join("   ", snap.Regs.Select(kv => $"R{kv.Key:00}={Txt(kv.Value)}")));
            if (st.Out > 0)
            {
                sb.AppendLine().AppendLine(Lang.T("Messages :"));
                foreach (var m in traceResult.Display.Take(st.Out)) sb.AppendLine(m);
            }
            output.Text = sb.ToString().Replace("\n", Environment.NewLine).Replace("\r\r", "\r");
        }
    }
}
