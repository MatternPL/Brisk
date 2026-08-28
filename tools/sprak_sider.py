# -*- coding: utf-8 -*-
# Språksetter Pages2, Pages3, UpdateDialog og installeren, og kutter prosaen.
import io, sys

ROT = r"C:\Users\Mathias\Desktop\Vaktmester"


def load(f):
    return io.open(ROT + "\\" + f, encoding="utf-8").read()


def save(f, s):
    io.open(ROT + "\\" + f, "w", encoding="utf-8").write(s)


def rep(s, old, new, required=True):
    if old not in s:
        if required:
            print("IKKE FUNNET i " + rep.f + ": " + old[:90].replace("\n", " "))
            sys.exit(1)
        return s
    return s.replace(old, new, 1)


# ============ MainForm.Pages2.cs ============
rep.f = "src\\MainForm.Pages2.cs"
s = load(rep.f)

s = rep(s, 'FlatBtn bScan = new FlatBtn("Analyser plass"); bScan.Primary(); bScan.Width = 150;',
        'FlatBtn bScan = new FlatBtn(L.T("Analyser plass")); bScan.Primary(); bScan.Width = 150;')
s = rep(s, 'FlatBtn bStop = new FlatBtn("Stopp"); bStop.Width = 90; bStop.Enabled = false;',
        'FlatBtn bStop = new FlatBtn(L.T("Stopp")); bStop.Width = 90; bStop.Enabled = false;')
s = rep(s, 'FlatBtn bOpen = new FlatBtn("Åpne i Utforsker"); bOpen.Width = 165;',
        'FlatBtn bOpen = new FlatBtn(L.T("Åpne i Utforsker")); bOpen.Width = 165;')

# Notatfeltet under lista erstattes av et hint pa knappen
s = rep(s, '''            Panel note = new Panel();
            note.Dock = DockStyle.Bottom;
            note.Height = 34;
            note.BackColor = Theme.Bg;
            Label nl = Theme.Lbl(
                "Ingenting slettes her — dette er bare en oversikt. Dobbeltklikk en rad for å åpne stedet i Utforsker.",
                Theme.FSmall, Theme.Muted);
            nl.AutoSize = false; nl.Dock = DockStyle.Fill;
            note.Controls.Add(nl);
''', '''            Tip(bScan, "Leser gjennom hele treet. Sletter ingenting.");
            Tip(bOpen, "Dobbeltklikk en rad gjør det samme.");
''')
s = rep(s, '''            p.Controls.Add(split);
            p.Controls.Add(note);
            p.Controls.Add(bar);''',
        '''            p.Controls.Add(split);
            p.Controls.Add(bar);''')

s = rep(s, 'Label h1 = Theme.Lbl("Største mapper", Theme.FBold, Theme.Text);',
        'Label h1 = Theme.Lbl(L.T("Største mapper"), Theme.FBold, Theme.Text);')
s = rep(s, 'lvFolders = ListIn(split.Panel1, false, "Mappe", "600", "Størrelse", "120", "Filer", "90");',
        'lvFolders = ListIn(split.Panel1, false, L.T("Mappe"), "600", L.T("Størrelse"), "120", L.T("Filer"), "90");')
s = rep(s, 'Label h2 = Theme.Lbl("Største enkeltfiler (over 100 MB)", Theme.FBold, Theme.Text);',
        'Label h2 = Theme.Lbl(L.T("Største filer (over 100 MB)"), Theme.FBold, Theme.Text);')
s = rep(s, 'lvFiles = ListIn(split.Panel2, false, "Fil", "600", "Størrelse", "120", "Mappe", "400");',
        'lvFiles = ListIn(split.Panel2, false, L.T("Fil"), "600", L.T("Størrelse"), "120", L.T("Mappe"), "400");')
s = rep(s, 'if (src == null) { Status("Velg en rad først."); return; }',
        'if (src == null) { Status(L.T("Velg en rad først.")); return; }')
s = rep(s, 'catch (Exception ex) { Status("Kunne ikke åpne: " + ex.Message); }',
        'catch (Exception ex) { Status(L.T("Kunne ikke åpne: ") + ex.Message); }')
s = rep(s, 'Status("Går gjennom " + root + " … dette kan ta et par minutter.");',
        'Status(L.F("Går gjennom {0} …", root));')
s = rep(s, 'DiskTools.Scan(root, ct, delegate(string d) { Status("Leser: " + d); }, out fo, out fi);',
        'DiskTools.Scan(root, ct, delegate(string d) { Status(d); }, out fo, out fi);')
s = rep(s, 'if (fo == null) { Status("Analysen ble avbrutt."); return; }',
        'if (fo == null) { Status(L.T("Avbrutt.")); return; }')
s = rep(s, '''                lblDiskSum.Text = fo.Count + " mapper, " + fi.Count + " store filer";''',
        '''                lblDiskSum.Text = L.F("{0} mapper, {1} store filer", fo.Count, fi.Count);''')
s = rep(s, '''                Status("Ferdig på " + (int)(DateTime.Now - t0).TotalSeconds + " s. Største post: " +
                       Util.Bytes(biggest) + ".");''',
        '''                Status(L.F("Ferdig på {0} s. Største post: {1}.",
                       (int)(DateTime.Now - t0).TotalSeconds, Util.Bytes(biggest)));''')
s = rep(s, 'Status("Avbryter …");', 'Status(L.T("Avbryter …"));')

# --- Programvare ---
s = rep(s, 'FlatBtn bChk = new FlatBtn("Se etter oppdateringer"); bChk.Primary(); bChk.Width = 200;',
        'FlatBtn bChk = new FlatBtn(L.T("Se etter oppdateringer")); bChk.Primary(); bChk.Width = 200;')
s = rep(s, 'FlatBtn bUp = new FlatBtn("Oppdater merkede"); bUp.Width = 165; bUp.Enabled = false;',
        'FlatBtn bUp = new FlatBtn(L.T("Oppdater merkede")); bUp.Width = 165; bUp.Enabled = false;')
s = rep(s, 'FlatBtn bAll = new FlatBtn("Merk alle"); bAll.Width = 110;',
        'FlatBtn bAll = new FlatBtn(L.T("Merk alle")); bAll.Width = 110;')
s = rep(s, 'Label h1 = Theme.Lbl("Tilgjengelige programoppdateringer (winget)", Theme.FBold, Theme.Text);',
        'Label h1 = Theme.Lbl(L.T("Programoppdateringer (winget)"), Theme.FBold, Theme.Text);')
s = rep(s, '''            lvApps = ListIn(split.Panel1, true,
                "Program", "290", "Installert", "130", "Ny versjon", "130", "Pakke-ID", "320");''',
        '''            lvApps = ListIn(split.Panel1, true,
                L.T("Program"), "290", L.T("Installert"), "130", L.T("Ny versjon"), "130", L.T("Pakke-ID"), "320");''')
s = rep(s, 'Label h2 = Theme.Lbl("Installerte programmer — sortert etter størrelse", Theme.FBold, Theme.Text);',
        'Label h2 = Theme.Lbl(L.T("Installerte programmer"), Theme.FBold, Theme.Text);')
s = rep(s, 'FlatBtn bUn = new FlatBtn("Avinstaller valgt"); bUn.Danger();',
        'FlatBtn bUn = new FlatBtn(L.T("Avinstaller")); bUn.Danger();')
s = rep(s, 'FlatBtn bRefI = new FlatBtn("Oppdater liste");', 'FlatBtn bRefI = new FlatBtn(L.T("Oppdater"));')
s = rep(s, '''            lvInstalled = ListIn(split.Panel2, false,
                "Program", "330", "Størrelse", "110", "Versjon", "140", "Utgiver", "220", "Installert", "110");''',
        '''            lvInstalled = ListIn(split.Panel2, false,
                L.T("Program"), "330", L.T("Størrelse"), "110", L.T("Versjon"), "140",
                L.T("Utgiver"), "220", L.T("Installert"), "110");''')
s = rep(s, '''                    Status("winget mangler. Installer «App Installer» fra Microsoft Store, så virker denne.");
                    Append(appOut, "winget ble ikke funnet på maskinen.");''',
        '''                    Status(L.T("winget mangler. Installer «App Installer» fra Microsoft Store."));''')
s = rep(s, 'Status("Spør winget om oppdateringer …");', 'Status(L.T("Spør winget …"));')
s = rep(s, '''                Status(lvApps.Items.Count > 0
                    ? lvApps.Items.Count + " program(mer) kan oppdateres."
                    : (note.Length > 0 ? note : "Alt er oppdatert."));''',
        '''                Status(lvApps.Items.Count > 0
                    ? L.F("{0} kan oppdateres.", lvApps.Items.Count)
                    : (note.Length > 0 ? note : L.T("Alt er oppdatert.")));''')
s = rep(s, 'if (chosen.Count == 0) { Status("Ingen programmer er merket."); return; }',
        'if (chosen.Count == 0) { Status(L.T("Ingenting er merket.")); return; }')
s = rep(s, 'Status("Oppdaterer " + a.Name + " …");', 'Status(a.Name);')
s = rep(s, 'Status("Oppdaterte " + ok + " av " + chosen.Count + " program(mer).");',
        'Status(L.F("Oppdaterte {0} av {1}.", ok, chosen.Count));')
s = rep(s, 'if (lvInstalled.SelectedItems.Count == 0) { Status("Velg et program i den nedre lista."); return; }',
        'if (lvInstalled.SelectedItems.Count == 0) { Status(L.T("Velg et program i den nedre lista.")); return; }')
s = rep(s, '''                if (MessageBox.Show(this,
                        "Avinstaller «" + a.Name + "»?\\n\\n" +
                        "Programmets egen avinstallering starter. Følg eventuelle spørsmål der.",
                        "Bekreft avinstallering", MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) != DialogResult.Yes) return;''',
        '''                if (MessageBox.Show(this,
                        L.F("Avinstaller «{0}»? Programmets egen avinstallering starter.", a.Name),
                        L.T("Avinstaller"), MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) != DialogResult.Yes) return;''')
s = rep(s, '''                if (AppInventory.StartUninstall(a))
                    Status("Avinstallering startet for " + a.Name + ". Oppdater lista når den er ferdig.");
                else Status("Fant ingen avinstalleringskommando for " + a.Name + ".");''',
        '''                if (AppInventory.StartUninstall(a)) Status(L.F("Startet avinstallering av {0}.", a.Name));
                else Status(L.F("Fant ingen avinstalleringskommando for {0}.", a.Name));''')
s = rep(s, '''                lblInstalledSum.Text = apps.Count + " programmer · ca. " + Util.Bytes(sum);''',
        '''                lblInstalledSum.Text = L.F("{0} programmer · {1}", apps.Count, Util.Bytes(sum));''')
s = rep(s, 'catch (Exception ex) { Status("Kunne ikke lese programlista: " + ex.Message); }',
        'catch (Exception ex) { Status(L.T("Kunne ikke lese programlista: ") + ex.Message); }')
save(rep.f, s)
print("MainForm.Pages2.cs OK")

# ============ MainForm.Pages3.cs ============
rep.f = "src\\MainForm.Pages3.cs"
s = load(rep.f)
s = rep(s, 'FlatBtn bSearch = new FlatBtn("Søk hos Windows Update"); bSearch.Primary(); bSearch.Width = 210;',
        'FlatBtn bSearch = new FlatBtn(L.T("Søk")); bSearch.Primary(); bSearch.Width = 120;')
s = rep(s, 'FlatBtn bInst = new FlatBtn("Installer merkede"); bInst.Width = 160; bInst.Enabled = false;',
        'FlatBtn bInst = new FlatBtn(L.T("Installer merkede")); bInst.Width = 160; bInst.Enabled = false;')
s = rep(s, 'FlatBtn bDev = new FlatBtn("Enhetsbehandling"); bDev.Width = 155;',
        'FlatBtn bDev = new FlatBtn(L.T("Enhetsbehandling")); bDev.Width = 155;')
s = rep(s, 'FlatBtn bWu = new FlatBtn("Windows Update"); bWu.Width = 155;',
        'FlatBtn bWu = new FlatBtn("Windows Update"); bWu.Width = 155;\n            Tip(bSearch, "Spør Windows Update om drivere og systemoppdateringer. Tar gjerne et minutt.");')
s = rep(s, 'Label dl = Theme.Lbl("Enheter Windows melder problem på", Theme.FBold, Theme.Text);',
        'Label dl = Theme.Lbl(L.T("Enheter med problem"), Theme.FBold, Theme.Text);')
s = rep(s, 'lvDev = ListIn(devHost, false, "Enhet", "380", "Problem", "330", "Enhets-ID", "420");',
        'lvDev = ListIn(devHost, false, L.T("Enhet"), "380", L.T("Problem"), "330", L.T("Enhets-ID"), "420");')
s = rep(s, '''            Label dl2 = Theme.Lbl("Tilgjengelig fra Microsoft — drivere og Windows-oppdateringer",
                Theme.FBold, Theme.Text);''',
        '''            Label dl2 = Theme.Lbl(L.T("Tilgjengelig fra Microsoft"), Theme.FBold, Theme.Text);''')
s = rep(s, '''            lvUpd = ListIn(updHost, true,
                "Type", "95", "Oppdatering", "520", "Detaljer", "230", "Størrelse", "105");''',
        '''            lvUpd = ListIn(updHost, true,
                L.T("Type"), "95", L.T("Oppdatering"), "520", L.T("Detaljer"), "230", L.T("Størrelse"), "105");''')
s = rep(s, 'Status("Leser enhetsliste …");', 'Status(L.T("Leser enhetsliste …"));')
s = rep(s, 'Status("Spør Windows Update om drivere — kan ta et minutt …");',
        'Status(L.T("Spør Windows Update om drivere …"));')
s = rep(s, 'Status("Spør Windows Update om systemoppdateringer …");',
        'Status(L.T("Spør Windows Update om systemoppdateringer …"));')
s = rep(s, 'ListViewItem li = new ListViewItem("Ingen enheter med problemer.");',
        'ListViewItem li = new ListViewItem(L.T("Ingen enheter med problemer."));')
s = rep(s, 'ListViewItem li = new ListViewItem("Windows");', 'ListViewItem li = new ListViewItem(L.T("Windows"));')
s = rep(s, 'li.SubItems.Add(u.Severity.Length > 0 ? "Alvorlighet: " + u.Severity : "");',
        'li.SubItems.Add(u.Severity.Length > 0 ? L.F("Alvorlighet: {0}", u.Severity) : "");')
s = rep(s, 'ListViewItem li = new ListViewItem("Driver");', 'ListViewItem li = new ListViewItem(L.T("Driver"));')
s = rep(s, '''                if (lvUpd.Items.Count > 0)
                    Status(nw + " Windows-oppdatering(er) og " + nd + " driver(e) tilgjengelig.");
                else
                    Status("Alt er oppdatert. " + wnote + " " + dnote);''',
        '''                if (lvUpd.Items.Count > 0)
                    Status(L.F("{0} Windows-oppdateringer og {1} drivere.", nw, nd));
                else
                    Status(L.T("Alt er oppdatert."));''')
s = rep(s, 'if (n == 0) { Status("Ingenting er merket."); return; }',
        'if (n == 0) { Status(L.T("Ingenting er merket.")); return; }')
s = rep(s, '''                if (MessageBox.Show(this,
                        "Installerer " + n + " oppdatering(er) direkte fra Microsoft.\\n\\n" +
                        "Skjermen kan blinke under driverinstallasjon, og noe krever omstart.\\n\\n" +
                        "Fortsette?", "Bekreft installasjon",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;''',
        '''                if (MessageBox.Show(this,
                        L.F("Installerer {0} fra Microsoft. Skjermen kan blinke, og noe krever omstart.", n) +
                        "\\n\\n" + L.T("Fortsette?"), L.T("Oppdateringer"),
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;''')
s = rep(s, 'Status("Installerte " + done + " av " + n + "." + (reboot ? "  Omstart kreves." : ""));',
        'Status(L.F("Installerte {0} av {1}.", done, n) + (reboot ? "  " + L.T("Omstart kreves.") : ""));')
s = rep(s, '''                    MessageBox.Show(this, "Noe av dette krever omstart for å bli aktivt.",
                        "Omstart nødvendig", MessageBoxButtons.OK, MessageBoxIcon.Information);''',
        '''                    MessageBox.Show(this, L.T("Noe av dette krever omstart for å bli aktivt."),
                        L.T("Omstart"), MessageBoxButtons.OK, MessageBoxIcon.Information);''')
s = rep(s, '''                Defer(delegate { Status("Uten administrator kan oppdateringer søkes opp, men ikke installeres."); });''',
        '''                Defer(delegate { Status(L.T("Uten administrator kan du søke, men ikke installere.")); });''')
save(rep.f, s)
print("MainForm.Pages3.cs OK")

# ============ UpdateDialog.cs ============
rep.f = "src\\UpdateDialog.cs"
s = load(rep.f)
s = rep(s, 'Text = "Ny versjon tilgjengelig";', 'Text = L.T("Ny versjon tilgjengelig");')
s = rep(s, '''            Label h = Theme.Lbl("Vaktmester " + u.Version + " er klar",
                new Font("Segoe UI Light", 17f), Theme.Text);''',
        '''            Label h = Theme.Lbl(L.F("Vaktmester {0}", u.Version),
                new Font("Segoe UI Light", 17f), Theme.Text);''')
s = rep(s, '''            Label sub = Theme.Lbl("Du har " + Updater.CurrentVersion +
                (u.Size > 0 ? "   ·   nedlasting " + Util.Bytes(u.Size) : ""),
                Theme.FSmall, Theme.Muted);''',
        '''            Label sub = Theme.Lbl(L.F("Du har {0}", Updater.CurrentVersion) +
                (u.Size > 0 ? "   ·   " + Util.Bytes(u.Size) : ""),
                Theme.FSmall, Theme.Muted);''')
s = rep(s, 'bYes = new FlatBtn("Oppdater nå");', 'bYes = new FlatBtn(L.T("Oppdater nå"));')
s = rep(s, 'bNo = new FlatBtn("Ikke nå");', 'bNo = new FlatBtn(L.T("Ikke nå"));')
s = rep(s, '''            notes.Text = string.IsNullOrEmpty(u.Notes)
                ? "Ingen endringsbeskrivelse fulgte med denne versjonen."
                : u.Notes;''',
        '''            notes.Text = string.IsNullOrEmpty(u.Notes) ? L.T("Ingen endringsbeskrivelse.") : u.Notes;''')
s = rep(s, 'lblState.Text = "Laster ned …";', 'lblState.Text = L.T("Laster ned …");')
s = rep(s, 'lblState.Text = error ?? "Nedlastingen feilet.";', 'lblState.Text = error ?? L.T("Nedlastingen feilet.");')
s = rep(s, 'bNo.Text = "Lukk";', 'bNo.Text = L.T("Lukk");')
s = rep(s, 'lblState.Text = "Sjekksum bekreftet. Starter installasjonen …";',
        'lblState.Text = L.T("Sjekksum bekreftet. Starter installasjonen …");')
s = rep(s, '''                lblState.Text = "Laster ned … " + Util.Bytes(got) + " av " + Util.Bytes(total);''',
        '''                lblState.Text = L.F("Laster ned … {0} av {1}", Util.Bytes(got), Util.Bytes(total));''')
s = rep(s, '''                lblState.Text = "Laster ned … " + Util.Bytes(got);''',
        '''                lblState.Text = L.T("Laster ned …") + " " + Util.Bytes(got);''')
save(rep.f, s)
print("UpdateDialog.cs OK")

print("Ferdig.")
