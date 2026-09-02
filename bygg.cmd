@echo off
REM ===================================================================
REM  Bygger Brisk.exe og BriskInstaller.exe.
REM  Krever ingenting installert utover Windows selv - C#-kompilatoren
REM  folger med .NET Framework 4.8 som allerede ligger i Windows.
REM ===================================================================
setlocal
cd /d "%~dp0"
set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (echo Fant ikke C#-kompilatoren. & exit /b 1)

echo [1/5] Lager ikon og logo...
"%CSC%" -nologo -target:exe -out:tools\MakeIcon.exe ^
  -r:System.dll -r:System.Drawing.dll ^
  tools\MakeIcon.cs src\Logo.cs || goto :feil
tools\MakeIcon.exe brisk.ico || goto :feil

echo [2/5] Kompilerer Brisk.exe...
"%CSC%" -nologo -target:winexe -out:Brisk.exe -optimize+ -debug:pdbonly ^
  -win32icon:brisk.ico -win32manifest:src\app.manifest ^
  -resource:brisk.ico,brisk.icon ^
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll ^
  -r:System.Management.dll -r:Microsoft.CSharp.dll -r:System.Xml.dll ^
  src\*.cs || goto :feil

REM  Brisk.exe signeres FOER den pakkes inn nedenfor.
REM
REM  Slik det sto foer laa signeringen sist, etter at installasjonsfila
REM  allerede hadde bakt inn Brisk.exe som ressurs. Da ble den innpakkede
REM  kopien den USIGNERTE, mens den frittstaaende fila paa utgivelsen var
REM  signert. Alle som installerte fikk dermed en Brisk.exe uten signatur,
REM  og UAC sa «Unknown publisher» selv om nedlastingen var signert.
REM  Maalt paa 1.7.4: installert 524 800 byte NotSigned, utgivelsens
REM  536 632 byte Valid - differansen er signaturen.
echo [3/5] Signerer Brisk.exe...
powershell -NoProfile -ExecutionPolicy Bypass -File tools\signer.ps1 -Filer Brisk.exe
if errorlevel 1 goto :feil

echo [4/5] Kompilerer BriskInstaller.exe...
"%CSC%" -nologo -target:winexe -out:BriskInstaller.exe -optimize+ -debug:pdbonly ^
  -win32icon:brisk.ico -win32manifest:installer\installer.manifest ^
  -resource:Brisk.exe,Brisk.payload ^
  -resource:Brisk.pdb,Brisk.symbols ^
  -resource:brisk.ico,brisk.icon ^
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll ^
  -r:Microsoft.CSharp.dll ^
  installer\Installer.cs installer\SetupForm.cs installer\AssemblyInfo.cs ^
  src\Theme.cs src\Logo.cs src\Icons.cs src\Util.cs src\Lang.cs || goto :feil

echo [5/5] Signerer BriskInstaller.exe...
powershell -NoProfile -ExecutionPolicy Bypass -File tools\signer.ps1 -Filer BriskInstaller.exe
if errorlevel 1 goto :feil

echo.
echo Ferdig:
for %%f in (Brisk.exe BriskInstaller.exe) do @echo    %%~zf bytes  %%f
goto :slutt

:feil
echo.
echo BYGGET FEILET.
exit /b 1

:slutt
endlocal


