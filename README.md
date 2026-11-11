# StarryStar3 PBL2

A Unity star-sky project for exploring Chinese asterisms and Western constellations, comparing shared stars and practising constellation drawing with a mouse or SteamVR controller.

## Open the project

1. Download this repository using **Code → Download ZIP**, then extract it, or clone it with Git.
2. In Unity Hub, open the repository folder containing **Assets**, **Packages** and **ProjectSettings**.
3. Use Unity **2022.3.62f3** and wait for the first asset/package import.
4. Open `Assets/Scenes/SampleScene.unity`, press **Play**, then click the Game view.

The repository contains the complete project. Initial Unity package import may require a network connection. SteamVR assets, controller bindings and the local OpenVR package are included.

## Controls

| Input | Action |
| --- | --- |
| C | Switch Chinese / Western viewing. |
| B | Show both cultures in ordinary browsing, without opening a panel. |
| G / H | Previous / next focused Chinese asterism. |
| [ / ] | Previous / next Chinese region, or comparison case when in comparison mode. |
| V | Enter / exit the comparison and drawing menu. |
| 0–9, Q/W/E/R | Original Western constellation shortcuts. |
| Right mouse drag | Look around. |
| Left click | Focus a Chinese member while browsing, or select drawing endpoints and menu actions. |

Chinese browsing contains six selected regions with 37 asterisms; the complete local catalogue contains 312 entries. The current asterism is emphasised and other Chinese connections remain faintly visible. Western selections are preserved when switching cultures within the current Play session.

The six comparison cases are Five Chariots / Auriga, Three Stars / Orion, Root / Libra, Flail / Draco, Bond / Aries and Granary / Cetus. The menu provides Draw Chinese, Draw Western and Draw both, plus introductions, Undo, Reset, Finish and answer viewing.

For SteamVR, use the existing right-hand pointer and confirmation binding. An operator can press V before the headset demonstration, then use the controller to operate the menu and draw. Without an active headset, mouse input is used automatically.

If the original colour palette blocks the view, search **ColorPicker** in Hierarchy and turn off that object's active checkbox.

## Further information

- [Opening and operating instructions](README_FIRST_EN.txt)
- [Code, data and feature guide](PROJECT_GUIDE_EN.md)

Original third-party copyright and licence notices remain with their assets. The Chinese catalogue retains its Stellarium source attribution and CC BY-SA 4.0 metadata in `Assets/Resources/ChineseAsterisms.json`.
