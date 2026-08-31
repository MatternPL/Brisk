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

echo [1/3] Lager ikon og logo...
"%CSC%" -nologo -target:exe -out:tools\MakeIcon.exe ^
  -r:System.dll -r:System.Drawing.dll ^
  tools\MakeIcon.cs src\Logo.cs || goto :feil
tools\MakeIcon.exe brisk.ico || goto :feil

echo [2/3] Kompilerer Brisk.exe...
"%CSC%" -nologo -target:winexe -out:Brisk.exe -optimize+ ^
  -win32icon:brisk.ico -win32manifest:src\app.manifest ^
  -resource:brisk.ico,brisk.icon ^
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll ^
  -r:System.Management.dll -r:Microsoft.CSharp.dll -r:System.Xml.dll ^
  src\*.cs || goto :feil

echo [3/3] Kompilerer BriskInstaller.exe...
"%CSC%" -nologo -target:winexe -out:BriskInstaller.exe -optimize+ ^
  -win32icon:brisk.ico -win32manifest:installer\installer.manifest ^
  -resource:Brisk.exe,Brisk.payload ^
  -resource:brisk.ico,brisk.icon ^
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll ^
  -r:Microsoft.CSharp.dll ^
  installer\Installer.cs installer\SetupForm.cs installer\AssemblyInfo.cs ^
  src\Theme.cs src\Logo.cs src\Icons.cs src\Util.cs src\Lang.cs || goto :feil

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


