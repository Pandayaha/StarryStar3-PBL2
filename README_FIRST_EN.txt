STARRY STAR 3 - COMPLETE UNITY PROJECT

OPEN THE PROJECT

1. Extract this entire folder to a local drive, preferably D: if the system drive has limited space.
2. In Unity Hub, choose Open / Add project from disk.
3. Select the StarryStar3_Complete_20261111 folder containing Assets, Packages and ProjectSettings.
4. Use Unity Editor 2022.3.62f3. Let Unity import assets and resolve packages. The first import can take several minutes and may need an internet connection for Unity packages.
5. In Unity's Project window, open Assets/Scenes/SampleScene.unity.
6. Press Play, then click inside the Game view to use the keyboard.

The project includes the scene, star data, Chinese catalogue, scripts, shaders, label prefabs, SteamVR assets, actions, controller bindings and the local OpenVR package. Unity creates Library, Temp and IDE project files automatically when needed.

KEYBOARD AND MOUSE

C       Switch Chinese / Western single-culture viewing.
B       Toggle simultaneous Chinese + Western lines. Ordinary B viewing has no panel or menu tab.
G / H   Previous / next focused Chinese asterism in the visible region.
[ / ]   Previous / next Chinese region. Inside a comparison, previous / next case.
T       Toggle the default Purple Forbidden region.
Y       Show / hide the selected Chinese region.
V       Enter / exit the comparison and drawing menu.

Original Western figure keys:
0 Draco, 1 Pegasus, 2 Cetus, 3 Aries, 4 Perseus, 5 Auriga, 6 Orion,
7 Canis Major, 8 Monoceros, 9 Lynx, Q Bootes, W Libra,
E Ophiuchus, R Capricornus.

Hold the right mouse button and drag to look around. Click a member star while browsing a Chinese region to focus its asterism. Other Chinese connections remain faint and only the focused name is shown. Focus selection does not rotate the camera.

Western selections are preserved when switching cultures or using Both view. Explicitly switching all Western figures off keeps that empty selection. Selections belong to the current Play session.

If the original colour palette covers the sky, search for ColorPicker in Hierarchy, select the object and untick its active checkbox. Changing it during Play lasts for that run.

COMPARISON AND DRAWING

Press V to open the six selected cases and their menu. B shows both reference figures; C switches single-culture views. The menu stays available in every comparison view.

Choose Start drawing, then Draw Chinese, Draw Western or Draw both. Left-click one outlined target star and then another to make one connection. Open menu provides Undo, Reset, Finish and Show answer.

Draw Chinese keeps Western hint lines. Draw Western keeps Chinese hint lines. Draw both hides the standards and has two stages, Chinese then Western, keeping the first drawing. Finish / Check drawing submits the current pattern. A complete correct pattern produces green feedback; an incorrect pattern remains editable with an amber retry message. There is no automatic per-edge correction.

VR

Use the existing SteamVR runtime and controller bindings. An operator can press V to enter the comparison before the headset demonstration. The right-hand pointer and its confirmation action operate the menu and drawing endpoints. In ordinary Chinese browsing, pointing at a member star and confirming focuses its asterism.

The existing GrabPinch action is used. Physical buttons follow the supplied SteamVR bindings. No new controller-binding setup is required. Without an active headset, mouse input is selected automatically.

PROJECT INFORMATION

PROJECT_GUIDE_EN.md explains the main files, data sources and display features. Original third-party copyright, licence and source-attribution information is retained with the relevant assets and catalogue.
