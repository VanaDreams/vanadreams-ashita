# charcapture for Windower

Takes a snapshot of your character so it can be brought to Vanadreams. It only reads: nothing is sent to
the server and nothing in the game changes.

1. In the Vanadreams launcher, open **Capture** and press **Put charcapture into Windower**
   (or copy this folder into `Windower4\addons\`).
2. In game: `//lua load charcapture`.
3. Zone once, then open the **Merit Points** and **Job Points** menus and both **Currencies** tabs of the
   Profile menu. To bring the skills of your other jobs, change to each of them in your Mog House.
4. `//capture`. The snapshot goes to `Windower4\addons\charcapture\data\<Name>.json`.
5. Back in the launcher's **Capture** page, pick it and press **Send to Vanadreams**.

`//capture show` prints the last summary again. The snapshot is the same file the Ashita charcapture writes.
