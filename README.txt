MOM'S PIE - SIGN LOGO TOOL
==========================

Replace the logo on the Mom's Pie sign with your own image.
Works with all Mom's Pie locations: Great Ocean Highway, Del Perro and Vespucci Canals.
Nothing needs to be installed - the tool runs on any Windows PC.


HOW TO USE
----------
0. If you received the tool as a ZIP/RAR file, EXTRACT it first.
   Do not run the .bat files directly from inside the archive.
   Best place for this "logo_tool" folder: inside your "hane_mpie_base" resource folder.
1. Put your image into the "PUT_YOUR_LOGO_HERE" folder.
   Supported formats: PNG, JPG, BMP, GIF, TIFF. Any size - it is converted to 512x512.
2. Double-click "ReplaceLogo.bat" and follow the steps on screen:
     STEP 1  The tool finds your Mom's Pie files.
             (If it can't, it asks you to drag & drop your hane_mpie_base folder
             into the window.)
     STEP 2  Choose the location your server uses. The tool checks that the
             location's files are installed.
     STEP 3  The tool shows your image and what it will do. Type Y to continue.
     STEP 4  The logo is replaced and a preview opens.
3. Follow the "WHAT TO DO NOW" list at the end. It checks your server.cfg and
   tells you exactly what to restart.


GOOD TO KNOW
------------
All Mom's Pie locations use the same sign, stored in hane_mpie_base.
If your server runs more than one location, all of them show the new logo.


TIPS FOR THE BEST RESULT
------------------------
- Use a PNG with a TRANSPARENT background. With JPG (or a PNG with a solid
  background) the whole rectangle, including the background, shows on the sign.
- A wide logo (for example 1024x512) or a square one both work.
- If the logo looks too big on the sign, raise "Margin" in settings.ini
  (for example Margin=10) and run ReplaceLogo.bat again.


SETTINGS (settings.ini)
-----------------------
Mode      Fit     = keep proportions, empty space stays transparent (default)
          Fill    = keep proportions and fill the whole square (edges are cropped)
          Stretch = stretch to exactly 512x512 (may distort the image)
Margin    Empty space around the logo in percent, 0 - 45 (default 0)
BasePath  Path to your hane_mpie_base folder. Leave empty for automatic.


UNDO
----
Double-click "RestoreOriginalLogo.bat" to bring back the original Mom's Pie logo.
Every change is also backed up in the "backups" folder.


STILL SEEING THE OLD LOGO IN GAME?
----------------------------------
1. Restart the resource: type "restart hane_mpie_base" in the server console,
   or restart the server.
2. Close FiveM, open %LocalAppData%\FiveM\FiveM.app\data and delete the
   "cache", "server-cache" and "server-cache-priv" folders, then reconnect.


FOLDERS
-------
PUT_YOUR_LOGO_HERE   your image goes here
preview              logo_preview_on_dark.png  - how the new logo looks
                     logo_512x512.png          - the converted 512x512 image
backups              original file and a backup from every change
_tool                program files, do not change
