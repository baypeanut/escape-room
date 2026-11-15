Midnight Lockdown
Assignment 6 - Meta Quest 3

What this is
You are on night shift when a transfer alert comes in. Follow it to Fred's workstation, stop the upload, then find the device that does not belong in the server room. The SIEM screens, mail client, and network panels are part of the story. They do not talk to a real network.

There are three playable chapters: the SOC floor, Fred's office, and the basement server room. The office chapter has a timer. The other two end when you finish their required tasks.

Install on Quest 3
1. Install Builds/MidnightLockdown-Quest3.apk with Meta Quest Developer Hub or adb.
2. On the headset, open Midnight Lockdown from Unknown Sources.
3. Choose New story or Continue, open Play, then press BEGIN on the chapter card.

No PC link is required after the APK is installed.

Controls
- Point the ray and pull the trigger to use screens and buttons.
- Squeeze grip to pick something up. Release grip to drop it. Point at a distant object and squeeze grip to pull it in.
- Left stick: push forward, aim, release to teleport. Right stick: turn.
- Y or B opens the in-game menu. The left wrist panel has MENU, HINT, and BAG.
- X also opens inventory. BAG can store a held item and recover loose items that fell out of reach. Deposited evidence stays put.
- Click the right stick to turn the mounted flashlight on or off.

Maintenance rail (server aisle only)
With an empty hand, hold grip near the rail and pull your hand back to move along it. Release to stop. Teleport still works if you prefer that.

Menus and settings
- Objectives, hints, and learning notes live in the wrist menu.
- Settings covers the guide marker, room ambience, radio voice, reduce flashing, and office difficulty (Normal 10 min / Experienced 8 min). Changing difficulty mid-office reloads that chapter.
- Radio voice starts off. Captions and puzzle sounds stay on either way.
- The main menu has an Achievements page for unlocks such as clean runs and finishing the recovery circuit.
- Chapter cards keep historical best times. The end-of-run review only lists chapters finished in the current session.

Office door and recovery circuit
Containment stops the office countdown, but the door still needs the four-digit code from the Containment Board. Entering the code early is refused until the required tasks are done.

In the server room, isolate the infected port first. Then seat all three cartridges on the recovery panel so BACKUP joins RECOVERY in one continuous path. You can lift and turn a cartridge again until the panel accepts the route. A bad drop sends it back to the tray. The backup console stays locked until physical containment and that route are finished.

Open the Unity project
1. Install Unity 6000.5.9f1 with Android Build Support, SDK, NDK, and OpenJDK.
2. Open this folder in Unity Hub and let packages restore.
3. Open Assets/_Project/Scenes/MainMenu.unity.
4. Confirm all four story scenes are enabled in Build Settings.
5. Switch the platform to Android, then use CSER > Build APK (Quest).
   Default output: Builds/MidnightLockdown-Quest3.apk

Editor checks
CSER > Validate Quest Story and CSER > Validate Midnight Acceptance exercise game rules and scene wiring with simulated UI clicks and object poses. They do not replace a headset playtest. Written reports for this build are under Submission/Verification.txt and Submission/Validation/. TESTING.txt has the headset pass list and the shot list for the team video.

Other files
- SOURCES.txt: outside models, textures, audio, packages, and contributor notes
- RUBRIC.txt: how the build maps to the assignment criteria (not a grade)
- SUBMISSION.txt: GitHub link plus what still has to be added by hand (video and filled peer assessments)
- Submission/Peer-Assessment-blank.pdf: blank form only; do not hand that in as a completed assessment
