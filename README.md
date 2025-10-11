🩸 Bloodys Manager








🧠 Overview

BloodysManager is a modular, visual server management tool designed for local or private server environments such as AzerothCore (World of Warcraft).
It allows users to control, monitor, and organize multiple server instances through a modern WPF GUI with real-time performance tracking, logging, and automation support.

🚀 Features
🧩 Core Management

Path configuration for Live, Copy, and Backup servers

Automatic folder structure creation

Custom GitHub download/update path

Server cloning & rotation functions

Backup compression (ZIP/RAR)

Multi-profile support (Server 1, 2, 3, … customizable)

🖥️ Live Monitoring (System Stats)

Real-time CPU, RAM, latency, and server activity tracking

Process-specific CPU% helper class

Graph visualization of server load

🎨 Modern WPF Interface

Color-coded status indicators (green/red via BoolToBrushConverter)

Context menus for folder actions (“Open in Explorer”, “Create Folder”)

Tooltips on all buttons for clear guidance

Log output section for success/error tracking

🧰 Developer Enhancements

Configurable GitHub Repository URL (for self-hosted forks)

Built-in support for multiple servers in one UI

Command panel for launching:

worldserver.exe

authserver.exe

And any other defined executables

⚙️ Installation
git clone https://github.com/bloody2927/BloodysManager.App.git
cd BloodysManager.App
dotnet restore
dotnet build -c Release


The compiled executable will appear under:

/src/BloodysManager.App/bin/Release/net8.0-windows/

🧩 Usage

Launch the BloodysManager.exe

Configure your server paths (Live, Copy, Backup)

Set your GitHub Download/Update Source

Build or update your AzerothCore automatically

Monitor live performance and logs at the bottom panel

🧠 Upcoming Enhancements

Profile-based server presets

Performance graph animations

Integration with remote API monitoring

Automatic crash recovery & relaunch

Localization (EN/DE/FR planned)

🧾 License

This project uses a Custom Restricted License:

🟥 Commercial use, redistribution, or resale is not allowed without explicit permission from the author.
🟩 Personal, educational, and non-commercial use is permitted.

👤 Author

Bloody2927
GitHub: bloody2927

Project: BloodysManager.App
