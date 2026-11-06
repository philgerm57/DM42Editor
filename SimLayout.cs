using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace dm42editor
{
    /// <summary>Mise en page commune des simulateurs (éditeur RPN, fenêtre Python) : espaces autour de l'écran et du texte.</summary>
    static class SimLayout
    {
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        const int EM_SETMARGINS = 0xD3, EC_LEFTMARGIN = 1;

        /// <summary>Marge intérieure à gauche d'une zone de texte : le texte ne colle plus au bord.</summary>
        public static void LeftMargin(TextBox tb, int px)
        {
            void Apply() => SendMessage(tb.Handle, EM_SETMARGINS, (IntPtr)EC_LEFTMARGIN, (IntPtr)px);
            tb.HandleCreated += (s, e) => Apply();
            tb.FontChanged += (s, e) => { if (tb.IsHandleCreated) Apply(); };   // un changement de police remet la marge à zéro
            if (tb.IsHandleCreated) Apply();
        }

        /// <summary>Espace entre l'écran C47 et le texte : du même côté que l'écran, visible avec lui.
        /// À ajouter au conteneur entre le texte (Fill) et l'écran.</summary>
        public static Panel Gap(PictureBox lcd, int px)
        {
            var gap = new Panel { Width = px, Height = px, Dock = lcd.Dock, Visible = false };
            lcd.VisibleChanged += (s, e) => gap.Visible = lcd.Visible;
            lcd.DockChanged += (s, e) => gap.Dock = lcd.Dock;
            return gap;
        }
    }
}
