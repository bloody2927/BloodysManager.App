# BloodysManager

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![WPF](https://img.shields.io/badge/WPF-Windows-blue)
![Build](https://github.com/bloody2927/BloodysManager.App/actions/workflows/build.yml/badge.svg)

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

## Versionstaggung

Versionstaggung lokal:

```
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0
```
