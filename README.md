# Mom's Pie Logo Tool

Replaces the logo on the Mom's Pie sign in the `hane_mpie_base` FiveM map with your own image.
Runs on any Windows PC, nothing to install. It uses the .NET Framework that already ships with Windows.

## Usage

1. Place the `logo_tool` folder inside the `hane_mpie_base` resource folder.
2. Put your image into `PUT_YOUR_LOGO_HERE` (PNG, JPG, BMP, GIF or TIFF).
3. Run `ReplaceLogo.bat`. A preview opens when it is done.
4. Run `restart hane_mpie_base` in the server console, or restart the server.
   If the old logo still shows in game, clear your FiveM cache.

Any image size works, it is converted to the texture size automatically.
If the folder has more than one image, the newest one is used.
A PNG with a transparent background gives the best result.

To undo, run `RestoreOriginalLogo.bat`. Every change is also backed up in `backups`.

## Settings

`settings.ini`

| Key | Values | Default |
| --- | --- | --- |
| `Mode` | `Fit` keeps proportions, `Fill` fills the square and crops the edges, `Stretch` stretches to the exact size | `Fit` |
| `Margin` | Empty space around the logo in percent, `0` to `45` | `0` |
| `YtdPath` | Texture file, relative to the tool folder | `..\stream\custom_texture\ch1_11_blgs01.ytd` |
| `TextureName` | Texture inside the file | `hane_mpie_logo` |

## How it works

- Reads the RSC7 texture dictionary (`.ytd`) and finds the target texture.
- Resizes the image with premultiplied alpha, so transparent edges get no halos.
- Encodes DXT5 (BC3) with the full mip chain and writes it back in place.
- Reads the file again to verify the write, restores the previous file if it does not match.
- Saves `preview/logo_512x512.png` and `preview/logo_preview_on_dark.png`.

## Files

```
ReplaceLogo.bat            replace the logo
RestoreOriginalLogo.bat    bring back the original logo
settings.ini               settings
PUT_YOUR_LOGO_HERE/        input image
_tool/LogoTool.cs          texture reader, resizer and DXT5 encoder
_tool/run.ps1              compiles LogoTool.cs with Add-Type and runs it
```

Made by Hane Studios.
