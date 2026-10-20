# Mom's Pie Logo Tool

Replaces the logo on the Mom's Pie sign in the `hane_mpie_base` FiveM map with your own image.
Works with all Mom's Pie locations: Great Ocean Highway, Del Perro and Vespucci Canals.
Runs on any Windows PC, nothing to install. It uses the .NET Framework that already ships with Windows.

## Usage

1. Extract the `logo_tool` folder. The best place for it is inside your `hane_mpie_base` resource folder.
2. Put your image into `PUT_YOUR_LOGO_HERE` (PNG, JPG, BMP, GIF or TIFF).
3. Run `ReplaceLogo.bat` and follow the steps on screen:
   - **Step 1** finds `hane_mpie_base`. If it can't, drag & drop the folder into the window.
   - **Step 2** asks which location your server uses and checks that its resources are installed.
   - **Step 3** shows your image and what will change. Type `Y` to continue.
   - **Step 4** replaces the logo and opens a preview.
4. Follow the `WHAT TO DO NOW` list at the end. It checks your `server.cfg` and tells you what to restart.

All locations use the same sign texture in `hane_mpie_base`, so every location on the server shows the new logo.
Any image size works, it is converted to the texture size automatically.
A PNG with a transparent background gives the best result.

To undo, run `RestoreOriginalLogo.bat`. Every change is also backed up in `backups`.

If the old logo still shows in game, close FiveM, delete the `cache`, `server-cache` and `server-cache-priv`
folders in `%LocalAppData%\FiveM\FiveM.app\data` and reconnect.

## Settings

`settings.ini`

| Key | Values | Default |
| --- | --- | --- |
| `Mode` | `Fit` keeps proportions, `Fill` fills the square and crops the edges, `Stretch` stretches to the exact size | `Fit` |
| `Margin` | Empty space around the logo in percent, `0` to `45` | `0` |
| `BasePath` | Path to the `hane_mpie_base` folder, empty means automatic | empty |

## How it works

- Looks for `hane_mpie_base` next to the tool, then in the server's `resources` folder, then asks.
- Finds the location resources and the `ensure` lines in `server.cfg`, following `exec` files.
- Reads the RSC7 texture dictionary (`.ytd`) and finds the `hane_mpie_logo` texture.
- Resizes the image with premultiplied alpha, so transparent edges get no halos.
- Encodes DXT5 (BC3) with the full mip chain and writes it back in place.
- Reads the file again to verify the write, restores the previous file if it does not match.
- Keeps one original backup per server, so moving the tool between servers can't mix them up.

## Files

```
ReplaceLogo.bat            replace the logo
RestoreOriginalLogo.bat    bring back the original logo
settings.ini               settings
PUT_YOUR_LOGO_HERE/        input image
_tool/LogoTool.cs          finder, texture reader, resizer and DXT5 encoder
_tool/run.ps1              compiles LogoTool.cs with Add-Type and runs it
```

Made by Hane Studios.
