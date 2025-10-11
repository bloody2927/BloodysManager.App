# BloodysManager

BloodysManager ist eine WPF-Anwendung auf Basis von .NET 8. Dieses Repository nutzt eine standardisierte Struktur mit einer zentralen Solution-Datei und dem Hauptprojekt im Ordner `src/`.

## Projektstruktur

```
BloodysManager/
├─ BloodysManager.sln
├─ src/
│  └─ BloodysManager.App/
│     └─ BloodysManager.App.csproj
└─ ...
```

## Build

```bash
dotnet restore
dotnet build -c Release
```
