# -*- coding: utf-8 -*-
# Språksetter og korter ned installasjonsvinduet.
import io, sys

P = r"C:\Users\Mathias\Desktop\Vaktmester\installer\SetupForm.cs"
s = io.open(P, encoding="utf-8").read()


def rep(old, new):
    global s
    if old not in s:
        print("IKKE FUNNET: " + old[:90].replace("\n", " "))
        sys.exit(1)
    s = s.replace(old, new, 1)


rep('Text = uninstall ? "Avinstaller Vaktmester" : "Installer Vaktmester";',
    'Text = L.T(uninstall ? "Avinstaller Vaktmester" : "Installer Vaktmester");')
rep('ClientSize = new Size(660, uninstall ? 430 : 528);',
    'ClientSize = new Size(660, uninstall ? 400 : 470);')

rep('''            lblHead = Theme.Lbl(uninstallMode ? "Avinstaller Vaktmester" : "Vaktmester",
                new Font("Segoe UI Light", 22f), Theme.Text);''',
    '''            lblHead = Theme.Lbl(uninstallMode ? L.T("Avinstaller Vaktmester") : "Vaktmester",
                new Font("Segoe UI Light", 22f), Theme.Text);''')
rep('''            lblSub = Theme.Lbl(uninstallMode
                    ? "Fjerner programmet og snarveiene fra denne maskinen."
                    : "PC-vedlikehold uten tull  ·  versjon " + Setup.Version,
                Theme.F, Theme.Muted);''',
    '''            lblSub = Theme.Lbl(uninstallMode
                    ? L.T("Fjerner programmet og snarveiene fra denne maskinen.")
                    : "v" + Setup.Version,
                Theme.F, Theme.Muted);''')

rep('primary = new FlatBtn(uninstallMode ? "Avinstaller" : "Installer");',
    'primary = new FlatBtn(L.T(uninstallMode ? "Avinstaller" : "Installer"));')
rep('secondary = new FlatBtn("Avbryt");', 'secondary = new FlatBtn(L.T("Avbryt"));')

rep('''            Label what = new Label();
            what.Dock = DockStyle.Top;
            what.Height = 118;
            what.ForeColor = Theme.Muted;
            what.Text =
                "Vaktmester rydder søppelfiler, viser hva som starter med Windows, henter drivere " +
                "og Windows-oppdateringer fra Microsoft, og finner hvor lagringsplassen har blitt av.\\r\\n\\r\\n" +
                "Ingen betalingsmur, ingen abonnement, ingen datainnsamling. Programmet snakker bare " +
                "med Windows Update og winget — begge fra Microsoft.";

            Label where = new Label();
            where.Dock = DockStyle.Top;
            where.Height = 46;
            where.ForeColor = Theme.Muted;
            where.Text = "Installeres i:\\r\\n" + Setup.InstallDir;

            Label admin = new Label();
            admin.Dock = DockStyle.Top;
            admin.Height = 40;
            admin.ForeColor = Theme.Good;
            admin.Text = "Installasjonen trenger ikke administrator. Selve programmet ber om det " +
                         "først når du gjør noe som krever det.";

            chkDesktop = Chk("Lag snarvei på skrivebordet", true);
            chkLaunch = Chk("Start Vaktmester når installasjonen er ferdig", true);''',
    '''            Label what = new Label();
            what.Dock = DockStyle.Top;
            what.Height = 74;
            what.ForeColor = Theme.Muted;
            what.Text =
                L.T("Rydder søppelfiler, viser hva som starter med Windows, henter drivere og Windows-oppdateringer fra Microsoft, og finner hvor lagringsplassen har blitt av.") +
                "\\r\\n\\r\\n" + L.T("Ingen betalingsmur, ingen abonnement, ingen datainnsamling.");

            Label where = new Label();
            where.Dock = DockStyle.Top;
            where.Height = 44;
            where.ForeColor = Theme.Muted;
            where.Text = L.T("Installeres i:") + "\\r\\n" + Setup.InstallDir;

            Label admin = new Label();
            admin.Dock = DockStyle.Top;
            admin.Height = 26;
            admin.ForeColor = Theme.Good;
            admin.Text = L.T("Trenger ikke administrator.");

            chkDesktop = Chk(L.T("Lag snarvei på skrivebordet"), true);
            chkLaunch = Chk(L.T("Start etter installasjon"), true);''')

rep('''                lblSub.Text = "Allerede installert — dette oppdaterer den til versjon " + Setup.Version + ".";
                primary.Text = "Oppdater";''',
    '''                lblSub.Text = L.T("Allerede installert.");
                primary.Text = L.F("Oppdater til {0}", Setup.Version);''')

rep('''            Label what = new Label();
            what.Dock = DockStyle.Top;
            what.Height = 150;
            what.ForeColor = Theme.Muted;
            what.Text =
                "Dette fjerner:\\r\\n" +
                "   ·  programfilene i " + Setup.InstallDir + "\\r\\n" +
                "   ·  snarveiene i Start-menyen og på skrivebordet\\r\\n" +
                "   ·  oppføringen i «Apper og funksjoner»\\r\\n\\r\\n" +
                "Endringer du har gjort i oppstartsprogrammer beholdes — de ligger i Windows, " +
                "ikke i Vaktmester. Loggen beholdes også.";
            body.Controls.Add(what);''',
    '''            Label what = new Label();
            what.Dock = DockStyle.Top;
            what.Height = 110;
            what.ForeColor = Theme.Muted;
            what.Text = L.T("Dette fjerner programfilene, snarveiene og oppføringen i «Apper og funksjoner». Endringer du har gjort i oppstartsprogrammer beholdes.") +
                        "\\r\\n\\r\\n" + Setup.InstallDir;
            body.Controls.Add(what);''')

rep('lblHead.Text = uninstallMode ? "Avinstallerer …" : "Installerer …";',
    'lblHead.Text = L.T(uninstallMode ? "Avinstallerer …" : "Installerer …");')
rep('''                lblHead.Text = "Det gikk galt";''', '''                lblHead.Text = L.T("Det gikk galt");''')
rep('primary.Text = "Lukk";\n                if (chkLaunch != null) chkLaunch.Checked = false;',
    'primary.Text = L.T("Lukk");\n                if (chkLaunch != null) chkLaunch.Checked = false;')
rep('''                lblHead.Text = "Vaktmester er fjernet";
                lblSub.Text = "Takk for lånet.";
                primary.Text = "Lukk";''',
    '''                lblHead.Text = L.T("Vaktmester er fjernet");
                lblSub.Text = "";
                primary.Text = L.T("Lukk");''')
rep('''                lblHead.Text = "Ferdig installert";
                lblSub.Text = "Du finner Vaktmester i Start-menyen.";
                primary.Text = (chkLaunch != null && chkLaunch.Checked) ? "Start Vaktmester" : "Lukk";''',
    '''                lblHead.Text = L.T("Ferdig installert");
                lblSub.Text = L.T("Du finner Vaktmester i Start-menyen.");
                primary.Text = (chkLaunch != null && chkLaunch.Checked)
                    ? L.T("Start Vaktmester") : L.T("Lukk");''')

io.open(P, "w", encoding="utf-8").write(s)
print("SetupForm.cs OK")
