# Microphone Loopback Project

This C# console application captures audio from one microphone and plays it to another device using NAudio.

## How to Build and Run

1. **Install .NET SDK**
   - Download and install from https://dotnet.microsoft.com/download

2. **Restore NuGet Packages**
   - Open a terminal in this folder and run:
     ```powershell
     dotnet restore
     ```

3. **Build the Project**
   - Run:
     ```powershell
     dotnet build
     ```

4. **Run the Application**
   - Run:
     ```powershell
     dotnet run
     ```
   - Select your input and output devices when prompted.

## Files
- `MicLoopback.cs` — Main source code
- `MicLoopback.csproj` — Project file

## Requirements
- Windows OS
- At least one microphone connected (physical or virtual)
- .NET 6.0 or later
- [NAudio](https://github.com/naudio/NAudio) (installed automatically via NuGet)

## Usage Notes
- The application only allows you to select the input device. Output is always sent to the default Windows playback device.
- To route audio to another application or device (such as a second microphone), use a virtual audio cable (e.g., VB-Audio Cable) and set it as your default output device.
- For best results, use headphones to avoid audio feedback/echo.

## Troubleshooting
- If you hear no sound, check that your selected microphone is working and your output device is set correctly in Windows.
- If you get errors about missing devices, ensure your microphones are plugged in and recognized by Windows.
- If you experience latency, try adjusting the buffer size in the code (see `WaveInEvent` and `BufferedWaveProvider` settings).

---
For any issues, let me know!
