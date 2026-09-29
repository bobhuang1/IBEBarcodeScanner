# Running the app on an Android phone (Visual Studio 2026)

Short version: pick the Android target framework, pick the phone in the device dropdown, press F5.

## 1. Before Visual Studio

On the phone, in **Settings → Developer options**:

- **USB debugging** = on.
- **Install via USB** = on (some OEMs hide this behind a SIM/PIN check).
- **Default USB configuration** = *File transfer / Android Auto* (this is what makes the ADB interface appear on most MediaTek and Samsung devices).
- **Revoke USB debugging authorisations** if you have never paired this PC, then re-plug the cable. Keep the screen unlocked; the *Allow USB debugging?* prompt only appears on an unlocked screen.

Confirm from a terminal **before** opening VS — the state must be `device`, not `unauthorized`, not `offline`:

```bash
"/c/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe" devices -l
```

`adb` is not on `PATH` on this machine and `ANDROID_HOME` is empty, so use the full path (or
`setx PATH "%PATH%;C:\Program Files (x86)\Android\android-sdk\platform-tools"` once and reopen the shell).

### If `adb devices` is empty

An empty list with the phone plugged in means **Windows did not create an ADB interface**, not that the
app is broken. Check what Windows actually enumerated:

```powershell
Get-PnpDevice -PresentOnly -Class WPD,USB | Select-Object Class,FriendlyName,InstanceId
```

- Only `MTP USB Device` (class `WPD`) and no `Android ADB Interface` / `Android Composite ADB
  Interface` → the driver or the USB mode is wrong. Fix in this order:
  1. Unplug, `adb kill-server`, replug the phone with the screen unlocked. Try a different USB port and a
     different (data-capable) cable.
  2. Change the USB notification to *File transfer*, then re-plug again.
  3. Install a USB driver: SDK Manager → *SDK Tools* → **Google USB Driver** (`extras;google;usb_driver`),
     or the OEM's own driver. Then Device Manager → the phone → *Update driver* → *Browse my computer* →
     *Let me pick from a list* → **Android ADB Interface**. `android_winusb.inf` from the Google USB
     Driver can be edited to accept an unknown `VID_xxxx&PID_xxxx` as a last resort.
  4. If the device shows under *Other devices* with a warning triangle, right-click → *Update driver* and
     point at the unpacked Google USB Driver folder.
- `unauthorized` → the RSA prompt was never accepted. Unlock the phone, tap *Allow*, tick *Always allow
  from this computer*. Re-run `adb devices`.
- `offline` → stale server. `adb kill-server` then `adb devices`.

### Android 11+ wireless debugging (no cable)

1. Phone and PC on the same network. Developer options → **Wireless debugging** → *Pair device with
   pairing code*.
2. `adb pair <ip>:<pair-port>` then type the 6-digit code.
3. `adb connect <ip>:<debug-port>` (the port on the *Wireless debugging* screen, not the pairing port).
4. The phone now appears in the VS device dropdown like a USB device.

## 2. In Visual Studio 2026

1. **Set the startup project.** The solution has three projects; right-click
   `IBEBarcode.Scanner` → *Set as Startup Project*. If the test project is selected, F5 runs tests.
2. **Pick the Android target framework.** The toolbar combo next to the green ▶ shows the target
   framework — choose **net10.0-android**. The iOS target will not build on Windows without a paired Mac.
3. **Pick the device.** The device dropdown next to it should now list the phone by its model name. If it
   still says *Android Emulator* or is empty, VS has not picked up the device: close VS, fix adb as above,
   reopen. *Tools → Options → Xamarin → Android Settings* (or *Android → Device Manager*) shows the SDK
   path VS is using — it must be `C:\Program Files (x86)\Android\android-sdk` to match the CLI.
4. **Build → Configuration Manager**: *Debug* / *Any CPU* (or *arm64*; leave it at the default).
5. **Press F5.** This builds, signs with the debug keystore, installs over adb, launches, and attaches the
   debugger. The first deploy takes a minute or two; later ones are incremental. *Ctrl+F5* runs without the
   debugger, which is faster for pure camera/NFC testing.
6. On the phone: accept **Allow IBE Barcode Scanner to take pictures** on first launch, and turn on **NFC**
   in system settings if you are testing tags.

### Without Visual Studio

```bash
# build + install + launch, one command
dotnet build src/IBEBarcode.Scanner -f net10.0-android -t:Run

# several devices attached? target one
dotnet build src/IBEBarcode.Scanner -f net10.0-android -t:Run -p:AdbTarget="-s <serial>"

# or just install an APK that was already built
ADB="/c/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe"
"$ADB" install -r src/IBEBarcode.Scanner/bin/Debug/net10.0-android/net.ibegroup.ibebarcodescanner-Signed.apk
```

Useful while the app runs:

```bash
"$ADB" logcat -s monodroid:V DOTNET:V mono-stdout:V AndroidRuntime:E
"$ADB" shell pm clear net.ibegroup.ibebarcodescanner   # wipe settings/history and start fresh
```

## 3. Two failures already hit on real hardware, and their fixes

Both are baked into `IBEBarcode.Scanner.csproj`; the notes here are so nobody "tidies them away".

### `error ADB0020` / `IncompatibleCpuAbiException` — "The package does not support the CPU architecture of this device"

The .NET for Android SDK defaults `RuntimeIdentifiers` to **`android-arm64;android-x64`** (see
`Microsoft.Android.Sdk.DefaultProperties.targets` in the installed workload), which silently excludes 32-bit
ARM. A 32-bit-only handset — one whose `ro.product.cpu.abilist` is `armeabi-v7a,armeabi` and whose
`ro.product.cpu.abilist64` is empty — therefore cannot install the default APK. The csproj now sets:

```xml
<RuntimeIdentifiers Condition="…android…">android-arm;android-arm64;android-x64</RuntimeIdentifiers>
```

`BarcodeScanning.Native.Maui` ships `armeabi-v7a` binaries for all four of its native libraries, so
`android-arm` is a genuinely supported ABI and not a hack. Confirm a device's ABI with
`adb shell getprop ro.product.cpu.abilist` and an APK's with
`unzip -l app.apk | grep lib/ | awk -F'lib/' '{print $2}' | cut -d/ -f1 | sort -u`.

Do **not** pass `-p:RuntimeIdentifier=android-arm` on the command line: it is a global property, so it also
reaches `IBEBarcode.Scanner.Core` (a plain `net10.0` project) and restore dies with
`NU1101: Unable to find package Microsoft.NETCore.App.Runtime.linux-bionic-arm`.

### `TypeLoadException: Could not load file or assembly 'IBEBarcode.Scanner.Core'` on startup

Fast deployment is the Debug default: the assemblies are left out of the APK and pushed to
`/data/data/<app>/files/.__override__/<abi>` after the install. That push missed
`IBEBarcode.Scanner.Core.dll` (the `ProjectReference` output) while pushing the other 36 assemblies, so the app
died in `MauiProgram.CreateMauiApp`. You can see the state for yourself:

```bash
adb shell run-as net.ibegroup.ibebarcodescanner ls files/.__override__/armeabi-v7a
```

The csproj now sets `EmbedAssembliesIntoApk=true` for Debug Android, so the APK carries its own assemblies and
there is no device-side state to desynchronise. Consequences worth knowing:

- The Debug APK grows to roughly **150 MB** (three ABIs, no trimming). The first `-t:Run` that has to
  repackage takes about **5 minutes** on this machine; that is the price of not depending on the override
  directory.
- A Debug APK is now worth keeping: `adb install -r` it on any test device and it runs, no build step needed.
- If you ever disable it again, the recovery from a desynced override directory is
  `adb uninstall net.ibegroup.ibebarcodescanner` (or `adb shell pm clear …`) followed by a fresh deploy —
  never a plain re-deploy, which will keep trusting its own stale stamps.

### Verified working configuration

| | |
|---|---|
| Device | Along **Stratus_C7**, Android 12 (SDK 31), `armeabi-v7a` only, serial `C7V240416051117` |
| Command | `dotnet build src/IBEBarcode.Scanner -f net10.0-android -t:Run -p:AdbTarget="-s C7V240416051117"` |
| First deploy | APK **150 MB**, `primaryCpuAbi=armeabi-v7a`, build ~4m45s, then install + launch |
| Later deploys | ~**25 s** total: the build is up to date, so only the force-stop and relaunch happen. The 150 MB is a one-off packaging cost, not a per-deploy one |
| Runtime proof | `libbarhopper_v3` mapped into the process (ML Kit initialised), a camera client in `state: 2`, and `Scan` / `NFC` / `History` / `About` rendered |

Useful checks that the app is actually alive rather than merely installed:

```bash
adb shell pidof net.ibegroup.ibebarcodescanner
adb shell dumpsys window | grep mCurrentFocus
adb shell dumpsys media.camera | grep -i "Client priority"   # a client means the preview is live
```

### When a launch wedges instead of starting

Two things bit us on the first device, both environmental:

- **The screen slept and Android reaped the app** (`dumpsys power | grep mWakefulness` → `Asleep`), leaving
  the camera-permission dialog orphaned on top of the task. Every later launch then `START`s `MainActivity`
  underneath it while `pidof` stays empty. Recovery:
  ```bash
  adb shell input keyevent KEYCODE_WAKEUP
  adb shell am force-stop net.ibegroup.ibebarcodescanner
  adb shell am force-stop com.google.android.permissioncontroller   # kills the orphaned dialog
  adb shell monkey -p net.ibegroup.ibebarcodescanner -c android.intent.category.LAUNCHER 1
  ```
  To stop it recurring while testing on the bench: `adb shell svc power stayon true` (undo with `false`).
- **Granting a runtime permission from the shell** is much faster than answering the dialog, and is worth doing
  when you are iterating on the scanner rather than testing checklist #1 itself:
  ```bash
  adb shell pm grant net.ibegroup.ibebarcodescanner android.permission.CAMERA
  # re-test the real dialog flow later with:
  adb shell pm revoke net.ibegroup.ibebarcodescanner android.permission.CAMERA
  ```

On Windows the Git Bash shell rewrites `/sdcard/...` into `/Files/Git/sdcard/...`. Prefix device paths with
`MSYS_NO_PATHCONV=1` (or use `//sdcard/...`) whenever a command takes a path that lives on the phone.

## 4. What to test once it is running

Work through `docs/testing/device-checklist.md`. Highest value first:

| Order | Checks | Why |
|---|---|---|
| 1 | 1–2 | Permissions and a first successful scan prove the plumbing. |
| 2 | 17–21 | MSI Plessey / Postnet is our own decoder, sampled during a plain scan — the part no SDK can do. |
| 3 | 22–30 | NFC, including the tag-tapped-while-app-closed cold start. |
| 4 | 3–6 | UPC-A / ISBN / UPC-E / GS1-128 normalization and AI parsing. |
| 5 | 14–16 | Link vs text vs a hostile scheme (`javascript:`, `intent://` must not launch). |
| 6 | 31–34 | Language switching, including the persisted choice after a restart. |
| 7 | 41 | **Do this once in Release.** Trimming is the one setting that can break reflection-based code paths in a way Debug never shows. |

Now open `docs/testing/device-checklist.md` and start at #1.
