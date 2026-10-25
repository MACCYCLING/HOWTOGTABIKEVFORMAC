# GTBikeV on macOS CrossOver: Wattline Bluetooth bridge and screenshot fix

A working setup and troubleshooting results from October 2026. A Wahoo KICKR CORE supplies live power to GTBikeV in GTA V Legacy through a native macOS Bluetooth bridge. No physical ANT+ USB stick was used.

This repository contains the tested Windows bridge source fixes and a local screenshot patcher. It is a technical supplement to Wattline, not a complete installer.

## Tested setup and results

| Component | Version / result |
| --- | --- |
| Mac | Apple M2, macOS 15.7.4 |
| CrossOver | 26.0, Windows 10 bottle |
| GTA V | Legacy 1.0.3889.0, Story Mode |
| ScriptHookV | 3889.0 / 1158.13 |
| ScriptHookVDotNet Enhanced | 1.1.0.6, API 3.9.0.6 |
| GTBikeV | 0.8.0.4 |
| Trainer | Wahoo KICKR CORE, Bluetooth |
| Bridge | Trainer detected and live watts confirmed in GTBikeV |
| Screenshot repair | Activity saves and F5 still works after restarting the game |

A later FIT recording passed integrity checks and extends beyond the former 5 km failure point. Menu responsiveness after that longer ride was not separately confirmed. Trainer control commands were observed being forwarded, but physical resistance changes were not independently verified.

## Connection path

```text
KICKR CORE --Bluetooth--> native macOS Wattline / Python BLE
                              |
                         ANT emulator
                              |
                    TCP 127.0.0.1:51234
                              |
              DSI_SiUSBXp_3_1.DLL in CrossOver
                              |
                     GTBikeV ANT_Receiver
```

Direct Windows BLE discovery did not work in this tested CrossOver version. The bridge lets GTBikeV use its ANT receiver while the Mac handles Bluetooth.

Start from [Dev869/wattline at commit 1387087](https://github.com/Dev869/wattline/tree/1387087d6383422a86f7f9d4c85530f9421c65d8). Later upstream changes removed the ANT bridge, so the pinned commit matters. The local app also needed macOS Bluetooth authorization, launch on Bluetooth `poweredOn`, repeated BLE scans and the ANT emulator running with `--always`. Those local app changes are described here; the full customized app is not included. Adapt upstream application paths to your installation before building or launching it.

## Three fixes for an empty device list

1. **Return ASCII USB identity strings.** The tested receiver parses `SI_GetProductString` VID/PID as hexadecimal text, and the serial number as decimal text. Returning binary words caused it to discard the virtual receiver before `SI_Open`. The shim now returns `0FCF`, `1008` and `11170001`.
2. **Supply the required manufacturing interface.** The receiver loads `DSI_CP210xManufacturing_3_1.dll` before opening the device. The companion implements the five required exports for the virtual receiver. Reset forwards an ANT reset packet over the existing connection. It does not program firmware or access physical USB devices. Signatures were checked against the [Dynastream interface header](https://github.com/ursoft/ANT_Libraries/blob/master/ANT_LIB/libraries/DSI_CP210xManufacturingDLL_3_1.h).
3. **Allow reply processing during startup.** GTBikeV 0.8.0.4 allows only 100 tight receiver update calls. A successful TCP send can return before Python and the Wine receive thread process the reply. A 10 ms delay after each successful write avoided this startup race in the tested setup.

An isolated comparison with the actual receiver produced:

| Write delay | Result within the bounded startup check |
| --- | --- |
| 0 ms | 100 polls, status 10, not ready |
| 10 ms | 4 polls, status 16, ready; live power received |

`ANT_BRIDGE_WRITE_DELAY_MS` overrides the delay from 0 to 50 ms for diagnostics. The default is 10 ms. These numbers describe this installation, not a guarantee for every machine.

Build the x64 shims using MinGW-w64:

```sh
mkdir -p build
x86_64-w64-mingw32-gcc -shared -O2 -o build/DSI_SiUSBXp_3_1.DLL si_shim.c -lws2_32
x86_64-w64-mingw32-gcc -shared -O2 -o build/DSI_CP210xManufacturing_3_1.dll cp210x_shim.c
```

With GTA V fully closed, back up any existing files with those names and place both DLLs beside `GTA5.exe` in the intended bottle. The tested installation also had the same USBXpress shim in that bottle's `windows/system32` and `C:/WattlineBridge`; keep existing copies consistent if your setup loads them. Keep the working ScriptHookV `dinput8` native/builtin override. Start Wattline first, then GTA V Story Mode, press F5, activate the mod and wake the trainer by pedalling.

## GTBikeV stops near 5 km and F5 becomes unresponsive

The primary logged exception was `System.ComponentModel.Win32Exception` from `System.Drawing.Graphics.CopyFromScreen`, called by `GTBikeV.Activity.ScreenShot` during activity recording. ScriptHookVDotNet aborted the script, which explains the dead F5 menu. An illegal `Script.Wait()` error during abort cleanup was secondary.

GTBikeV 0.8.0.4 defaults to a screenshot every 5 km. The same capture method is used by manual screenshots and the activity summary save path. Setting the interval to zero does not disable capture in the inspected code.

The included patcher adds an outer catch for **Win32Exception only** to `Activity.ScreenShot`. Failed captures return without a screenshot success notification; existing bitmap/graphics disposal handlers remain in place. Other exception types propagate. The patch does not make screen capture itself work under CrossOver.

Build with Mono and Mono.Cecil 0.11.1, supplying your own Cecil assembly path:

```sh
CECIL_DLL=/absolute/path/to/Mono.Cecil.dll
mcs -r:"$CECIL_DLL" -out:build/PatchScreenshot.exe PatchScreenshot.cs
cp "$CECIL_DLL" build/Mono.Cecil.dll
mono build/PatchScreenshot.exe /path/to/original/GTBikeV.dll build/GTBikeV.patched.dll GTBikeV.Activity ScreenShot
```

Close GTA V, retain a backup of the original `scripts/GTBikeV.dll`, then replace it with the patched output under the original filename. Restore your backup to undo the repair. Do not use a patched DLL from a different GTBikeV version. The tool checks method structure and rejects signed or already guarded assemblies; the tested target is 0.8.0.4.

### Verification

The synthetic fixture passed `mono --verify-all`: normal execution works, Win32 failures are contained, unrelated exceptions propagate and `finally` cleanup runs. A semantic IL comparison of the actual mod confirmed only `Activity.ScreenShot` changed; the other 1,882 methods remained unchanged. The user then restarted GTA V, saved a short ride, accepted its summary and confirmed F5 still worked.

To run the fixture and compare your local DLLs:

```sh
mcs -out:build/GuardFixture.exe GuardFixture.cs
mono build/PatchScreenshot.exe build/GuardFixture.exe build/GuardFixture.patched.exe GuardFixture ScreenShot
mono --verify-all build/GuardFixture.patched.exe
mcs -r:"$CECIL_DLL" -out:build/VerifyPatch.exe VerifyPatch.cs
mono build/VerifyPatch.exe /path/to/original/GTBikeV.dll build/GTBikeV.patched.dll
```

## Credits and contents

The USBXpress shim derives from Devin Wilson's MIT-licensed Wattline. The companion shim, screenshot patcher and verification sources are supplied under the included MIT license. GTBikeV, GTA V, CrossOver, ScriptHookV and Mono.Cecil remain their respective projects; obtain them separately. This package contains no game/mod binaries, ride recordings, personal logs or account credentials.
