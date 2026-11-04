using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace dm42editor
{
    /// <summary>
    /// Émulateur qui reçoit les programmes (Free42 Decimal / Binary, Plus42, ou tout autre programme choisi) :
    /// chemin réglé dans Paramètres, commun à l'éditeur et à la fenêtre Python.
    /// Enregistré dans %LOCALAPPDATA%\dm42editor\emulateur.ini.
    /// </summary>
    static class Emulator
    {
        /// <summary>Exécutables reconnus quand aucun émulateur n'est réglé (détection des fenêtres ouvertes).</summary>
        public static readonly string[] KnownNames = { "Free42Decimal", "Free42Binary", "Free42", "Plus42Decimal", "Plus42Binary", "Plus42" };

        static string folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dm42editor");
        static string SettingsFile => Path.Combine(folder, "emulateur.ini");

        static string path;
        static string c47Path;
        static bool c47First;
        static bool loaded;

        /// <summary>Calculatrice par défaut : le dialogue Ouvrir présente d'abord les programmes C47 plutôt que les fichiers DM42 (*.s42).</summary>
        public static bool DefaultC47
        {
            get { Load(); return c47First; }
            set { Load(); c47First = value; Save(); }
        }

        /// <summary>Émulateur C47 / R47 (c47.exe, r47.exe ou un lanceur .cmd) : reçoit les .p47 dans son dossier PROGRAMS.</summary>
        public static string C47Path
        {
            get { Load(); return c47Path ?? ""; }
            set { Load(); c47Path = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); Save(); }
        }

        /// <summary>Chemin complet de l'exécutable de l'émulateur (vide : pas encore choisi).</summary>
        public static string ExePath
        {
            get { Load(); return path ?? ""; }
            set { Load(); path = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); Save(); }
        }

        /// <summary>Nom affiché : « Free42Decimal », « Plus42 »… ou « l'émulateur ».</summary>
        public static string DisplayName => ExePath.Length > 0 ? Path.GetFileNameWithoutExtension(ExePath) : Lang.T("l'émulateur");

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (File.Exists(SettingsFile))
                {
                    foreach (var l in File.ReadAllLines(SettingsFile))
                    {
                        if (l.StartsWith("Chemin=")) path = l.Substring(7).Trim();
                        else if (l.StartsWith("C47=")) c47Path = l.Substring(4).Trim();
                        else if (l.StartsWith("Defaut=")) c47First = l.Substring(7).Trim() == "C47";
                    }
                }
                else
                {
                    // reprise du réglage de l'ancienne version (fenêtre Python)
                    string old = Path.Combine(folder, "python-vue.ini");
                    if (File.Exists(old))
                        foreach (var l in File.ReadAllLines(old))
                            if (l.StartsWith("Free42=") && l.Length > 7) path = l.Substring(7).Trim();
                }
                if (string.IsNullOrEmpty(path)) path = null;
                if (string.IsNullOrEmpty(c47Path)) c47Path = null;
            }
            catch (Exception) { /* réglage illisible : pas d'émulateur */ }
        }

        static void Save()
        {
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllLines(SettingsFile, new[] { "Chemin=" + (path ?? ""), "C47=" + (c47Path ?? ""), "Defaut=" + (c47First ? "C47" : "DM42") });
            }
            catch (Exception) { /* dossier inaccessible : réglage non mémorisé */ }
        }

        /// <summary>Exécutable d'un émulateur déjà ouvert (Free42 / Plus42), ou null.</summary>
        public static string DetectRunning()
        {
            foreach (var p in KnownNames.SelectMany(Process.GetProcessesByName))
            {
                try { if (p.MainModule?.FileName is string f) return f; }
                catch (Exception) { /* processus protégé */ }
            }
            return null;
        }

        /// <summary>Demande l'exécutable de l'émulateur ; true si un fichier a été choisi.</summary>
        public static bool Choose(IWin32Window owner)
        {
            using (var dlg = new OpenFileDialog
            {
                Title = Lang.T("Choisir l'émulateur (Free42, Plus42…)"),
                Filter = Lang.T("Émulateurs HP-42S|Free42*.exe;Plus42*.exe|Programmes (*.exe)|*.exe"),
                FilterIndex = ExePath.Length == 0 || KnownNames.Any(n => DisplayName.Equals(n, StringComparison.OrdinalIgnoreCase)) ? 1 : 2
            })
            {
                if (ExePath.Length > 0 && Directory.Exists(Path.GetDirectoryName(ExePath)))
                {
                    dlg.InitialDirectory = Path.GetDirectoryName(ExePath);
                    dlg.FileName = Path.GetFileName(ExePath);
                }
                if (dlg.ShowDialog(owner) != DialogResult.OK) return false;
                ExePath = dlg.FileName;
                return true;
            }
        }

        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);

        /// <summary>
        /// Copie le listing dans le presse-papiers puis affiche l'émulateur réglé (le lance s'il n'est pas ouvert).
        /// Renvoie un message pour la barre d'état.
        /// </summary>
        public static string Send(IWin32Window owner, string listing, int steps)
        {
            try { Clipboard.SetText(listing); }
            catch (Exception ex) { return Lang.T("Presse-papiers indisponible : ") + ex.Message; }

            // émulateur réglé déjà ouvert ? sinon, sans réglage, n'importe quel Free42 / Plus42 ouvert
            Process proc = null;
            string exe = ExePath;
            string name = exe.Length > 0 ? Path.GetFileNameWithoutExtension(exe) : null;
            var candidates = name != null ? Process.GetProcessesByName(name) : KnownNames.SelectMany(Process.GetProcessesByName).ToArray();
            proc = candidates.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);

            if (proc == null)
            {
                if (exe.Length == 0 || !File.Exists(exe))
                {
                    if (!Choose(owner)) return Lang.T("Listing copié. Ouvrez l'émulateur, passez en mode PRGM puis Ctrl+V.");
                    exe = ExePath;
                }
                try
                {
                    proc = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) });
                    proc?.WaitForInputIdle(5000);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(owner, Lang.F("Impossible de lancer {0} :\n{1}\n\nChoisissez l'émulateur dans Fichier → Paramètres.", exe, ex.Message),
                                    Lang.T("Émulateur"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return Lang.T("Listing copié, mais l'émulateur n'a pas pu être lancé.");
                }
            }
            else
            {
                IntPtr h = proc.MainWindowHandle;
                if (IsIconic(h)) ShowWindow(h, 9);   // SW_RESTORE
                SetForegroundWindow(h);
            }
            return Lang.F("Listing de {0} pas copié. Dans {1} : mode PRGM (⇧ R/S) puis Ctrl+V — le programme est ajouté à la fin de la mémoire.", steps, DisplayName);
        }

        /// <summary>Filtre du choix de l'émulateur C47, partagé avec les paramètres.</summary>
        public static string C47Filter => Lang.T("Émulateur C47 / R47|c47*.exe;r47*.exe;*.cmd|Programmes (*.exe;*.cmd)|*.exe;*.cmd");

        /// <summary>Demande l'émulateur C47 ; true si un fichier a été choisi.</summary>
        public static bool ChooseC47(IWin32Window owner)
        {
            using (var dlg = new OpenFileDialog { Title = Lang.T("Choisir l'émulateur C47 (c47.exe, r47.exe ou lancer_c47.cmd)"), Filter = C47Filter })
            {
                if (C47Path.Length > 0 && Directory.Exists(Path.GetDirectoryName(C47Path)))
                {
                    dlg.InitialDirectory = Path.GetDirectoryName(C47Path);
                    dlg.FileName = Path.GetFileName(C47Path);
                }
                if (dlg.ShowDialog(owner) != DialogResult.OK) return false;
                C47Path = dlg.FileName;
                return true;
            }
        }

        /// <summary>
        /// Range le programme dans le dossier PROGRAMS de l'émulateur C47 puis l'affiche (le lance s'il n'est pas ouvert).
        /// L'émulateur C47 ne charge pas de programme au démarrage : il reste READP à faire. Renvoie un message pour la barre d'état.
        /// </summary>
        public static string SendC47(IWin32Window owner, byte[] p47File, string name)
        {
            string sim = C47Path;
            if (sim.Length == 0 || !File.Exists(sim))
            {
                if (!ChooseC47(owner)) return Lang.T("Aucun émulateur C47 choisi (Fichier → Paramètres).");
                sim = C47Path;
            }
            string folder = Path.GetDirectoryName(sim);
            string target = Path.Combine(folder, "PROGRAMS", name + ".p47");
            // le dossier PROGRAMS contient souvent les programmes d'exemple : pas d'écrasement sans accord
            try
            {
                if (File.Exists(target) && !File.ReadAllBytes(target).AsSpan().SequenceEqual(p47File)
                    && MessageBox.Show(owner, Lang.F("Remplacer {0} ?\n\nCe fichier existe déjà dans le dossier PROGRAMS de l'émulateur C47.", target),
                                       Lang.T("Émulateur"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return Lang.T("Envoi annulé.");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { /* illisible : l'écriture dira pourquoi */ }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.WriteAllBytes(target, p47File);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(owner, Lang.F("Impossible d'écrire {0}\n{1}", target, ex.Message), Lang.T("Émulateur"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return Lang.T("Programme non copié.");
            }

            // émulateur de ce dossier déjà ouvert ? (le lanceur .cmd démarre c47.exe ou r47.exe à côté de lui)
            var running = new[] { "c47", "r47" }.SelectMany(Process.GetProcessesByName).FirstOrDefault(p =>
            {
                try { return p.MainWindowHandle != IntPtr.Zero && string.Equals(Path.GetDirectoryName(p.MainModule?.FileName), folder, StringComparison.OrdinalIgnoreCase); }
                catch (Exception) { return false; }
            });
            if (running != null)
            {
                IntPtr h = running.MainWindowHandle;
                if (IsIconic(h)) ShowWindow(h, 9);   // SW_RESTORE
                SetForegroundWindow(h);
            }
            else
            {
                try
                {
                    var start = sim.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || sim.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                        ? new ProcessStartInfo("cmd.exe", "/c \"\"" + sim + "\"\"") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = folder }
                        : new ProcessStartInfo(sim) { UseShellExecute = true, WorkingDirectory = folder };
                    Process.Start(start);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(owner, Lang.F("Impossible de lancer {0} :\n{1}\n\nChoisissez l'émulateur C47 dans Fichier → Paramètres.", sim, ex.Message),
                                    Lang.T("Émulateur"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return Lang.F("{0}.p47 copié dans PROGRAMS, mais l'émulateur C47 n'a pas pu être lancé.", name);
                }
            }
            return Lang.F("{0}.p47 copié dans {1}. Dans l'émulateur C47 : READP puis choisissez ce fichier.", name, Path.GetDirectoryName(target));
        }

        /// <summary>Pile après l'exécution : X Y Z T, et A B C D quand la pile a 8 niveaux (SSIZE8, réglage d'usine de la C47).</summary>
        public sealed record C47Run(string X, string Y, string Z, string T, string Alpha, string Error, bool Stack8 = false,
                                    string A = "", string B = "", string C = "", string D = "", string Log = "",
                                    System.Drawing.Image Screen = null);

        /// <summary>
        /// Exécute un programme dans le simulateur C47 sans fenêtre (--headless, langage de commandes -e) : la pile de départ,
        /// XEQ entry, chaque INPUT reçoit la valeur suivante puis R/S. Renvoie la pile, ALPHA (registre K des instructions 42α…)
        /// ou l'erreur. Les menus (varmenu, menu) demandent les touches de menu et ne sont pas simulés ainsi.
        /// </summary>
        /// <param name="logVar">variable texte relue à la fin (journal des messages affichés), ou null</param>
        /// <summary>Simulation C47 en cours (la fenêtre reste utilisable pendant l'attente) ; C47Cancel = true l'interrompt.</summary>
        public static bool C47Busy { get; private set; }
        public static volatile bool C47Cancel;

        /// <param name="pauseSnap">le programme exécute SNAP à la place de sa PAUSE de fin (SimCopy.Build) : sa capture est l'écran rendu</param>
        public static C47Run RunC47(IWin32Window owner, byte[] p47File, string entry, double[] stack, double[] inputs, string logVar = null,
                                    bool pauseSnap = false)
        {
            if (C47Busy) return new C47Run("", "", "", "", "", Lang.T("Une simulation C47 est déjà en cours."));
            C47Busy = true;
            C47Cancel = false;
            try { return RunC47Core(owner, p47File, entry, stack, inputs, logVar, pauseSnap); }
            finally { C47Busy = false; }
        }

        static C47Run RunC47Core(IWin32Window owner, byte[] p47File, string entry, double[] stack, double[] inputs, string logVar, bool pauseSnap)
        {
            string sim = C47Path;
            if (sim.Length == 0 || !File.Exists(sim))
            {
                if (!ChooseC47(owner)) return new C47Run("", "", "", "", "", Lang.T("Aucun émulateur C47 choisi (Fichier → Paramètres)."));
                sim = C47Path;
            }
            // le lanceur .cmd démarre c47.exe ou r47.exe de son dossier
            string folder = Path.GetDirectoryName(sim);
            string exe = sim.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? sim
                       : new[] { "c47.exe", "r47.exe" }.Select(n => Path.Combine(folder, n)).FirstOrDefault(File.Exists);
            if (exe == null) return new C47Run("", "", "", "", "", Lang.F("c47.exe introuvable dans {0}", folder));

            string file = Path.Combine(Path.GetTempPath(), "dm42editor", "py47sim.p47");
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllBytes(file, p47File);
            // écran de la calculatrice à la fin (SNAP : py47lcd.bmp, avec deux fichiers de registres à effacer)
            string snap = Path.Combine(Path.GetDirectoryName(file), "py47lcd");
            string[] snapFiles = { snap + ".bmp", snap + ".REGS.TSV", snap + ".REGS.TSV.T47.TSV" };
            foreach (var f in snapFiles) try { File.Delete(f); } catch (IOException) { }
            string N(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            var script = new System.Text.StringBuilder($"readp {file.Replace('\\', '/')}; ");
            foreach (var v in stack) script.Append($"nim {N(v)}; ");
            // SNAP du programme (PAUSE de fin, voir SimCopy) : sa capture prend ce nom au lieu d'un nom daté
            string pause = Path.Combine(Path.GetDirectoryName(file), "py47pause");
            string[] pauseFiles = { pause + ".bmp", pause + ".REGS.TSV", pause + ".REGS.TSV.T47.TSV" };
            foreach (var f in pauseFiles) try { File.Delete(f); } catch (IOException) { }
            if (pauseSnap) script.Append($"iofile {pause.Replace('\\', '/')}.bmp; tsvfn {pause.Replace('\\', '/')}.REGS.TSV; ");
            script.Append($"xeq {entry}; ");
            if (pauseSnap) script.Append("tsvfn; ");
            foreach (var v in inputs) script.Append($"nim {N(v)}; press R/S; ");
            script.Append($"snap {snap.Replace('\\', '/')}; ");
            if (logVar != null) script.Append($"puts \"@@LOG=[var {logVar}]\"; ");
            script.Append("puts \"@@X=[reg X]\"; puts \"@@Y=[reg Y]\"; puts \"@@Z=[reg Z]\"; puts \"@@T=[reg T]\"; puts \"@@K=[reg K]\"; "
                        + "puts \"@@S8=[flag SSIZE8]\"; puts \"@@A=[reg A]\"; puts \"@@B=[reg B]\"; puts \"@@C=[reg C]\"; puts \"@@D=[reg D]\"");

            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = folder,
                // le simulateur écrit en UTF-8 ; sans console, l'éditeur lirait en page OEM (¦ → ┬ª : messages collés)
                StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8,
            };
            foreach (var a in new[] { "--reset", "--headless", "-e", script.ToString() }) psi.ArgumentList.Add(a);
            // bibliothèques GTK du dossier voisin, comme lancer_c47.cmd
            string gtk = Path.GetFullPath(Path.Combine(folder, "..", "c47build", "gtkroot", "mingw64"));
            if (Directory.Exists(gtk))
            {
                psi.Environment["PATH"] = Path.Combine(gtk, "bin") + ";" + Environment.GetEnvironmentVariable("PATH");
                psi.Environment["XDG_DATA_DIRS"] = Path.Combine(gtk, "share");
            }
            try
            {
                using (var p = Process.Start(psi))
                {
                    var outTask = p.StandardOutput.ReadToEndAsync();
                    var errTask = p.StandardError.ReadToEndAsync();
                    // attente sans figer la fenêtre : elle se redessine, et l'utilisateur peut interrompre (C47Cancel)
                    var clock = Stopwatch.StartNew();
                    while (!p.WaitForExit(50))
                    {
                        Application.DoEvents();
                        if (C47Cancel || clock.ElapsedMilliseconds > 30000)
                        {
                            try { p.Kill(); } catch (InvalidOperationException) { }
                            return new C47Run("", "", "", "", "", C47Cancel
                                ? Lang.T("Simulation C47 interrompue.")
                                : Lang.T("Le programme ne s'est pas terminé en 30 s (boucle sans fin, ou saisie attendue par un menu ?)."));
                        }
                    }
                    p.WaitForExit();
                    string text = outTask.Result + errTask.Result;
                    string Get(string k) => text.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("@@" + k + "=")).Select(l => l.Substring(k.Length + 3)).LastOrDefault() ?? "";
                    // valeurs de la pile en codes de la police C47 : exposants lisibles (TEXTEⅢ → TEXTE², ×⑽ⅫⅣ → ×10⁻³)
                    string Reg(string k) => C47.Core.C47Text.Display(Get(k)).Replace("⑽", "10");
                    // le simulateur écrit aussi son journal de débogage : seuls les refus de chargement ou d'appel sont gardés
                    string err = string.Join("\n", text.Split('\n').Select(l => l.Trim())
                        .Where(l => l.StartsWith("xeq:") || l.StartsWith("readp") || l.StartsWith("nim:") || l.StartsWith("press:")
                                    || l.Contains("not found") || l.Contains("invalid command") || l.Contains("Structure")));
                    return new C47Run(Reg("X"), Reg("Y"), Reg("Z"), Reg("T"), Get("K"), err, Get("S8") == "1", Reg("A"), Reg("B"), Reg("C"), Reg("D"), Get("LOG"),
                                      // l'écran capturé pendant la pause du programme, sinon celui de la fin
                                      File.Exists(pauseFiles[0]) ? LoadScreen(pauseFiles, snapFiles) : LoadScreen(snapFiles, pauseFiles));
                }
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is IOException)
            {
                return new C47Run("", "", "", "", "", ex.Message);
            }
        }

        /// <summary>Capture SNAP du simulateur, chargée en mémoire (le fichier reste libre) ; les fichiers sont effacés.</summary>
        static System.Drawing.Image LoadScreen(string[] files, string[] unused)
        {
            foreach (var f in unused) try { File.Delete(f); } catch (IOException) { }
            System.Drawing.Image img = null;
            try
            {
                if (File.Exists(files[0]))
                    using (var bmp = new System.Drawing.Bitmap(files[0])) img = new System.Drawing.Bitmap(bmp);
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException) { }
            foreach (var f in files) try { File.Delete(f); } catch (IOException) { }
            return img;
        }

        /// <summary>Texte d'un onglet prêt à coller : sans commentaires ni lignes vides ; renvoie aussi le nombre de pas.</summary>
        public static string CleanListing(string text, out int steps)
        {
            var lines = new List<string>();
            steps = 0;
            foreach (var raw in (text ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                Flowchart.FlowGraph.SplitComment(raw, out string code, out _, out _);
                code = code.TrimEnd();
                if (code.Trim().Length == 0) continue;
                lines.Add(code);
                if (!code.TrimStart().StartsWith("{")) steps++;
            }
            return string.Join("\r\n", lines);
        }
    }
}
