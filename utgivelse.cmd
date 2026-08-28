@echo off
REM  Lager en ny utgivelse.
REM    utgivelse.cmd 1.1.0 https://.../Vaktmester-Installer.exe "Hva som er nytt"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\lag-utgivelse.ps1" -Versjon %1 -NedlastingsUrl %2 -Notat %3
