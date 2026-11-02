using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Dm42.Editor;

/// <summary>
/// Langue de l'interface. Le français écrit dans le code sert de clé : resources\lang_en.txt donne, ligne par ligne,
/// « français TAB anglais ». Un texte absent de la table reste en français.
/// </summary>
public static class Lang
{
    static readonly Dictionary<string, string> table = new Dictionary<string, string>();
    static readonly Dictionary<string, string> source = new Dictionary<string, string>();

    static string SettingFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dm42editor", "langue.ini");

    /// <summary>Choix enregistré : auto (langue de Windows), fr ou en. Pris en compte au prochain démarrage.</summary>
    public static string Setting
    {
        get
        {
            try
            {
                string v = File.Exists(SettingFile) ? File.ReadAllText(SettingFile).Trim() : "";
                return v == "fr" || v == "en" ? v : "auto";
            }
            catch (Exception) { return "auto"; }
        }
        set
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingFile));
                File.WriteAllText(SettingFile, value);
            }
            catch (Exception) { /* le choix sera redemandé */ }
        }
    }

    static Lang()
    {
        string s = Setting;
        bool english = s == "en" || (s == "auto" && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "fr");
        if (english) Load();
        Py42.Core.Tr.English = english;
        C47.Core.Tr.English = english;
    }

    static void Load()
    {
        using (var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("lang_en.txt"))
        {
            if (st == null) return;
            using (var r = new StreamReader(st))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    int tab = line.IndexOf('\t');
                    if (tab <= 0) continue;
                    string fr = line.Substring(0, tab).Replace("\\n", "\n");
                    string en = line.Substring(tab + 1).Replace("\\n", "\n");
                    table[fr] = en;
                    source[en] = fr;
                }
            }
        }
    }

    /// <summary>Charge la langue avant la première fenêtre.</summary>
    public static void Init() { }

    /// <summary>Nom du fichier de données dans la langue choisie : truc.csv devient truc_en.csv s'il existe.</summary>
    public static string DataFile(string name)
    {
        if (table.Count == 0) return name;
        string en = Path.GetFileNameWithoutExtension(name) + "_en" + Path.GetExtension(name);
        return File.Exists(Path.Combine(Application.StartupPath, en)) ? en : name;
    }

    /// <summary>Traduit un texte français.</summary>
    public static string T(string fr) => fr != null && table.TryGetValue(fr, out string en) ? en : fr;

    /// <summary>Traduit un modèle français « {0} pas » puis le remplit.</summary>
    public static string F(string fr, params object[] args) => string.Format(T(fr), args);

    /// <summary>Texte français d'origine d'un texte affiché (les icônes des menus sont repérées par leur texte français).</summary>
    public static string Source(string text) => text != null && source.TryGetValue(text, out string fr) ? fr : text;

    static string Keys(string s) => table.Count == 0 || s == null ? s : s.Replace("Maj", "Shift").Replace("Suppr", "Del");

    /// <summary>Traduit une fenêtre : titres, menus, boutons, colonnes, menus contextuels.</summary>
    public static void Apply(Control root)
    {
        if (table.Count == 0) return;
        Walk(root);
        // menus contextuels attachés plus tard à un contrôle (éditeurs des onglets…)
        for (var t = root.GetType(); t != null && t != typeof(Form); t = t.BaseType)
            foreach (var fi in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                if (typeof(ToolStripDropDown).IsAssignableFrom(fi.FieldType) && fi.GetValue(root) is ToolStripDropDown dd)
                    Items(dd.Items);
    }

    static void Walk(Control c)
    {
        if (c.ContextMenuStrip != null) Items(c.ContextMenuStrip.Items);
        if (c is CodeEditor || c is TextBoxBase || c is ComboBox || c is ListBox) return;
        c.Text = T(c.Text);
        switch (c)
        {
            case ToolStrip ts: Items(ts.Items); break;
            case ListView lv: foreach (ColumnHeader h in lv.Columns) h.Text = T(h.Text); break;
            case DataGridView g: foreach (DataGridViewColumn col in g.Columns) col.HeaderText = T(col.HeaderText); break;
        }
        foreach (Control child in c.Controls) Walk(child);
    }

    static void Items(ToolStripItemCollection items)
    {
        foreach (ToolStripItem it in items)
        {
            it.ToolTipText = T(it.ToolTipText);
            if (it is ToolStripControlHost) continue;
            it.Text = T(it.Text);
            if (it is ToolStripMenuItem mi) mi.ShortcutKeyDisplayString = Keys(mi.ShortcutKeyDisplayString);
            if (it is ToolStripDropDownItem dd) Items(dd.DropDownItems);
        }
    }
}
