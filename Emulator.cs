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
