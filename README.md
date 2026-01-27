<p align="center">
  <img src="https://images2.imgbox.com/d8/c0/58Scr2hu_o.png" alt="EssentialsX Banner" width="100%" />
</p>

<p align="center">
  <!-- Latest Release (Green) -->
  <a href="https://github.com/Yanoee/EssentialsX/releases/tag/v1.0.0">
    <img src="https://img.shields.io/github/v/tag/Yanoee/EssentialsX?style=flat&logo=github&label=Release&color=2ea44f" alt="Release" />
  </a>
  <!-- Version -->
  <a href="https://github.com/Yanoee/EssentialsX/releases">
    <img src="https://img.shields.io/github/v/tag/Yanoee/EssentialsX?label=Version&style=flat&color=blueviolet" alt="Version" />
  </a>
  <!-- Issues -->
  <a href="https://github.com/Yanoee/EssentialsX/issues">
    <img src="https://img.shields.io/github/issues/Yanoee/EssentialsX?style=flat&logo=github&color=yellow" alt="Issues" />
  </a>
  <!-- Wiki (White) -->
  <a href="https://github.com/Yanoee/EssentialsX/wiki">
    <img src="https://img.shields.io/badge/Wiki-Docs-white?style=flat&logo=wikipedia&logoColor=black" alt="Wiki" />
  </a>
  <!-- ModDB (Blue) -->
  <a href="https://mods.vintagestory.at/essentialsx">
    <img src="https://img.shields.io/badge/ModDB-Download-0078D6?style=flat&logo=moddb&logoColor=white" alt="ModDB" />
  </a>
</p>

# EssentialsX for Vintage Story
EssentialsX is a modular **server utility pack** made for Vintage Story.<br>It boosts the server experience by addressing the absence of built-in server-side plugins.<br>
With features like homes, rules, TPA, TPR, Rcon integration, and webAPI support, everything is easily adjustable through straightforward `.json` files.<br>

A key highlight of this mod is its RichText support, which allows you to incorporate colors and formatting into chat messages, giving your server chat a lively and professional appearance.<br>

My design goal is straightforward: **server-friendly, admin-friendly, player-friendly**.<br>EssentialsX is also **new-user friendly**; 99% of the mod can be set up effortlessly by tweaking human-readable JSON files.<br>

In many ways, EssentialsX acts as a full **server plugin suite** for Vintage Story.  
It offers admins modern tools for **server management and automation**, which include:

- **Homes** — players can set, delete, list, and teleport to their personal home points with limits based on roles, warmups, and cooldowns  
- **Rules** — server rules are shown in a paged format that supports RichText, fully editable through JSON  
- **Teleportation (TPA/TPR)** — player teleportation based on requests with options to accept/deny/cancel, clickable chat features, warmups, cooldowns, and expiry timers  
- **Back** — sends players back to their last location or death point after they teleport or respawn  
- **Spawn** — teleports players to the world spawn point with safety checks and adjustable timers  
- **Random Teleport (RTP)** — teleports players to safe, random locations in the world with options for range and biome blacklists  
- **Join/Leave messages** — customizable messages formatted in RichText to greet players and announce when they leave  
- **TabList enhancements** — an upgraded in-game player list featuring player counts, ping icons, and custom headers/footers  
- **Kits** (planned) — ready-made item bundles for new players or server rewards  
- **Moderation tools** (planned) — adjustable features to assist staff in maintaining fairness and safety on servers  
- **RCON + WebAPI** (planned) — support for remote console and REST API for websites/dashboards, providing live data like player count, slots, ping, uptime, world info, and more


Whether you’re running a small private server or a large community, EssentialsX makes it easy to **manage, moderate, and expand your server**.
## 📦 Installation
1. Download the latest release `EssentialsX.zip` from [Releases](https://github.com/Yanoee/EssentialsX/releases).  
OR download from [VintageStory ModDB](https://mods.vintagestory.at/essentialsx)

2. Place it into your server’s mods folder:
   - **Windows:** `%AppData%\VintagestoryData\Mods`
   - **Linux (client default):** `~/.config\VintagestoryData/Mods`
   - **Linux (your server setup):** `/SERVERFILES/Mods <- drop here`
3. Start the server.  
   → All config and message `.json` files will auto-generate under:
VintagestoryData/ModConfig/EssentialsX/

## 📜 License
This project is licensed under the MIT License.
You may use, modify, and distribute this software with proper attribution.

**Author:** Yanoee  

**Contributors:** Y3GeZ, MeoWara

---
  
### 🗂 JSON Config System

Every module has:
- **Settings.json** → timers, toggles, role permissions
- **Messages.json** → all player-facing text
Everything is human-readable and safe to edit.
All configs are **auto-generated** if missing.

Example `RulesMessages.json`:
```json
{
  "Header": "<font color='#FF0000'>[</font><font color='#5BD9D9'>Rules</font>]",
  "Footer": " ",
  "PageLabel": "Page {page}/{total}",
  "Lines": [
    "Be respectful to other players.",
    "No cheating, exploiting, or griefing."
  ]
}
```

---

## 🛠 Modules

### 🏠 Home - Home module with storage database.
### 📜 Rules - Rules module fully editable.
### 🔄 Teleportation - Standard teleportation module.
### ⚡ Back - Death location checkpoint.
### 🏠 Spawn - Automatic teleportation for the spawn command.
### 🌍 Random Teleport (RTP) - Randomly teleports you somewhere in the world. 
### 👥 Join/Leave - Rich join & leave text module.
### 📋 TabList - TabList including header&footer and supports server advertisment.
### 🌐 RCON & WebAPI (Planned)
EssentialsX is being built with **future server management in mind**.  
A dedicated **RCON (Remote Console)** and **WebAPI** system is planned, enabling seamless integration with websites, dashboards, and external tools.

**RCON (Remote Console):**
- Secure remote access to the in-game server console
- Run commands and view output directly from a web interface
- Ideal for admins who want to manage servers without SSH/RDP access
- Supports multiple admin logins with permissions

**WebAPI:**
- REST-style endpoints to expose live server data
- Example features:
  - Player count  
  - Online player names & roles  
  - Server title & description (from `serverconfig.json`)  
  - World time & uptime  
  - Ping & slot information  
- Designed for server list sites, dashboards, or custom web apps
- JSON responses for easy integration with any language/framework
**Goal:**  
Bring Vintage Story closer to “modern” server management seen in other games (like Minecraft Spigot/Bukkit or Rust Oxide), where websites can interact with the server easily and securely.
---
<p align="center">
<img src="https://images2.imgbox.com/82/c0/aObb9Xxy_o.png" alt="Button" width="256" height="256">
</p>
