# -*- coding: utf-8 -*-
# Språksetter MainForm.cs: pakker brukervendt tekst inn i L.T() / L.F(),
# og legger inn språkvelgeren i sidemenyen.
import io, sys

p = r"C:\Users\Mathias\Desktop\Vaktmester\src\MainForm.cs"
s = io.open(p, encoding="utf-8").read()
n = 0

def rep(old, new, required=True):
    global s, n
    if old not in s:
        if required:
            print("IKKE FUNNET:", old[:70].replace("\n", " "))
            sys.exit(1)
        return
    s = s.replace(old, new, 1)
    n += 1

# --- statuslinje og admin-boks ---
rep('Theme.Lbl("Klar.", Theme.FSmall, Theme.Muted)',
    'Theme.Lbl(L.T("Klar."), Theme.FSmall, Theme.Muted)')
rep('Theme.Lbl("● Kjører som administrator", Theme.FSmall, Theme.Good)',
    'Theme.Lbl("● " + L.T("Kjører som administrator"), Theme.FSmall, Theme.Good)')
rep('Theme.Lbl("● Begrenset modus", Theme.FSmall, Theme.Warn)',
    'Theme.Lbl("● " + L.T("Begrenset modus"), Theme.FSmall, Theme.Warn)')
rep('Theme.Lbl("Systemfiler og drivere krever\\nadministrator.", Theme.FSmall, Theme.Muted)',
    'Theme.Lbl(L.T("Systemfiler og drivere krever\\nadministrator."), Theme.FSmall, Theme.Muted)')
rep('new FlatBtn("Start på nytt som admin")', 'new FlatBtn(L.T("Start på nytt som admin"))')

# --- navigasjon ---
for key, label in [("logg", "Logg"), ("vedlikehold", "Vedlikehold"), ("programmer", "Programvare"),
                   ("drivere", "Oppdateringer"), ("minne", "Minne"), ("oppstart", "Oppstart"),
                   ("diskplass", "Diskplass"), ("rydding", "Rydding"), ("oversikt", "Oversikt")]:
    rep('AddNav(navHost, "%s", "%s");' % (key, label),
        'AddNav(navHost, "%s", L.T("%s"));' % (key, label))

rep('Theme.Lbl("PC-vedlikehold uten tull", Theme.FSmall, Theme.Muted)',
    'Theme.Lbl(L.T("PC-vedlikehold uten tull"), Theme.FSmall, Theme.Muted)')

# --- sidetitler ---
for k in ["Oversikt", "Rydding", "Diskplass", "Oppstart", "Minne",
          "Oppdateringer", "Programvare", "Logg"]:
    rep('return "%s";' % k, 'return L.T("%s");' % k)

for k in ["Tilstanden på maskinen akkurat nå.",
          "Finn og slett filer som bare tar plass.",
          "Hvor det er blitt av lagringsplassen.",
          "Programmer som starter med Windows.",
          "Hva RAM-en faktisk brukes til.",
          "Drivere og Windows-oppdateringer rett fra Microsoft.",
          "Oppdater eller fjern installerte programmer.",
          "Reparasjon, diskhelse og systemsjekk.",
          "Alt programmet har gjort."]:
    rep('return "%s";' % k, 'return L.T("%s");' % k)

# Vedlikehold finnes bade som tittel og i to andre sammenhenger — ta den siste igjen
rep('case "vedlikehold": return "Vedlikehold";', 'case "vedlikehold": return L.T("Vedlikehold");', required=False)

# TitleOf/SubOf kan ikke lenger vaere static naar de bruker L (de kan, L er static) - beholdes

# --- oppdateringsmeldinger ---
rep('Status("Kunne ikke vise oppdateringen: " + ex.Message)',
    'Status(L.T("Kunne ikke vise oppdateringen: ") + ex.Message)')
rep('Status("Ser etter oppdateringer \\u2026");', 'Status(L.T("Ser etter oppdateringer …"));')
rep('Status("Ny versjon " + u.Version + " er tilgjengelig.")',
    'Status(L.F("Ny versjon {0} er tilgjengelig.", u.Version))')
rep('Status("Du har nyeste versjon (" + Updater.CurrentVersion + ").")',
    'Status(L.F("Du har nyeste versjon ({0}).", Updater.CurrentVersion))')

# --- oversiktskort ---
rep('StatCard(out ovRam, out ovRamSub, "Minne i bruk", out ovRamBar)',
    'StatCard(out ovRam, out ovRamSub, L.T("Minne i bruk"), out ovRamBar)')
rep('StatCard(out ovDisk, out ovDiskSub, "Ledig plass på systemdisken", out ovDiskBar)',
    'StatCard(out ovDisk, out ovDiskSub, L.T("Ledig plass på systemdisken"), out ovDiskBar)')
rep('StatCard(out ovStart, out ovStartSub, "Aktive oppstartsprogrammer", null)',
    'StatCard(out ovStart, out ovStartSub, L.T("Aktive oppstartsprogrammer"), null)')
rep('StatCard(out ovJunk, out ovJunkSub, "Søppel funnet", null)',
    'StatCard(out ovJunk, out ovJunkSub, L.T("Søppel funnet"), null)')
rep('new FlatBtn("Kjør full sjekk")', 'new FlatBtn(L.T("Kjør full sjekk"))')
rep('new FlatBtn("Oppdater tall")', 'new FlatBtn(L.T("Oppdater tall"))')

# --- den lange forklaringsteksten: én nøkkel ---
start = s.index('            rt.Text =\n')
end = s.index('            info.Controls.Add(rt);')
gammel = s[start:end]
ny = '            rt.Text = L.T("info.oversikt");\n'
s = s.replace(gammel, ny, 1)
n += 1

# --- tall og status ---
rep('ovRamSub.Text = Util.Bytes(m.UsedPhys) + " av " + Util.Bytes(m.TotalPhys) + " i bruk";',
    'ovRamSub.Text = L.F("{0} av {1} i bruk", Util.Bytes(m.UsedPhys), Util.Bytes(m.TotalPhys));')
rep('''ovDiskSub.Text = "av " + Util.Bytes(sys.TotalSize) + " totalt (" +
                                 (freePct * 100).ToString("0") + " % ledig)";''',
    '''ovDiskSub.Text = L.F("av {0} totalt ({1} % ledig)",
                                 Util.Bytes(sys.TotalSize), (freePct * 100).ToString("0"));''')
rep('ovStartSub.Text = "av " + total + " oppføringer";',
    'ovStartSub.Text = L.F("av {0} oppføringer", total);')
rep('Status("Kunne ikke lese systemtall: " + ex.Message)',
    'Status(L.T("Kunne ikke lese systemtall: ") + ex.Message)')
rep('Status("Skanner etter søppelfiler …");', 'Status(L.T("Skanner etter søppelfiler …"));')
rep('Status("Skanner: " + t.Name);', 'Status(L.T("Skanner: ") + t.Name);')
rep('ovJunkSub.Text = "kan slettes trygt — se Rydding";',
    'ovJunkSub.Text = L.T("kan slettes trygt — se Rydding");')
rep('Status("Full sjekk ferdig. " + Util.Bytes(total) + " kan ryddes bort.");',
    'Status(L.F("Full sjekk ferdig. {0} kan ryddes bort.", Util.Bytes(total)));')
rep('Status("Skanning avbrutt: " + ex.Message)',
    'Status(L.T("Skanning avbrutt: ") + ex.Message)')

# --- logg-siden ---
rep('new FlatBtn("Åpne loggfil")', 'new FlatBtn(L.T("Åpne loggfil"))')
rep('new FlatBtn("Tøm visning")', 'new FlatBtn(L.T("Tøm visning"))')

# --- språkvelger nederst i sidemenyen ---
rep('''            side.Controls.Add(adminBox);''',
'''            side.Controls.Add(adminBox);

            // Språkvelger. Bytte krever omstart av programmet — enklere og
            // sikrere enn å bygge om hele vinduet mens det står åpent.
            Panel langBox = new Panel();
            langBox.Dock = DockStyle.Bottom;
            langBox.Height = 56;
            langBox.BackColor = Theme.Side;
            Label ll = Theme.Lbl(L.T("Språk"), Theme.FSmall, Theme.Muted);
            ll.Location = new Point(16, 6);
            ComboBox cbo = new ComboBox();
            cbo.DropDownStyle = ComboBoxStyle.DropDownList;
            cbo.FlatStyle = FlatStyle.Flat;
            cbo.BackColor = Theme.CardHi;
            cbo.ForeColor = Theme.Text;
            cbo.Location = new Point(16, 24);
            cbo.Width = 186;
            cbo.Items.Add("English");
            cbo.Items.Add("Norsk");
            cbo.SelectedIndex = L.IsNorwegian ? 1 : 0;
            cbo.SelectedIndexChanged += delegate
            {
                string want = cbo.SelectedIndex == 1 ? "no" : "en";
                if (want == L.Lang) return;
                L.Lang = want;
                Util.Log("Språk endret til " + want + ". Starter på nytt.");
                try
                {
                    System.Diagnostics.ProcessStartInfo psi =
                        new System.Diagnostics.ProcessStartInfo(Util.ExePath());
                    psi.Arguments = "/side:" + current;
                    psi.UseShellExecute = true;
                    System.Diagnostics.Process.Start(psi);
                }
                catch { }
                Application.Exit();
            };
            langBox.Controls.Add(ll);
            langBox.Controls.Add(cbo);
            side.Controls.Add(langBox);''')

io.open(p, "w", encoding="utf-8").write(s)
print("MainForm.cs: %d erstatninger" % n)
