# ForgeServer

## Description

**ForgeServer** is a user-friendly desktop application designed to simplify the process of launching and managing Minecraft Forge servers, specifically for the version **1.16.5-36.2.42**. This tool provides an intuitive interface to customize server settings, manage worlds, and optimize server performance, offering a seamless experience for both novice and advanced users.

## Key Features

- **Customizable Memory Settings**:
  - Easily specify the maximum (`Xmx`) and minimum (`Xms`) memory allocation for the Java process, optimizing performance based on your hardware.

- **Server Configuration Panel**:
  - A dedicated interface for editing `server.properties`, adding/removing mods, managing worlds, and configuring server behavior.

- **PlayIt.gg Support**:
  - Integrated support for **PlayIt.gg**, enabling simple tunneling and sharing of your server with friends. 
  - Debug mode allows monitoring of PlayIt.gg's real-time output in a dedicated window.

- **Real-Time Server Monitoring**:
  - Captures and displays server output and error messages in the console for easy troubleshooting.

- **World and Mod Management**:
  - Intuitive interfaces for adding, renaming, and managing server worlds and mods directly from the app.

- **Error Handling**:
  - Comprehensive feedback and error reporting ensure users can quickly resolve issues during setup or runtime.

- **Cross-Process Management**:
  - Ensures that all related processes (e.g., server, PlayIt.gg) are properly handled and terminated when closing the main window.

## How to Use

### Running the Application:
1. **Download and Setup**:
   - Ensure all dependencies (see **Requirements**) are installed.
   - Place `ForgeServer.exe` and `forge-1.16.5-36.2.42.jar` in the same directory.

2. **Launching**:
   - Double-click `ForgeServer.exe` to start the application.

3. **Configuring**:
   - Adjust memory settings (`Xmx`, `Xms`) and server properties directly from the interface.
   - Enable **PlayIt.gg** support from the configuration panel if needed.

4. **Starting the Server**:
   - Click `Start Server` to launch the Minecraft Forge server.
   - If PlayIt.gg support is enabled, your tunneling URL will be displayed.

### Debug Mode:
- Enable **Debug Mode** to monitor PlayIt.gg's output in a separate window for advanced troubleshooting.

## Requirements

- **ALL WILL BE BUNDLED IN THE INSTALLER (WHEN RELEASED)**

- **Java Runtime Environment (JRE)**:
  - Required to run the Minecraft server. ([Download](https://www.java.com/en/download/manual.jsp)).

- **Minecraft Forge Server JAR**:
  - `forge-1.16.5-36.2.42.jar` must be located in the same directory as `ForgeServer.exe`. ([Current Forge Version](https://maven.minecraftforge.net/net/minecraftforge/forge/1.16.5-36.2.42/forge-1.16.5-36.2.42-installer.jar)).

- **.NET 8.0 Desktop Runtime**:
  - Required for the ForgeServer application to run. ([Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-8.0.402-windows-x64-installer)).
  - x86 Unsupported as of right now.

## License

This project is licensed under the MIT License - see the [LICENSE](./LICENSE.md) file for details.

## Acknowledgements

- **Minecraft Community**:
  - For their continued support and contributions to the Minecraft ecosystem.
- **Minecraft Forge Developers**:
  - For creating and maintaining an exceptional modding platform.
- **PlayIt.gg**:
  - For providing seamless server tunneling solutions.
- **Contributors**:
  - Everyone who has contributed to improving and refining this project.
