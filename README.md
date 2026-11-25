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
| M | Open / close the menu while browsing, comparing or drawing. |
| G / H | Previous / next focused Chinese asterism. |
| [ / ] | Previous / next Chinese region, or comparison case when in comparison mode. |
| V | Enter / exit comparison mode; this remains a desktop shortcut. |
| 0–9, Q/W/E/R | Original Western constellation shortcuts. |
| Right mouse drag | Look around. |
| Left click | Select a visible figure, open its details, choose menu actions or connect drawing endpoints. |
| Left controller grip | Open / close the same menu in SteamVR. |
| Right controller pointer + trigger | Aim and confirm a menu choice, focused asterism or drawing endpoint. |

Chinese browsing contains six selected regions with 37 asterisms; the complete local catalogue contains 312 entries. The current asterism is emphasised and other Chinese connections remain faintly visible. Western selections are preserved when switching cultures within the current Play session.

The six comparison cases are Five Chariots / Auriga, Three Stars / Orion, Root / Libra, Flail / Draco, Bond / Aries and Granary / Cetus. The menu provides Draw Chinese, Draw Western and Draw both, plus introductions, Undo, Reset, Finish and answer viewing.

### Draw a figure you see

Point at a star in a visible figure and click or press the right trigger. Select **Details** beside its name to read an English introduction while the reference lines are visible. On a star shared by several visible figures, use the arrows on the card to choose which one to study. **Draw this pattern** hides the reference. Select two outlined stars for each line, then open the menu with **M** or the **left grip** and submit with **Finish / Check drawing**. **Back to sky** returns to browsing.

All 51 visible figures have a card. The 34 Chinese asterisms with lines and all 14 Western figures can be drawn individually. The three single-star figures are observation only.

To play through SteamVR, start the existing PC connection for the headset and check that SteamVR detects both controllers. After the scene is running, press the **left grip** to open the menu, aim with the **right controller**, and press its **trigger** to confirm. The browsing menu provides culture, Chinese region/focus and Western figure selection. Choose **Compare / Draw**, select a case, choose **Start drawing**, then choose **Draw Chinese**, **Draw Western** or **Draw both**. Reopen the menu with the left grip for **Undo line**, **Restart drawing**, **Finish / Check drawing** or **Show answer**. A keyboard operator is no longer required to enter the comparison.

This adaptation targets **Quest 3 connected to a Windows PC through SteamVR**, using Unity 2022.3.62f3. It reuses the existing GrabGrip and GrabPinch actions and supplied bindings; it is not a standalone Quest Android build. Physical grip/trigger mappings, tracking, reconnect behaviour and headset comfort still require testing on the demonstration hardware. Without an active headset, mouse input is used automatically; **M** opens the menu.

If the original colour palette blocks the view, search **ColorPicker** in Hierarchy and turn off that object's active checkbox.

## Further information

- [Opening and operating instructions](README_FIRST_EN.txt)
- [Code, data and feature guide](PROJECT_GUIDE_EN.md)

Original third-party copyright and licence notices remain with their assets. The Chinese catalogue retains its Stellarium source attribution and CC BY-SA 4.0 metadata in `Assets/Resources/ChineseAsterisms.json`.
