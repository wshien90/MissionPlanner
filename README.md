# FSEMI Mission Planner

FSEMI Mission Planner is the ground control station (GCS) of **FPT Semiconductor (FSEMI)** for UAVs
running ArduPilot. It plans missions, configures and flashes the autopilot, and monitors the vehicle in
flight over MAVLink.

Website: [fpt-semiconductor.com](https://fpt-semiconductor.com/)

FSEMI Mission Planner has its own version numbers, starting at 1.0.0. Version 1.0.0 is based on
Mission Planner 1.3.83.

FSEMI Mission Planner is based on [Mission Planner](https://github.com/ArduPilot/MissionPlanner)
by Michael Oborne and the ArduPilot community, and is distributed under the same license, the GNU
GPL v3 (see [COPYING.txt](COPYING.txt)). Most of the
[Mission Planner documentation](https://ardupilot.org/planner/) applies to FSEMI Mission Planner.

The source is the `fsemi` branch of this repository; `master` follows Mission Planner.

## Differences from Mission Planner

- Name, window title and installer are FSEMI Mission Planner; the executable is
  `FSEMIMissionPlanner.exe`. The code namespaces keep the `MissionPlanner` name, and plugins
  built for Mission Planner (which reference the `MissionPlanner` assembly) are redirected to it,
  so existing plugins and scripts still load.
- FPT Semiconductor's logo and icon: the app icon, the logo at the right of the menu bar (it
  opens [fpt-semiconductor.com](https://fpt-semiconductor.com/)) and the start-up screen. Their
  sources and colours are in [fsemi/branding](fsemi/branding/README.md).
- The default theme `FSEMI` uses the website's colours: navy `#010837` with the accent blue
  `#6388F7`, and the menu icons in the same blue. Mission Planner's green `BurntKermit` theme is
  still available under **Config → Planner → Theme**.
- Settings, logs, map cache and other data live in their own `FSEMI Mission Planner` folders
  (see [Paths used](#paths-used)), so it can be installed next to Mission Planner without sharing
  settings.
- No updates from, and no crash reports or anonymous usage statistics to, the Mission Planner
  servers (see [External services used](#external-services-used)).
- The whole interface is also in Vietnamese: **Config → Planner → UI Language → Tiếng Việt
  (vi-VN)**, then restart.
- The FSEMI screens (delivery, inspection, photos) are in English and Vietnamese; their text is in
  `FsemiStrings.resx` and `FsemiStrings.vi-VN.resx`, apart from Mission Planner's Crowdin
  translations.
- No question about Altitude Angel or restart prompt for the Drone ID tab on first start.
- **Delivery**:
  - The **Delivery** tab under the HUD locks and releases the payload with large LOCK / RELEASE
    buttons (release asks for confirmation), shows whether the payload is locked from the servo
    output, and lists where it was released. It uses the ArduPilot gripper when `GRIP_ENABLE` is set
    (servo function 28, `GRIP_GRAB` / `GRIP_RELEASE` PWM), otherwise a servo channel with its own
    lock and release PWM (default servo 9, 1100 / 1900). The method can also be forced in the tab.
  - On the **PLAN** map, right-click → **Delivery drop here** adds a drop to the end of the
    mission: fly to the point at cruise altitude, descend to the drop altitude and hover, release,
    hover, climb back, then optionally lock the hook and return to launch. Planned drops are marked
    in orange on the PLAN map.
  - Every release seen this session, from the Delivery tab or by the vehicle itself (a mission or
    an RC switch, seen on the servo output), is listed in the Delivery tab and marked in orange on
    the DATA map. With **Config → Planner → Enable Speech** on, a release is announced (in
    English).
- **Photos for 3D models of structures**:
  - On the **PLAN** map, right-click → **Inspection** adds a structure inspection
    to the end of the mission, levels flown from the top down with the camera
    pointed at the structure (DO_SET_ROI) and a photo at each point
    (DO_DIGICAM_CONTROL):
    - **Orbit structure here...**: circles around a tower, pole or pier at a
      radius, starting on the side facing home.
    - **Facade: wall start here**, then **Facade: wall end here...**: passes
      along a wall at a distance from it, on the home side or the far side, the
      camera square to the wall.

    The dialog shows the number of mission items, photos, flight length and an
    estimated flight time, and warns when the vehicle would fly closer than 5 m
    or the mission has more than 700 items.

    - **Camera**: choose the camera (those of the Survey tool, from
      `camerasBuiltin.xml` and your own `cameras.xml`) and the overlap between
      photos (default 80 %) and between levels (default 70 %). The dialog then
      works out the photos per orbit (at least one every 15°), the photo spacing
      along a wall and the level spacing, and shows the area one photo covers and
      the millimetres per pixel at that distance. Built in, among others: Sony
      A6400 with 16, 20 and 35 mm lenses (the same sensor as the A6100 and A6600;
      the 16 mm is the default), Sony A7R IV 35 mm, DJI Zenmuse P1 35 mm, DJI
      Mavic 3 Enterprise and DJI Phantom 4 Pro / RTK. **Set by hand** keeps the
      numbers you type.
    - **Split into flights by battery** (on by default): when the inspection
      takes longer than one battery, its levels are grouped, top down, into
      flights that each fit the battery flight time set in the dialog (20
      minutes until changed) less a 30 % reserve, counting the legs from home and
      back and take-off and landing. Each flight ends with RTL. The first flight
      goes into the mission; **Inspection → Inspection flights** lists them all
      and puts the chosen one in place of the inspection rows after the battery
      change. Each flight is also saved as its own mission file,
      `missions/inspection_<date>_<time>/flight_<n>_of_<count>.waypoints` in the
      per-user folder (with home and any mission rows before the inspection), to
      open later with **Load WP File** or on another computer;
      **Inspection flights → Open the folder of flight files** opens it.
  - **Inspection photos**: from arming to disarming, where each photo is taken (the vehicle's
    CAMERA_FEEDBACK) is recorded and saved in `photos/flight_<date>_<time>_photos.csv` in the
    per-user folder. After the flight, right-click the DATA map → **Inspection photos...**, choose
    the folder with the photos from the camera's card and the flight. The photos are matched to the
    positions recorded when they were taken (in order when the counts agree, else by time), and copies
    are made in `FSEMI_<name>` next to them, named by floor and side, e.g.
    `BuildingA_F03_NE_0012.jpg` (`TOP` for photos over the top), with the GPS
    position written into each JPEG (EXIF). With them: `photos.csv`, `geo.txt`
    (for WebODM/OpenDroneMap and other photogrammetry software) and
    `photos.kml` (Google Earth, by floor). The originals are not changed.

Planned: photo capture for 3D models of hills and mountain ranges (terrain following). See
[process.md](process.md).

## How to compile

### On Windows

Building needs **Windows with Visual Studio 2022** (MSBuild). VS Code can open the
code but cannot build it.

1. Install Visual Studio 2022 (Community is enough). In the Visual Studio
   Installer choose **More → Import configuration** and select
   [vs2022.vsconfig](vs2022.vsconfig) to get the required workloads and components.
2. Clone this repository and fetch the submodules:

   ```sh
   git clone https://github.com/wshien90/MissionPlanner.git
   cd MissionPlanner
   git checkout fsemi
   git submodule update --init
   ```

3. Open `MissionPlanner.sln` in Visual Studio and build **MissionPlanner**, or
   from a Developer Command Prompt:

   ```bat
   .nuget\nuget.exe restore MissionPlanner.sln
   msbuild -v:m -restore -t:Build -p:Configuration=Release MissionPlanner.sln
   ```

The output is in `bin\Release\net461\` (the folder name is intentional). Run
`FSEMIMissionPlanner.exe` from there.

### On GitHub Actions

The **DotNet Build** workflow builds the Windows release, runs the unit tests and
starts the app in English and Vietnamese as a smoke test. It
only runs when started by hand: **Actions → DotNet Build → Run workflow**. The
build is published as the **test-build** pre-release
([Releases → test-build](https://github.com/wshien90/MissionPlanner/releases/tag/test-build)):
download `FSEMIMissionPlannerBeta.zip`, unzip it and run `FSEMIMissionPlanner.exe`. Each
run replaces the previous test build.

The **Android Build** and **OSX Build** workflows run only by hand (**Actions → … → Run
workflow**) to save Actions minutes; **Linux Build** runs on each pull request.

### On Linux

See [Linux/README.md](Linux/README.md). Build with `./Linux/build.sh` and launch
with `./Linux/run.sh`. Not every function is available under Mono on Linux.
You can debug startup with `MONO_LOG_LEVEL=debug ./Linux/run.sh`.

## External services used

FSEMI Mission Planner still uses the public ArduPilot services for firmware, parameter
metadata and elevation data.

| Source | Use | How to disable | Custodian |
|---|---|---|---|
| https://firmware.ardupilot.org | firmware metadata, firmware, user alerts, gstreamer, SRTM, SITL | Not possible | ArduPilot Team |
| https://raw.githubusercontent.com | old param metadata, SITL config files | Not possible | ArduPilot Team |
| https://api.github.com/ | ArduPilot preload param files | Not possible | ArduPilot Team |
| https://raw.oborne.me/ | parameter metadata generator (only when the user asks to regenerate) | edit `FSEMIMissionPlanner.exe.config` | Michael Oborne |
| https://autotest.ardupilot.org | dataflash log metadata, parameter metadata | Not possible | ArduPilot Team |
| https://www.cloudflare.com | geo location for no-fly-zone selection | Not possible | Michael Oborne |
| https://discuss.cubepilot.org/ | SB2 reporting, only on affected boards when the user enters details | only used at user request | CubePilot |
| https://altitudeangel.com | UTM data | only used at user request | Altitude Angel |
| https://esua.cad.gov.hk | HK no-fly zones | user selectable | HK Gov |
| https://files.rfdesign.com.au | RFD firmwares | user initiated | RFDesign |
| https://ardupilot.org, https://www.youtube.com | help pages and videos | user initiated | ArduPilot Team |
| Many | map provider (Google/Bing/OpenStreetMap/...) | user selectable | user/many |

Not used by FSEMI Mission Planner: the Mission Planner update servers
(firmware.ardupilot.org/MissionPlanner, firmware.oborne.me and the GitHub beta builds), Google
Analytics, and the upstream crash report server. There is no update server yet: **Help → Check for
Updates** is hidden and new versions are installed by hand.

## Offline use

| Location | Use | Transferable between PCs |
|---|---|---|
| `C:\ProgramData\FSEMI Mission Planner\gmapcache` | map cache | yes |
| `C:\ProgramData\FSEMI Mission Planner\srtm` | elevation data cache | yes |
| `C:\ProgramData\FSEMI Mission Planner\*.pdef.xml` | parameter cache | yes |
| `C:\ProgramData\FSEMI Mission Planner\LogMessages*.xml` | DF log metadata cache | yes |

Supported offline data:

- Elevation: SRTM cache, GeoTIFFs in WGS84/EGM96, DTED
- Images: map cache, WMS, WMTS, GDAL

## Paths used

| Location | Use |
|---|---|
| `C:\ProgramData\FSEMI Mission Planner` | content shared by all users |
| `C:\Users\USERNAME\Documents\FSEMI Mission Planner` | per-user content (settings, logs, missions) |

On Linux both are in `/home/<user>/.local/share/FSEMI Mission Planner/`.

## Privacy

See [PRIVACY.md](PRIVACY.md).

## License

GNU General Public License v3. FSEMI Mission Planner contains the work of the Mission Planner
authors and contributors; see [COPYING.txt](COPYING.txt) and the
[Mission Planner contributors](https://github.com/ArduPilot/MissionPlanner/graphs/contributors).
