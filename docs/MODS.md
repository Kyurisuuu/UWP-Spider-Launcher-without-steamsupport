# Mods

The **Mods** tab layers your own `.stage` mods onto the prepared game. The prepared game index is backed up first, and **Remove mods** restores it exactly.

## What works

| Mod type | Supported |
| --- | --- |
| Replaces existing gameplay files, such as traversal, physics and other `.config` assets | Yes |
| Replaces a texture or model the Xbox preparation already converted | No, refused |
| Adds new assets (new suits, new files) | No, refused |
| `.smpcmod`, `.suit` and script mods | No; repack as `.stage` |

Mods are untested on the Xbox runtime. Test a small change first and keep the mod for the same game version as your Xbox copy.

## Make a mod

1. Install the game from Steam on your PC.
2. Use Tkachov's ALERT Assets Browser or Overstrike's Modding Tool to find and extract the config you want to change, then export your change as a `.stage`.
3. A `.stage` is a ZIP. Assets sit in numbered span folders using either the asset path or its 16-digit hex ID:

   ```text
   my-swing-mod.stage
   ├── info.json
   └── 0/
       └── configs/…/swing.config
   ```

   An unpacked folder with the same layout also works.
4. Test it on PC with Overstrike first.

## Install on Xbox

1. Open the Xbox's `DevelopmentFiles` share from your PC, as described in Device Portal's File explorer.
2. Copy the `.stage` files or folders into `SpiderManInstaller-Mods`. Open the launcher's **Mods** tab once if the folder doesn't exist yet.
3. In the launcher, open **Mods**, choose **Refresh**, then **Apply mods**.
4. Start the game.

Mods apply in name order, and a later mod wins when two change the same file. Rename a mod to start with `_` to disable it. After changing the folder, choose **Apply mods** again. Each apply starts from the clean backup, so mods never stack on an older apply.

**Remove game files** in Storage also removes applied mods.
