# Starry Star 3 project guide

This folder is a complete Unity 2022.3.62f3 project. Open its root folder in Unity Hub, then open `Assets/Scenes/SampleScene.unity` and press Play.

## Main folders

| Folder | Contents |
| --- | --- |
| Assets | Scenes, scripts, star catalogues, shaders, prefabs, SteamVR and controller actions/bindings. |
| Packages | Package manifest and resolved package versions. The local OpenVR archive is included under Assets/SteamVR. |
| ProjectSettings | Project, rendering, input and XR settings. |

## Main scripts

| File | Responsibility |
| --- | --- |
| Assets/Scripts/StarField.cs | Loads and draws the original stars and fourteen Western figures. Handles C/B/V, region/focus shortcuts and Western figure keys. It keeps selected Western holders when another culture is shown, so switching back restores all selected figures. Western labels use short English names, camera-facing rotation and a larger character scale for readability. |
| Assets/Scripts/ChineseAsterismData.cs | Defines the catalogue classes and reads the local JSON using Unity's JsonUtility. It validates object identities, coordinate values, members and connections. |
| Assets/Scripts/ChineseAsterismController.cs | Maps Chinese members onto the existing stars or supplied catalogue coordinates. It shows six selected sky regions, containing 37 asterisms. G/H cycles the focus; only its short English name is shown and other connections use about 22% opacity. Thin rings mark focused members. |
| Assets/Scripts/SkyCultureComparisonController.cs | Provides six Chinese/Western cases, shared-star/edge comparison, three drawing modes, two-star connection selection and Finish checking. Connections use unordered pairs of HR identifiers, so drawing a connection in either direction gives the same result. |
| Assets/Scripts/SkyPracticeVrControls.cs | Uses the desktop mouse or existing SteamVR right-hand pointer for menu and star selection. The comparison menu appears only after entering with V. Ordinary Chinese, Western and Both browsing have no panel. The drawing menu includes About, Undo, Reset, Finish and answer viewing. |

The scripts initialise the additional components from StarField, so the main scene does not need manual Inspector assignments for these features. Asset identities are retained through their .meta files. The original Western star and edge arrays remain intact.

## Chinese catalogue

`Assets/Resources/ChineseAsterisms.json` contains 312 entries derived from Stellarium's Chinese sky culture. Its header retains source URLs, attribution, the CC BY-SA 4.0 licence, coordinate epoch and adaptation notes. The original BSC stars are reused by HR identifier. Supplemental positions and catalogue cross-identifications are retained in the JSON.

The displayed regional selection is separate from the full catalogue. Short screen labels do not replace catalogue names or change the source members and connections. Focus rings are symbolic selection marks rather than measured star or cluster sizes. Focusing changes line emphasis and label visibility without moving the stars or changing their actual brightness.

`Assets/Resources/ChineseAsterismLine.shader` draws both the ordinary Chinese lines and comparison lines, including transparent hint lines.

## Six comparison cases

- Five Chariots / Auriga
- Three Stars / Orion
- Root / Libra
- Flail / Draco
- Bond / Aries
- Granary / Cetus

About this case contains the full English introduction and names. Gold indicates Chinese connections, blue Western connections, and bright magenta shared connections in the reference figures.

## Drawing behaviour

Draw Chinese uses Western reference lines; Draw Western uses Chinese reference lines. Draw both begins without standard connections and uses separate Chinese and Western stages. The first stage remains visible while the second is drawn. Magenta overlap between the player's two drawings is not a correctness judgement.

The Finish action compares the player's complete connection set with the selected reference. Missing, extra or different connections fail even when the counts happen to match. A failed check leaves the drawing editable. A successful first-stage check in Draw both advances to Western, and a successful second check completes the exercise. Editing, Undo, Reset or revealing the reference clears previous feedback.

## Input

Without an active headset, the mouse ray and left click select menu buttons and star endpoints. Right-drag turns the camera. SteamVR uses the right-hand pose and the existing GrabPinch confirmation action. The same selection path handles desktop and headset input.

The original freehand drawing components are temporarily suspended during Chinese focus browsing and comparisons, then restored to their prior enabled states, preventing confirmation input from also creating an unrelated freehand stroke. The original component scripts and controller bindings are retained.

Use matching Unity and SteamVR installations on the presentation computer. Initial package import can require a network connection. Physical controller tracking, input mapping and headset readability should be checked on that computer before the demonstration.
