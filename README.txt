Midnight Lockdown
Assignment 6 | Meta Quest 3

You are the analyst on duty when a transfer alert appears at Northwind. Find out which machine triggered it, contain the incident in Fred's office, then head to the server room to recover the files. All terminals and network activity are fictional and run inside the game.

Run on Quest 3
Install the built MidnightLockdown-Quest3 APK on the headset with Meta Quest Developer Hub or adb. Open Midnight Lockdown from Unknown Sources. A PC connection is not needed to play after installation. Start a new story and choose Begin on the chapter card.

Open in Unity
Use Unity 6000.5.9f1 with Android Build Support, SDK, NDK and OpenJDK. Open this folder in Unity Hub and let it restore the packages. The four story scenes are under Assets/_Project/Scenes. Start from MainMenu. For a build, switch to Android and use CSER > Build APK (Quest). By default the menu writes the APK to Builds/. Set NORTHWIND_APK_OUTPUT for a different output file in a batch build.

Controls
Point at a menu or keypad button and pull the trigger. Squeeze grip to pick up an object. To grab from farther away, point at the object and squeeze grip. Let go over the labeled container to drop it. Use the left stick to aim and release a teleport, and the right stick to turn. Press Y or B to open the menu, or use the wrist panel for the current objective and hints. The server room flashlight clips to your wrist after pickup.

What to test
1. Start in Night Shift. Read the alert and logs, identify the affected host, clean the laptop and swipe the badge. The office should stay locked until all required control room tasks are complete.
2. In Fred's Office, sort the printout and note into evidence, shred the confidential papers and quarantine the USB stick. Try an incorrect drop first and check the rejection feedback.
3. Use the room clues and hardware token to secure the account. Review all six messages. A wrong classification can be corrected, but it cannot count toward clearing the inbox. Stop the transfer and enter the four digits shown on the Containment Board. Knocking on the door does not enter the code for you.
4. Let the office countdown expire in a separate run. Check the game-over card, retry, and confirm the clock and objectives reset. The office defaults to 10 minutes in Normal and 8 in Experienced. Both can be overridden on the chapter's GameManager in the Inspector; zero keeps the default.
5. Look for the containment beacon and particles in the office and server room. Confirm the beacon changes after the corresponding task. Listen for feedback on successful and rejected actions. Check teleport, turning and keypad input on the headset.
6. Finish the server room: find and isolate the device, choose a clean backup, and file the incident report. Confirm the final success card appears only when the required tasks are done.

Project sources and credits are listed in SOURCES.txt. This edition adapts The Northwind Breach by the Northwind AR/VR team. The original team's gameplay footage documents their version. The submission needs its own gameplay video and each human team member's separate peer assessment.
