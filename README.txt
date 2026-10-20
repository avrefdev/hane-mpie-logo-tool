MOM'S PIE - SIGN LOGO TOOL
==========================

Replace the logo on the Mom's Pie sign with your own image.
Nothing needs to be installed - the tool runs on any Windows PC.


HOW TO USE
----------
1. Put your image into the "PUT_YOUR_LOGO_HERE" folder.
   Supported formats: PNG, JPG, BMP, GIF, TIFF
2. Double-click "ReplaceLogo.bat".
3. A preview of the new logo opens when it is done.
4. Restart the resource (type "restart hane_mpie_base" in the server console)
   or restart the server.
   If you still see the old logo in game, clear your FiveM cache.

Any size works. If your image is not 512x512 it is converted automatically.
If there is more than one image in the folder, the newest one is used.


TIPS FOR THE BEST RESULT
------------------------
- Use a PNG with a TRANSPARENT background. With JPG (or a PNG with a solid
  background) the whole rectangle, including the background, shows on the sign.
- A wide logo (for example 1024x512) or a square one both work.
- If the logo looks too big on the sign, raise "Margin" in settings.ini
  (for example Margin=10) and run ReplaceLogo.bat again.


SETTINGS (settings.ini)
-----------------------
Mode    Fit     = keep proportions, empty space stays transparent (default)
        Fill    = keep proportions and fill the whole square (edges are cropped)
        Stretch = stretch to exactly 512x512 (may distort the image)
Margin  Empty space around the logo in percent, 0 - 45 (default 0)


UNDO
----
Double-click "RestoreOriginalLogo.bat" to bring back the original Mom's Pie logo.
Every change is also backed up in the "backups" folder.


FOLDERS
-------
PUT_YOUR_LOGO_HERE   your image goes here
preview              logo_preview_on_dark.png  - how the new logo looks
                     logo_512x512.png          - the converted 512x512 image
backups              original file and a backup from every change
_tool                program files, do not change


IMPORTANT
---------
Keep this "logo_tool" folder inside the "hane_mpie_base" resource folder.
The tool edits: hane_mpie_base\stream\custom_texture\ch1_11_blgs01.ytd
