using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

            Defer(delegate
            {
                Append(toolsOut, L.T("Ingenting kjører."));
                Defer(delegate { grid.AutoScrollPosition = new Point(0, 0); });
            });
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

            // Remote betyr at kommandoen henter kode fra nettet. Det vises med
            // oransje stripe paa flisen og oransje kommandotekst ved Kjor-knappen,
            // ikke med en ekstra dialog - kommandoen staar allerede synlig, og
            // UAC spor uansett for noe kjores som administrator.

            // Verktoy med eget grensesnitt aapnes i sitt eget vindu, som blir
            // staaende (-NoExit) saa brukeren kan jobbe videre der.
            if (t.OwnWindow)
            {
                string ownExe, ownArgs;
                Shell(t, out ownExe, out ownArgs);
                if (ownExe == "powershell.exe") ownArgs = "-NoExit " + ownArgs;
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(ownExe, ownArgs);
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                    Process.Start(psi);
                    Append(toolsOut, "");
                    Append(toolsOut, "> " + t.Command);
                    Append(toolsOut, L.F("{0} åpnet i sitt eget vindu.", t.Name));
                    Util.Log("Verktoy startet: " + t.Command);
                }
                catch (Exception ex)
                {
                    Append(toolsOut, L.F("Klarte ikke starte {0}: ", t.Name) + ex.Message);
                }
                return;
            }

            string id = WingetId(t.Command);

            // winget-pakker: installer bare hvis den mangler, og aapne den etterpaa.
            if (id.Length > 0)
            {
                toolsRun.Enabled = false;
                toolsStop.Enabled = true;
                Busy(true);

                bool har = false;
                Status(L.F("Ser etter {0} …", t.Name));
                await System.Threading.Tasks.Task.Run(delegate { har = Installed(id); });

                if (!har)
                {
                    string exe1, args1;
                    Shell(t, out exe1, out args1);
                    Append(toolsOut, "");
                    Append(toolsOut, "> " + Runnable(t.Command));
                    Util.Log("Verktoy: " + exe1 + " " + args1);
                    Status(L.F("Installerer {0} …", t.Name));

                    int rc = -1;
                    await System.Threading.Tasks.Task.Run(delegate
                    {
                        rc = Util.Run(exe1, args1,
                            delegate(string line) { Append(toolsOut, line); },
                            delegate(Process proc) { toolsProc = proc; });
                    });
                    toolsProc = null;

                    if (rc != 0)
                    {
                        Append(toolsOut, WingetSays(rc));
                        toolsStop.Enabled = false;
                        toolsRun.Enabled = true;
                        Busy(false);
                        Status("");
                        return;
                    }
                    Append(toolsOut, L.F("{0} er installert.", t.Name));
                }
                else Append(toolsOut, L.F("{0} er allerede installert.", t.Name));

                toolsStop.Enabled = false;
                toolsRun.Enabled = true;
                Busy(false);
                Status("");
                Open(t);
                return;
            }

            // Alt annet: kjor og vis utdata her.
            string exe, args;
            Shell(t, out exe, out args);

            Append(toolsOut, "");
            Append(toolsOut, "> " + Runnable(t.Command));
            Util.Log("Verktoy: " + exe + " " + args);

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

        // Starter programmet etter installasjon. Ser tre steder, i denne
        // rekkefolgen: exe-fila winget legger i Links-mappa, snarveien i
        // Start-menyen, og til slutt PATH.
        void Open(ExternalTool t)
        {
            string key = t.Launch.Length > 0 ? t.Launch : t.Name;

            string links = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\WinGet\Links", key + ".exe");
            if (File.Exists(links) && Start(links, t)) return;

            string lnk = FindShortcut(key);
            if (lnk != null && Start(lnk, t)) return;

            if (Start(key, t)) return;

            Append(toolsOut, L.F("Fant ikke {0} etter installasjonen. Åpne den fra Start-menyen.", t.Name));
        }

        bool Start(string what, ExternalTool t)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(what);
                psi.UseShellExecute = true;
                Process.Start(psi);
                Append(toolsOut, L.F("Åpnet {0}.", t.Name));
                Util.Log("Verktoy aapnet: " + what);
                return true;
            }
            catch (Exception) { return false; }
        }

        // Leter etter en snarvei i Start-menyen, bade felles og for denne brukeren.
        static string FindShortcut(string key)
        {
            string[] roots = new string[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            };
            foreach (string root in roots)
            {
                try
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (string f in Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories))
                    {
                        string name = Path.GetFileNameWithoutExtension(f);
                        if (name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return f;
                    }
                }
                catch (Exception) { }
            }
            return null;
        }

        // "winget install Noe.Id --flagg" -> "Noe.Id". Tom streng hvis det ikke
        // er en winget-installasjon.
        static string WingetId(string command)
        {
            string c = (command ?? "").Trim();
            if (!c.StartsWith("winget install ", StringComparison.OrdinalIgnoreCase)) return "";
            string rest = c.Substring("winget install ".Length).Trim();
            int sp = rest.IndexOf(' ');
            if (sp > 0) rest = rest.Substring(0, sp);
            return rest.Trim('"');
        }

        // exit 0 = pakken finnes, -1978335212 = den finnes ikke. Maalt.
        static bool Installed(string id)
        {
            try
            {
                int code;
                Util.RunCapture("winget",
                    "list --id \"" + id + "\" --exact --disable-interactivity", out code);
                return code == 0;
            }
            catch (Exception) { return false; }
        }

        // De vanligste winget-kodene i klartekst i stedet for et negativt tall.
        static string WingetSays(int code)
        {
            switch (code)
            {
                case -1978335189: return L.T("Allerede installert og oppdatert.");
                case -1978335212: return L.T("Fant ikke pakken. Sjekk ID-en.");
                case -1978334972: return L.T("Installasjonen ble avbrutt.");
                case -1978335215: return L.T("Ingen kilde svarte. Er du på nett?");
            }
            return L.F("Avsluttet med kode {0}.", code);
        }

        // Avgjor hvilket program som faktisk startes, og med hvilke argumenter.
        // Feltet Shell paa verktoyet styrer dette:
        //   ""           programmet kjores rett, med argumentene sine
        //   "powershell" hele linja sendes til powershell -Command
        //   "cmd"        hele linja sendes til cmd /c
        static void Shell(ExternalTool t, out string exe, out string args)
        {
            string cmd = Runnable(t.Command);

            if (string.Equals(t.Shell, "powershell", StringComparison.OrdinalIgnoreCase))
            {
                exe = "powershell.exe";
                args = "-NoProfile -ExecutionPolicy Bypass -Command \"" +
                       cmd.Replace("\"", "\\\"") + "\"";
                return;
            }
            if (string.Equals(t.Shell, "cmd", StringComparison.OrdinalIgnoreCase))
            {
                exe = "cmd.exe";
                args = "/c " + cmd;
                return;
            }
            Split(cmd, out exe, out args);
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
