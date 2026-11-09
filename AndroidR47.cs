using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace dm42editor
{
    /// <summary>
    /// Envoi d'un .p47 vers l'émulateur C47 / R47 d'un téléphone Android branché en USB, par adb (outils Android
    /// « platform-tools », débogage USB activé sur le téléphone). Sur Android, READP ouvre le sélecteur de fichiers du système
    /// sur le sous-dossier PROGRAMS du dossier de travail réglé dans l'application : le programme est copié là, dans le
    /// stockage partagé (dossier lu dans les préférences par run-as, possible avec une version « debug » de l'application),
    /// ou dans Documents/R47/PROGRAMS. L'application est ensuite mise au premier plan : il reste READP à faire.
    /// </summary>
    static class AndroidR47
    {
        /// <summary>Paquets reconnus : le nom contient r47 ou c47 (io.github.ppigazzini.r47zen, sa version .debug…).</summary>
        static readonly Regex AppPattern = new Regex(@"(^|[.\-_])[rc]47", RegexOptions.IgnoreCase);

        /// <summary>adb.exe : PATH, puis ANDROID_HOME / ANDROID_SDK_ROOT, puis les dossiers d'installation habituels du SDK.</summary>
        public static string FindAdb()
        {
            var dirs = new List<string>();
            dirs.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
            foreach (var v in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
                if (Environment.GetEnvironmentVariable(v) is string sdk && sdk.Length > 0) dirs.Add(Path.Combine(sdk, "platform-tools"));
            dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk", "platform-tools"));
            dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Android", "platform-tools"));
            dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Android", "android-sdk", "platform-tools"));
            dirs.Add(@"C:\platform-tools");
            foreach (var d in dirs.Where(d => !string.IsNullOrWhiteSpace(d)))
            {
                try
                {
                    string f = Path.Combine(d.Trim().Trim('"'), "adb.exe");
                    if (File.Exists(f)) return f;
                }
                catch (ArgumentException) { /* entrée de PATH invalide */ }
            }
            return null;
        }

        sealed class Result
        {
            public int Code;
            public byte[] Out = Array.Empty<byte>();
            public string Err = "";
            public string Text => Encoding.UTF8.GetString(Out);
        }

        /// <summary>Lance adb et attend sa fin (20 s au plus : le premier appel démarre le serveur adb).</summary>
        static Result Adb(string adb, string args, int timeoutMs = 20000)
        {
            var psi = new ProcessStartInfo(adb, args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (var p = Process.Start(psi))
            {
                var ms = new MemoryStream();
                var outTask = p.StandardOutput.BaseStream.CopyToAsync(ms);
                var errTask = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch (Exception) { /* déjà terminé */ }
                    return new Result { Code = -1, Err = Lang.T("adb ne répond pas.") };
                }
                outTask.Wait(2000);
                errTask.Wait(2000);
                return new Result { Code = p.ExitCode, Out = ms.ToArray(), Err = errTask.IsCompleted ? errTask.Result : "" };
            }
        }

        /// <summary>Nom de fichier sûr pour la ligne de commande du téléphone : lettres, chiffres, _ - . seulement.</summary>
        static string SafeName(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name ?? "")
                sb.Append(c < 128 && (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.') ? c : '_');
            string s = sb.ToString().Trim('.');
            return s.Length == 0 ? "untitled" : s;
        }

        /// <summary>Choix dans une liste (plusieurs téléphones ou plusieurs applications R47) ; null si annulé.</summary>
        static string Pick(IWin32Window owner, string title, IList<string> items)
        {
            if (items.Count == 1) return items[0];
            using (var f = new Form
            {
                Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, ClientSize = new System.Drawing.Size(460, 220)
            })
            {
                var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
                list.Items.AddRange(items.Cast<object>().ToArray());
                list.SelectedIndex = 0;
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 30 };
                list.DoubleClick += (s, e) => { f.DialogResult = DialogResult.OK; };
                f.Controls.Add(list);
                f.Controls.Add(ok);
                f.AcceptButton = ok;
                return f.ShowDialog(owner) == DialogResult.OK ? list.SelectedItem as string : null;
            }
        }

        static string Warn(IWin32Window owner, string message, string status)
        {
            MessageBox.Show(owner, message, Lang.T("R47 Android"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return status;
        }

        /// <summary>
        /// Copie le programme dans l'application C47 / R47 du téléphone et la met au premier plan.
        /// Renvoie un message pour la barre d'état.
        /// </summary>
        public static string Send(IWin32Window owner, byte[] p47File, string name)
        {
            string adb = FindAdb();
            if (adb == null)
                return Warn(owner, Lang.T("adb.exe introuvable.\n\nInstallez les outils Android « SDK Platform-Tools » (ou Android Studio), "
                                          + "ou ajoutez leur dossier au PATH."), Lang.T("adb.exe introuvable : programme non envoyé."));
            Cursor old = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try { return SendCore(owner, adb, p47File, SafeName(name)); }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
            {
                return Warn(owner, Lang.F("Erreur d'adb :\n{0}", ex.Message), Lang.T("Programme non envoyé."));
            }
            finally { Cursor.Current = old; }
        }

        static string SendCore(IWin32Window owner, string adb, byte[] p47File, string name)
        {
            // 1. téléphone branché, débogage USB autorisé
            var lines = Adb(adb, "devices").Text.Split('\n').Select(l => l.Trim()).Where(l => l.Contains('\t')).ToList();
            var ready = lines.Where(l => l.EndsWith("\tdevice")).Select(l => l.Split('\t')[0]).ToList();
            if (ready.Count == 0)
            {
                string msg = lines.Any(l => l.EndsWith("\tunauthorized"))
                    ? Lang.T("Le téléphone n'a pas encore autorisé ce PC.\n\nDéverrouillez-le et acceptez « Autoriser le débogage USB ».")
                    : Lang.T("Aucun téléphone trouvé.\n\nBranchez-le en USB et activez le débogage USB (Options pour les développeurs).");
                return Warn(owner, msg, Lang.T("Aucun téléphone Android : programme non envoyé."));
            }
            string serial = Pick(owner, Lang.T("Choisir le téléphone"), ready);
            if (serial == null) return Lang.T("Envoi annulé.");
            string dev = "-s " + serial + " ";

            // 2. application C47 / R47 installée
            var apps = Adb(adb, dev + "shell pm list packages").Text.Split('\n')
                .Select(l => l.Trim()).Where(l => l.StartsWith("package:")).Select(l => l.Substring(8))
                .Where(p => AppPattern.IsMatch(p)).OrderBy(p => p).ToList();
            if (apps.Count == 0)
                return Warn(owner, Lang.T("Aucune application C47 / R47 trouvée sur le téléphone."), Lang.T("Programme non envoyé."));
            string pkg = Pick(owner, Lang.T("Choisir l'application"), apps);
            if (pkg == null) return Lang.T("Envoi annulé.");

            // 3. READP ouvre le sélecteur de fichiers d'Android sur <dossier de travail>/PROGRAMS (réglages de l'application) :
            //    le programme va dans ce dossier du stockage partagé, ou dans Documents/R47/PROGRAMS s'il n'y en a pas
            string work = WorkDirectory(adb, dev, pkg);
            string folder = (work ?? DefaultWorkDirectory) + "/PROGRAMS";
            string file = name + ".p47";
            string target = folder + "/" + file;

            // le dossier PROGRAMS peut contenir d'autres programmes : pas d'écrasement sans accord
            if (Adb(adb, dev + "shell ls " + Q(target)).Code == 0)
            {
                var old = Adb(adb, dev + "exec-out cat " + Q(target));
                if (old.Code != 0 || !old.Out.AsSpan().SequenceEqual(p47File))
                {
                    if (MessageBox.Show(owner, Lang.F("Remplacer {0} ?\n\nCe fichier existe déjà sur le téléphone.", target),
                                        Lang.T("R47 Android"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return Lang.T("Envoi annulé.");
                }
            }

            string local = Path.Combine(Path.GetTempPath(), "dm42editor", file);
            Directory.CreateDirectory(Path.GetDirectoryName(local));
            File.WriteAllBytes(local, p47File);
            Adb(adb, dev + "shell mkdir -p " + Q(folder));
            var push = Adb(adb, dev + "push \"" + local + "\" \"" + target + "\"");
            if (push.Code != 0)
                return Warn(owner, Lang.F("Copie vers le téléphone impossible :\n{0}", (push.Err + push.Text).Trim()), Lang.T("Programme non envoyé."));

            // 4. application au premier plan
            Adb(adb, dev + "shell monkey -p " + pkg + " -c android.intent.category.LAUNCHER 1");
            string shown = folder.Replace(Storage + "/", "");
            if (work != null)
                return Lang.F("{0} copié dans {1} du téléphone. Sur le téléphone : READP puis touchez {0}.", file, shown);
            if (!hinted) MessageBox.Show(owner, Lang.F("{0} est copié dans le dossier {1} du téléphone.\n\n"
                + "L'application n'a pas de dossier de travail : READP ouvre alors le sélecteur de fichiers ailleurs. "
                + "Pour qu'il s'ouvre directement sur vos programmes, choisissez une fois le dossier {2} "
                + "comme dossier de travail dans les réglages de l'application R47.\n\n"
                + "En attendant : READP, puis allez dans {1} dans le sélecteur.", file, shown, DefaultWorkDirectory.Replace(Storage + "/", "")),
                Lang.T("R47 Android"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            hinted = true;
            return Lang.F("{0} copié dans {1} du téléphone. Sur le téléphone : READP puis choisissez ce fichier.", file, shown);
        }

        /// <summary>
        /// Chemin pour la ligne de commande du téléphone : adb recolle ses arguments avant de les passer au shell Android,
        /// d'où les apostrophes à l'intérieur des guillemets de Windows (le nom du dossier de travail peut contenir des espaces).
        /// </summary>
        static string Q(string path) => "\"'" + path.Replace("'", "'\\''") + "'\"";

        /// <summary>Explication du dossier de travail déjà montrée pendant cette session.</summary>
        static bool hinted;

        /// <summary>Racine du stockage partagé du téléphone (stockage interne principal).</summary>
        const string Storage = "/sdcard";

        /// <summary>Dossier proposé quand l'application n'a pas de dossier de travail.</summary>
        const string DefaultWorkDirectory = Storage + "/Documents/R47";

        /// <summary>
        /// Dossier de travail réglé dans l'application (préférence work_directory_uri, un URI d'arborescence du stockage
        /// partagé), en chemin du téléphone ; null s'il n'est pas réglé ou pas lisible (run-as : version debug seulement).
        /// </summary>
        static string WorkDirectory(string adb, string dev, string pkg)
        {
            var prefs = Adb(adb, dev + "shell run-as " + pkg + " cat shared_prefs/R47WorkDirPrefs.xml shared_prefs/R47Prefs.xml");
            var m = Regex.Match(prefs.Text, @"name=""work_directory_uri"">([^<]+)<");
            if (!m.Success) return null;
            string uri = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value);
            // content://com.android.externalstorage.documents/tree/primary%3ADocuments%2FR47
            var t = Regex.Match(uri, @"^content://com\.android\.externalstorage\.documents/tree/([^/]+)");
            if (!t.Success) return null;   // autre fournisseur (Drive…) : pas de chemin de fichier
            string id = Uri.UnescapeDataString(t.Groups[1].Value);   // primary:Documents/R47 ou ABCD-1234:R47
            int colon = id.IndexOf(':');
            if (colon < 0) return null;
            string volume = id.Substring(0, colon), path = id.Substring(colon + 1).Trim('/');
            string root = volume.Equals("primary", StringComparison.OrdinalIgnoreCase) ? Storage : "/storage/" + volume;
            return path.Length == 0 ? root : root + "/" + path;
        }
    }
}
