# Sonar EQ Changer 🎧

![Sonar EQ Changer](icon.png)

**Sonar EQ Changer** is a lightweight, background Windows application built with WPF and .NET 10. It automatically detects the active game or application you are running and instantly switches your **SteelSeries Sonar** "Game" channel to your desired EQ preset. 

No more manually changing EQ profiles when switching from a tactical shooter to a story-driven RPG!

## ✨ Features
* **Auto EQ Switching:** Detects the foreground application and seamlessly switches the Sonar EQ preset using the local SteelSeries GG web server API.
* **Game Scanner:** Automatically scans running processes to help you quickly add your games to the library.
* **Manual Game Addition:** If a game isn't detected automatically, you can manually select its executable.
* **System Tray Integration:** Runs silently in the background. Minimizes to the system tray and uses minimal system resources.
* **Bilingual Support:** Supports both **English** and **Turkish** user interfaces.
* **Start with Windows:** Can be configured to automatically start with your PC.

## 🚀 How It Works
Sonar EQ Changer connects to the local SteelSeries GG background server (`http://localhost:<dynamic_port>`) by reading the coreProps.json file. It retrieves your available Sonar presets and listens to your active windows. When a mapped game is brought to the foreground, it sends an API request to change the EQ preset instantly.

## 📦 Installation
1. Ensure you have **SteelSeries GG** installed and running on your system, with Sonar enabled.
2. Download the latest release from the [Releases](#) page.
3. Extract the contents and run `SonarEQChanger.exe`.

*Alternatively, you can build it from source:*
```bash
git clone https://github.com/yourusername/Sonar-EQ-Changer.git
cd Sonar-EQ-Changer
dotnet build -c release
```

## 🛠️ Usage
1. Open the application from the system tray.
2. On the **Profiles (Profiller)** page, choose a default EQ preset. This preset will be applied when no mapped game is running.
3. Click "Scan Running Games (Çalışan Oyunları Tara)" to discover active games, or manually add an `.exe` file.
4. Map your games to your preferred Sonar EQ presets.
5. Minimize the app and enjoy! It will automatically switch presets as you alt-tab or launch games.

## 💻 Technologies Used
* **C# / .NET 10.0**
* **WPF (Windows Presentation Foundation)**
* **SteelSeries GG Local API**

## 🤝 Contributing
Contributions, issues, and feature requests are welcome! Feel free to check the [issues page](#).

## 📝 License
This project is licensed under the MIT License - see the LICENSE file for details.

---

*Disclaimer: This project is not affiliated with, endorsed, or sponsored by SteelSeries. "SteelSeries" and "Sonar" are trademarks of SteelSeries ApS.*
