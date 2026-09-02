using System.Reflection;
using System.Runtime.InteropServices;

// Installatoren ble bygget uten disse. Da staar det 0.0.0.0 som versjon og
// tomme felt for utgiver, produkt og beskrivelse i fila - og en usignert
// installatoer uten metadata som pakker ut et program og skriver i registeret
// er nettopp formen antivirus sine ML-modeller reagerer paa. Metadata alene
// fjerner ikke et falskt utslag, men fravaeret av den er et av signalene.

[assembly: AssemblyTitle("Brisk Installer")]
[assembly: AssemblyDescription("Installs Brisk, a free Windows maintenance tool.")]
[assembly: AssemblyProduct("Brisk")]
[assembly: AssemblyCompany("Mathias Arne Andresen")]
[assembly: AssemblyCopyright("© 2026 Mathias Arne Andresen")]
[assembly: AssemblyVersion("1.7.1.0")]
[assembly: AssemblyFileVersion("1.7.1.0")]
[assembly: ComVisible(false)]
