using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Brisk
{
    public partial class MainForm
    {
        // ==============================================================
        //  VERKTOY
        // ==============================================================
        // Gode gratisverktoy fra andre. Velg en flis, trykk Kjor. Alt som skjer
        // vises i konsollen nederst, og Stopp avbryter det som kjorer.
        //
        // Listen ligger i src/ExternalTools.cs. Se docs/verktoy.md.
        TextBox toolsOut;
        Label toolsNow;
        FlatBtn toolsRun, toolsStop;
        ToolTile toolsPicked;
        Process toolsProc;

        Panel PageTools()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            // --- konsollen nederst ---
            Panel outHost = new Panel();
            outHost.Dock = DockStyle.Bottom;
            outHost.Height = 236;
            outHost.BackColor = Theme.Bg;
            outHost.Padding = new Padding(0, 10, 0, 0);
            toolsOut = Console(outHost, 0);

            Label outNote;
            Panel outHead = Widgets.Head(L.T("Konsoll"), out outNote);
            outNote.Text = L.T("Alt verktøyet skriver ut vises her.");
            outHost.Controls.Add(outHead);

            // --- kjør og stopp ---
            Panel bar = new Panel();
            bar.Dock = DockStyle.Bottom;
            bar.Height = 58;
            bar.BackColor = Theme.Bg;

            toolsRun = new FlatBtn(L.T("Kjør"));
            toolsRun.Primary().Big();
            toolsRun.Width = 130; toolsRun.Height = 40;
            toolsRun.Location = new Point(0, 8);
            toolsRun.Enabled = false;
            toolsRun.Click += async delegate { await RunPicked(); };

            toolsStop = new FlatBtn(L.T("Stopp"));
            toolsStop.Danger().Big();
            toolsStop.Width = 130; toolsStop.Height = 40;
            toolsStop.Location = new Point(142, 8);
            toolsStop.Enabled = false;
            toolsStop.Click += delegate { StopTool(); };

            toolsNow = Theme.Lbl(L.T("Velg et verktøy over."), Theme.F, Theme.Muted);
            toolsNow.AutoSize = false;
            toolsNow.Location = new Point(290, 19);
            toolsNow.Height = 20;

            bar.Controls.Add(toolsRun);
            bar.Controls.Add(toolsStop);
            bar.Controls.Add(toolsNow);
            bar.Resize += delegate
            {
                toolsNow.Width = Math.Max(120, bar.Width - toolsNow.Left);
            };

            // --- flisene ---
            FlowLayoutPanel grid = new FlowLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.BackColor = Theme.Bg;
            grid.AutoScroll = true;
            grid.WrapContents = true;
            grid.FlowDirection = FlowDirection.LeftToRight;
            grid.Padding = new Padding(0, 0, 16, 8);

            List<ToolTile> tiles = new List<ToolTile>();
            foreach (ExternalTool t in ExternalTools.All())
            {
                ToolTile tile = new ToolTile(t);
                tile.Margin = new Padding(0, 0, 12, 12);
                ToolTile self = tile;
                tile.Click += delegate { PickTool(self, tiles); };
                tiles.Add(tile);
                grid.Controls.Add(tile);
            }

            Label note = Theme.Lbl(
                L.T("Andres programmer, ikke deler av Brisk. Velg ett, så ser du kommandoen før du kjører den."),
                Theme.FSmall, Theme.Muted);
            note.AutoSize = false;
            note.Dock = DockStyle.Top;
            note.Height = 28;

            p.Controls.Add(grid);
            p.Controls.Add(bar);
            p.Controls.Add(outHost);
            p.Controls.Add(note);

            Defer(delegate { Append(toolsOut, L.T("Ingenting kjører.")); });
            return p;
        }

        void PickTool(ToolTile picked, List<ToolTile> all)
        {
            foreach (ToolTile t in all) t.Picked = (t == picked);
            toolsPicked = picked;
            toolsRun.Enabled = toolsProc == null;
            toolsNow.Text = picked.Tool.Name + "   ·   " + picked.Tool.Command;
            toolsNow.ForeColor = picked.Tool.Remote ? Theme.Warn : Theme.Muted;
        }

        void StopTool()
        {
            Process p = toolsProc;
            if (p == null) return;
            Append(toolsOut, L.T("Stopper …"));
            Util.StopTree(p);
        }

        async System.Threading.Tasks.Task RunPicked()
        {
            if (toolsPicked == null) return;
            ExternalTool t = toolsPicked.Tool;

            // Kommandoer som henter kode fra nettet fortjener et ekstra steg.
            // Brisk kan ikke se hva som ligger paa den andre siden.
            if (t.Remote)
            {
                if (MessageBox.Show(this,
                        L.F("{0} startes med denne kommandoen:", t.Name) + "\r\n\r\n" +
                        t.Command + "\r\n\r\n" +
                        L.T("Den henter kode fra nettet og kjører den med en gang, som administrator. Brisk kan ikke se hva koden gjør før den kjører. Dette er måten laget bak verktøyet selv anbefaler.") +
                        "\r\n\r\n" + L.T("Fortsette?"),
                        t.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            if (t.OwnWindow)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("powershell.exe");
                    psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" +
                                    t.Command.Replace("\"", "\\\"") + "\"";
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                    Process.Start(psi);
                    Append(toolsOut, "");
                    Append(toolsOut, "> " + t.Command);
                    Append(toolsOut, L.F("{0} åpnet i sitt eget vindu. Utdata derfra vises ikke her.", t.Name));
                    Util.Log("Verktoy startet: " + t.Command);
                }
                catch (Exception ex)
                {
                    Append(toolsOut, L.F("Klarte ikke starte {0}: ", t.Name) + ex.Message);
                }
                return;
            }

            string full = Runnable(t.Command);
            string exe, args;
            Split(full, out exe, out args);

            Append(toolsOut, "");
            Append(toolsOut, "> " + full);
            Util.Log("Verktoy: " + full);

            toolsRun.Enabled = false;
            toolsStop.Enabled = true;
            Status(L.F("Kjører {0} …", t.Name));
            Busy(true);

            int code = -1;
            await System.Threading.Tasks.Task.Run(delegate
            {
                code = Util.Run(exe, args,
                    delegate(string line) { Append(toolsOut, line); },
                    delegate(Process proc) { toolsProc = proc; });
            });

            toolsProc = null;
            toolsStop.Enabled = false;
            toolsRun.Enabled = true;
            Busy(false);
            Status("");

            Append(toolsOut, code == 0
                ? L.F("{0} er ferdig.", t.Name)
                : L.F("Avsluttet med kode {0}.", code));
        }

        // winget spor om kildevilkaar og pakkevilkaar forste gang. Brisk fanger
        // utdata og har ingen stdin, saa uten disse flaggene ville kjoringen bli
        // staaende og vente paa et svar som aldri kommer.
        static string Runnable(string command)
        {
            string c = (command ?? "").Trim();
            if (!c.StartsWith("winget ", StringComparison.OrdinalIgnoreCase)) return c;
            if (c.IndexOf("--accept-source-agreements", StringComparison.OrdinalIgnoreCase) >= 0) return c;
            return c + " --accept-source-agreements --accept-package-agreements --disable-interactivity";
        }

        // Deler "winget install Noe.Id" i program og argumenter.
        static void Split(string command, out string exe, out string args)
        {
            string s = (command ?? "").Trim();
            int i = s.IndexOf(' ');
            if (i < 0) { exe = s; args = ""; return; }
            exe = s.Substring(0, i);
            args = s.Substring(i + 1);
        }
    }
}
