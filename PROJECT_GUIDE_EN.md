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
| Assets/Scripts/StarField.cs | Loads and draws the original stars and fourteen Western figures. Handles C/B/V, region/focus shortcuts and Western figure keys. The controller adaptation exposes the existing browsing and Western-selection commands to the menu instead of duplicating sky-state logic. It keeps selected Western holders when another culture is shown, so switching back restores all selected figures. Western labels use short English names, camera-facing rotation and a larger character scale for readability. |
| Assets/Scripts/ChineseAsterismData.cs | Defines the catalogue classes and reads the local JSON using Unity's JsonUtility. It validates object identities, coordinate values, members and connections. |
| Assets/Scripts/ChineseAsterismController.cs | Maps Chinese members onto the existing stars or supplied catalogue coordinates. It shows six selected sky regions, containing 37 asterisms. G/H cycles the focus; only its short English name is shown and other connections use about 22% opacity. Thin rings mark focused members. |
| Assets/Scripts/SkyCultureComparisonController.cs | Provides six Chinese/Western cases, shared-star/edge comparison, three drawing modes, two-star connection selection and Finish checking. Connections use unordered pairs of HR identifiers, so drawing a connection in either direction gives the same result. |
| Assets/Scripts/SkyPracticeVrControls.cs | Opens/closes the world-space menu on left-hand GrabGrip or desktop M, and uses right-hand GrabPinch or the mouse to confirm. The browsing menu provides culture, Chinese region/focus, Western figure selection and a comparison entry. The existing case and drawing menus retain About, Undo, Reset, Finish and answer viewing. It also handles camera rebinding, tracked-controller recovery, tracking status and input blocking. |

The scripts initialise the additional components from StarField, so the main scene does not need manual Inspector assignments for these features. Asset identities are retained through their .meta files. The original Western star and edge arrays remain intact.

## Chinese catalogue

`Assets/Resources/ChineseAsterisms.json` contains 312 entries derived from Stellarium's Chinese sky culture. Its header retains source URLs, attribution, the CC BY-SA 4.0 licence, coordinate epoch and adaptation notes. The original BSC stars are reused by HR identifier. Supplemental positions and catalogue cross-identifications are retained in the JSON.

The displayed regional selection is separate from the full catalogue. Short screen labels do not replace catalogue names or change the source members and connections. Focus rings are symbolic selection marks rather than measured star or cluster sizes. Focusing changes line emphasis and label visibility without moving the stars or changing their actual brightness.

`Assets/Resources/ChineseAsterismLine.shader` draws both the ordinary Chinese lines and comparison lines, including transparent hint lines.

## Individual pattern cards and practice

Select a star in a figure that is currently visible, then open **Details** to read about that figure. When several figures share a member, the player can switch the card and highlight together. **Draw this pattern** starts practice with only the selected figure's members marked. The menu supports undo, restart, checking the complete drawing, showing the answer and returning to the previous sky view. Emperor's Seat, Guard Captain and Celestial Pass each have one member and offer observation only. The remaining 48 figures support drawing.

| File | Role in individual practice |
| --- | --- |
| Assets/Scripts/SinglePatternPractice.cs | Resolves the figures at the selected star, highlights a candidate and handles individual practice using the original members, positions and connections. This includes supplemental Chinese catalogue stars. |
| Assets/Scripts/SinglePatternDescriptions.cs | Holds a short English introduction and an observing cue for each of the 51 browse figures. |
| Assets/Scripts/StarField.cs and ChineseAsterismController.cs | Supply the existing Western figures and regional Chinese patterns, including their visibility in the current sky. |
| Assets/Scripts/SkyPracticeVrControls.cs | Provides the name tag, details card, shared-star choices and practice actions on both mouse and SteamVR. |

The Chinese names come from the bundled Stellarium catalogue. The shape cues describe the project's own line figures. Western name meanings follow the [IAU constellation list](https://www.iau.org/IAU/Astronomy-FAQs/Constellations.aspx).

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

Without an active headset, M opens/closes the menu, and the mouse ray and left click select menu buttons and star endpoints. Right-drag turns the camera. C/B, region/focus keys, the fourteen Western shortcuts and V's comparison shortcut remain available. Ordinary B viewing changes the displayed cultures without automatically opening a menu.

For PC SteamVR, press the left grip to open/close the menu in browsing, comparison or drawing. Aim the right controller and press its trigger to confirm. In browsing, the menu switches cultures, changes the Chinese region/focus and Chinese visibility, and cycles/shows/hides the selected Western figure. Compare / Draw leads to the existing six cases and three drawing modes. A keyboard operator is not required to enter comparison mode. During practice, use the left grip to reopen Undo line, Restart drawing, Finish / Check drawing, Show answer or Back to observe.

## Controller adaptation: 25 November 2026

The implementation changes mainly two scripts:

- **StarField.cs:** exposes the existing culture, Chinese region/focus/visibility and fourteen-figure Western selection commands. Menu actions use the same holders and selection state as keyboard actions, preserving multiple Western selections and their restoration. Star positions, source graph arrays and the drawing checks are not redefined.
- **SkyPracticeVrControls.cs:** reads the existing left-hand GrabGrip and right-hand GrabPinch actions. Menu toggling is handled before confirmation, so the same frame cannot also select a button or star. The menu is shown only when requested and has separate browsing and comparison/drawing controls. It suspends conflicting legacy drawing input while the menu or comparison is active and restores the saved component states afterwards. It follows a changed active camera, prefers a valid active right-hand pose, recovers after controller tracking returns, and reports missing right-controller tracking in English.

The supplied Vive, Oculus Touch and Index bindings already contain the reused actions; no new generated action set is introduced. The intended physical controls are left grip and right trigger, but the active SteamVR binding determines their exact hardware behaviour.

## Compatibility and validation boundary

The target is **Quest 3 connected to a Windows PC through SteamVR**, on the existing Unity 2022.3.62f3 project. This change does not create a standalone Quest Android build or migrate the project to Unity 6. The existing XR-loader configuration is retained.

Headset-free Unity compilation, native scene checks and synthetic rays can validate menu commands, selection preservation, UI input blocking and the drawing rules. They do not establish physical Quest 3 compatibility. Test the complete PCVR connection, left grip/right trigger mapping, tracking loss/reconnect, camera changes, menu readability and comfort with the actual headset and controllers before the demonstration. If a headset is active but the right controller is not tracked, restore tracking before attempting menu or star confirmation.

The original freehand drawing components are temporarily suspended during Chinese focus browsing and comparisons, then restored to their prior enabled states, preventing confirmation input from also creating an unrelated freehand stroke. The original component scripts and controller bindings are retained.

Use matching Unity and SteamVR installations on the presentation computer. Initial package import can require a network connection. Physical controller tracking, input mapping and headset readability should be checked on that computer before the demonstration.
