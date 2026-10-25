using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace dm42editor
{
    /// <summary>Icônes vectorielles (ToolbarIcons) de la barre d'outils et des menus, redessinées à chaque changement de thème.</summary>
    public partial class Form1
    {
        const int ToolbarIconSize = 24;
        const int MenuIconSize = 16;

        /// <summary>Icône de chaque entrée de menu, d'après son texte (menus Fichier, Python, Aide et menu contextuel).</summary>
        static readonly Dictionary<string, string> MenuIcons = new Dictionary<string, string>
        {
            // Fichier
            ["Nouveau programme"] = "new",
            ["Ouvrir un programme…"] = "open",
            ["Enregistrer"] = "save",
            ["Enregistrer sous…"] = "save",
            ["Tout enregistrer"] = "saveAll",
            ["Paramètres…"] = "settings",
            ["Convertir la syntaxe HP-41 → DM42…"] = "convert",
            ["Encoder et enregistrer en .raw…"] = "encode",
            ["Envoyer vers l'émulateur"] = "send",
            ["Imprimer le code…"] = "print",
            ["Logigramme du programme…"] = "flowchart",
            ["Quitter"] = "quit",
            // Python
            ["Nouveau programme Python"] = "python",
            ["Ouvrir un fichier Python…"] = "open",
            ["Afficher l'éditeur Python"] = "window",
            ["Exemples"] = "examples",
            ["Aide Python → RPN"] = "help",
            // Aide
            ["Équivalences"] = "list",
            ["Aide PDF"] = "pdf",
            ["À propos"] = "info",
            // menu contextuel de l'éditeur
            ["Couper"] = "cut",
            ["Copier"] = "copy",
            ["Copier sans les commentaires"] = "copyClean",
            ["Coller"] = "paste",
            ["Tout sélectionner"] = "selectAll",
            ["Annuler"] = "undo",
            ["Rétablir"] = "redo",
            ["Rechercher…"] = "find",
            ["Remplacer…"] = "replace",
            ["Afficher les numéros de pas"] = "stepsShow",
            ["Masquer les numéros de pas"] = "stepsHide",
            ["Voir les octets .raw"] = "raw",
            ["Aligner les commentaires"] = "align",
            // menu « Aller à… »
            ["Aller à la ligne de programme…"] = "goto",
        };

        static Color IconInk => AppTheme.Dark ? Color.FromArgb(215, 215, 210) : Color.FromArgb(55, 58, 64);
        static Color IconPaper => AppTheme.Dark ? Color.FromArgb(45, 45, 48) : Color.White;

        void SetToolbarIcons()
        {
            var map = new (ToolStripItem Item, string Name)[]
            {
                (newToolStripButton, "new"), (openToolStripButton, "open"), (saveToolStripButton, "save"), (printToolStripButton, "print"),
                (cutToolStripButton, "cut"), (copyToolStripButton, "copy"), (pasteToolStripButton, "paste"),
                (btHighlightCurrentLine, "highlight"), (undoStripButton, "undo"), (redoStripButton, "redo"),
                (backStripButton, "back"), (forwardStripButton, "forward"),
                (bookmarkPlusButton, "bookmarkAdd"), (bookmarkMinusButton, "bookmarkRemove"),
                (toolStripButton1, "stepsShow"), (toolStripButton2, "stepsHide"), (gotoButton, "goto"),
            };
            foreach (var (item, name) in map) SetIcon(item, name, ToolbarIconSize);
            toolStrip1.ImageScalingSize = new Size(ToolbarIconSize, ToolbarIconSize);
            SetMenuIcons();
        }

        /// <summary>Icônes des menus déroulants (barre de menus et menu contextuel de l'éditeur).</summary>
        void SetMenuIcons()
        {
            foreach (ToolStripItem top in menuStrip1.Items) MenuItemIcons(top);
            foreach (ToolStripItem it in cmMain.Items) MenuItemIcons(it);
        }

        void MenuItemIcons(ToolStripItem item)
        {
            if (!(item is ToolStripMenuItem mi)) return;
            bool topLevel = mi.Owner is MenuStrip;
            if (!topLevel && MenuIcons.TryGetValue(mi.Text, out string name)) SetIcon(mi, name, MenuIconSize);
            foreach (ToolStripItem sub in mi.DropDownItems) MenuItemIcons(sub);
        }

        /// <summary>Icône d'une entrée de menu créée à la volée (menu « Aller à… »).</summary>
        static Image MenuIcon(string name) => ToolbarIcons.Make(name, MenuIconSize, IconInk, IconPaper);

        static void SetIcon(ToolStripItem item, string name, int size)
        {
            var old = item.Image;
            item.Image = ToolbarIcons.Make(name, size, IconInk, IconPaper);
            item.ImageTransparentColor = Color.Empty;
            item.ImageScaling = ToolStripItemImageScaling.None;
            old?.Dispose();
        }
    }
}
