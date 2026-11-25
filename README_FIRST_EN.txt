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
B       Toggle simultaneous Chinese + Western lines without automatically opening the menu.
M       Open / close the menu while browsing, comparing or drawing.
G / H   Previous / next focused Chinese asterism in the visible region.
[ / ]   Previous / next Chinese region. Inside a comparison, previous / next case.
T       Toggle the default Purple Forbidden region.
Y       Show / hide the selected Chinese region.
V       Enter / exit comparison mode; retained as a desktop shortcut.

Original Western figure keys:
0 Draco, 1 Pegasus, 2 Cetus, 3 Aries, 4 Perseus, 5 Auriga, 6 Orion,
7 Canis Major, 8 Monoceros, 9 Lynx, Q Bootes, W Libra,
E Ophiuchus, R Capricornus.

Hold the right mouse button and drag to look around. Click a member star while browsing a Chinese region to focus its asterism. Other Chinese connections remain faint and only the focused name is shown. Focus selection does not rotate the camera.

Western selections are preserved when switching cultures or using Both view. Explicitly switching all Western figures off keeps that empty selection. Selections belong to the current Play session.

If the original colour palette covers the sky, search for ColorPicker in Hierarchy, select the object and untick its active checkbox. Changing it during Play lasts for that run.

DRAW ONE FIGURE FROM THE SKY

1. Choose a visible figure in the sky. For a quick desktop example, switch to Western viewing with C if needed and use 6 to show Orion if it is hidden.
2. Click a star in the figure, or point with the right controller and press its trigger. Select Details beside the name to read the English card. When several visible figures share a star, use the card arrows to choose one. The reference lines stay visible while you read.
3. Choose Draw this pattern. Outline markers show the member stars. Select two stars to draw each line, in either order.
4. Open the practice menu with M or the left grip. Use Undo line, Restart drawing, Show answer or Finish / Check drawing. Back to sky restores browsing.

There are 48 individual drawing targets. Emperor's Seat, Guard Captain and Celestial Pass each have a single star. Their cards provide an observation cue.

COMPARISON AND DRAWING

Press M to open the menu and choose Compare / Draw, or use V as the desktop shortcut. The six selected cases are available from the comparison menu. B shows both reference figures; C switches single-culture views. M can hide or reopen the menu in any comparison view without discarding the current drawing.

Choose Start drawing, then Draw Chinese, Draw Western or Draw both. Left-click one outlined target star and then another to make one connection. Open menu provides Undo line, Restart drawing, Finish / Check drawing and Show answer.

Draw Chinese keeps Western hint lines. Draw Western keeps Chinese hint lines. Draw both hides the standards and has two stages, Chinese then Western, keeping the first drawing. Finish / Check drawing submits the current pattern. A complete correct pattern produces green feedback; an incorrect pattern remains editable with an amber retry message. There is no automatic per-edge correction.

VR

This version targets Quest 3 through a Windows PC SteamVR connection, using Unity 2022.3.62f3. It is not a standalone Quest Android application.

1. Start the headset's existing PC SteamVR connection. Confirm that SteamVR recognises the headset and both controllers, then run SampleScene in Unity.
2. Press the LEFT GRIP to open the menu. Press it again to close the menu. It works during browsing, comparison and drawing.
3. Aim the RIGHT CONTROLLER at a button and press the RIGHT TRIGGER to confirm.
4. In browsing, use Chinese / Western or Show both cultures. Use < Region / Region > and < Asterism / Asterism > for Chinese selection, and Show Chinese / Hide Chinese for visibility. Use < Western / Western > to cycle the fourteen names, then Show / Hide for the selected figure; other selected Western figures are preserved.
5. Choose Compare / Draw, select a case, then choose Start drawing and Draw Chinese, Draw Western or Draw both. No other person needs to press V.
6. Point and trigger once at each of two outlined stars to draw a connection. Press the left grip to reopen Undo line, Restart drawing, Finish / Check drawing, Show answer or Back to observe.
7. In ordinary Chinese browsing, pointing at a member star and pressing the right trigger focuses its asterism. If the headset is active but the right controller is not tracked, read the tracking status and restore controller tracking before selecting stars.

The existing GrabGrip and GrabPinch actions and supplied SteamVR bindings are reused. The intended controls are left grip for the menu and right trigger for confirmation; exact physical behaviour depends on the active binding and must be checked on the presentation hardware. Quest 3 tracking, physical controller input, disconnect/reconnect recovery and comfort have not been physically verified by headset-free checks. No Unity 6 migration, new action set or XR-loader change is part of this adaptation.

Without an active headset, mouse input is selected automatically. M opens/closes the same menu, and the existing keyboard shortcuts remain available.

PROJECT INFORMATION

PROJECT_GUIDE_EN.md explains the main files, data sources and display features. Original third-party copyright, licence and source-attribution information is retained with the relevant assets and catalogue.
