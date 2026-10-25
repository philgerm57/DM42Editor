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
        static bool loaded;

        /// <summary>Chemin complet de l'exécutable de l'émulateur (vide : pas encore choisi).</summary>
        public static string ExePath
        {
            get { Load(); return path ?? ""; }
            set { Load(); path = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); Save(); }
        }

        /// <summary>Nom affiché : « Free42Decimal », « Plus42 »… ou « l'émulateur ».</summary>
        public static string DisplayName => ExePath.Length > 0 ? Path.GetFileNameWithoutExtension(ExePath) : "l'émulateur";

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (File.Exists(SettingsFile))
                {
                    foreach (var l in File.ReadAllLines(SettingsFile))
                        if (l.StartsWith("Chemin=")) path = l.Substring(7).Trim();
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
            }
            catch (Exception) { /* réglage illisible : pas d'émulateur */ }
        }

        static void Save()
        {
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllLines(SettingsFile, new[] { "Chemin=" + (path ?? "") });
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
                Title = "Choisir l'émulateur (Free42, Plus42…)",
                Filter = "Émulateurs HP-42S|Free42*.exe;Plus42*.exe|Programmes (*.exe)|*.exe",
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
            catch (Exception ex) { return "Presse-papiers indisponible : " + ex.Message; }

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
                    if (!Choose(owner)) return "Listing copié. Ouvrez l'émulateur, passez en mode PRGM puis Ctrl+V.";
                    exe = ExePath;
                }
                try
                {
                    proc = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) });
                    proc?.WaitForInputIdle(5000);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(owner, "Impossible de lancer " + exe + " :\n" + ex.Message + "\n\nChoisissez l'émulateur dans Fichier → Paramètres.",
                                    "Émulateur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return "Listing copié, mais l'émulateur n'a pas pu être lancé.";
                }
            }
            else
            {
                IntPtr h = proc.MainWindowHandle;
                if (IsIconic(h)) ShowWindow(h, 9);   // SW_RESTORE
                SetForegroundWindow(h);
            }
            return $"Listing de {steps} pas copié. Dans {DisplayName} : mode PRGM (⇧ R/S) puis Ctrl+V — le programme est ajouté à la fin de la mémoire.";
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
