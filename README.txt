Midnight Lockdown
Assignment 6 | Meta Quest 3

A transfer alert interrupts your night shift. Trace it to Fred's workstation, stop the transfer, then contain the device hidden in the server room. The terminals and network traffic are part of the game; nothing connects to a real network.

Playing on Quest 3
Install MidnightLockdown-Quest3.apk with Meta Quest Developer Hub or adb, then open Midnight Lockdown from Unknown Sources. It runs on the headset without a PC after installation. Choose New story, Play, then BEGIN on the chapter card.

Controls
Point and pull trigger to use screens. Grip to pick up objects; release to drop them. Point and grip for distant objects. Push the left stick forward, aim and release to teleport. The right stick turns. Y or B opens the menu; the wrist panel has MENU, HINT and BAG buttons. Click the right stick to switch the mounted flashlight.

The server aisle also has a maintenance rail. With an empty hand, hold grip near the rail and pull your hand backward to move along it. Let go to stop. This is optional; you can use teleport throughout the game.

Menu and assistance
Inventory stores held objects and recovers misplaced loose items. Completed evidence stays deposited. Notes contains the lessons you have collected. Settings controls the guide marker, ambience, radio voice, reduced flashing and office difficulty. The main menu Achievements page tracks unlocks such as clean runs and circuit completion. Radio voice is off initially; radio captions and puzzle sounds stay on. Changing difficulty during the office chapter starts that chapter again.

The office allows 10 minutes in Normal or 8 in Experienced. Containment stops the transfer countdown, but the door still requires the correct code. Asking for hints counts toward the result. The final review shows completed attempts from the current session; chapter cards keep historical best times separately.

Recovery circuit
After isolating the server port, find the recovery panel. Pick up and turn the three cartridges, then release them flat against the sockets. Join BACKUP to RECOVERY with a continuous path using all three pieces. A cartridge can be lifted and turned again until the route is verified. A failed drop returns it to its tray. The backup console stays locked until physical containment and the route are complete.

Opening the source
Use Unity 6000.5.9f1 with Android Build Support, SDK, NDK and OpenJDK. Open this folder in Unity Hub and allow package restoration. Open Assets/_Project/Scenes/MainMenu.unity. The four saved scenes are already configured in Build Settings. Select Android, then CSER > Build APK (Quest). The default output is Builds/MidnightLockdown-Quest3.apk.

Verification
Run CSER > Validate Quest Story, then CSER > Validate Midnight Acceptance. These commands check game rules and scene connections in the editor. They use simulated buttons, object poses and trigger callbacks. They do not test physical Quest controllers, stereo rendering, comfort or headset frame rate. See Verification in the submission archive for the actual run reports. TESTING.txt covers the headset pass and the recording sequence.

SOURCES.txt lists contributors, models, textures, audio and software dependencies. RUBRIC.txt maps the assignment to the implementation. SUBMISSION.txt lists the remaining human submission material.
