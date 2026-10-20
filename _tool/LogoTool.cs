using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

public static class LogoTool
{
    const string InputFolder = "PUT_YOUR_LOGO_HERE";
    const string BackupFolder = "backups";
    const string PreviewFolder = "preview";
    const string BaseResource = "hane_mpie_base";
    const string YtdRelative = @"stream\custom_texture\ch1_11_blgs01.ytd";
    const string TextureName = "hane_mpie_logo";
    static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" };

    class Location
    {
        public string Key, Name, Folder;
        public string[] Resources;
        public Location(string key, string name, string folder, params string[] resources)
        { Key = key; Name = name; Folder = folder; Resources = resources; }
    }

    static readonly Location[] Locations = {
        new Location("greatocean", "Great Ocean Highway", "[Hane_MPie_GreatOcean]", "hane_data_greatocean", "hane_mpie_greatocean"),
        new Location("delperro",   "Del Perro",           "[Hane_MPie_DelPerro]",   "hane_data_delperro",   "hane_mpie_delperro"),
        new Location("vespucci",   "Vespucci Canals",     "[Hane_Mpie_Vespucci]",   "hane_data_vespucci",   "hane_mpie_vespucci"),
    };

    class Context
    {
        public string ToolDir, BasePath, YtdPath, ResourcesRoot, ServerCfg;
        public Dictionary<string, string> Settings, State;
        public Dictionary<string, string> FoundResources;
        public List<Location> Selected = new List<Location>();
    }

    class ToolException : Exception { public ToolException(string m) : base(m) { } }
    class CancelException : Exception { }

    public static int Run(string mode, string toolDir, string locationArg)
    {
        try
        {
            Context ctx = new Context();
            ctx.ToolDir = toolDir;
            ctx.Settings = LoadIni(Path.Combine(toolDir, "settings.ini"));
            ctx.State = LoadIni(StatePath(toolDir));

            Header(mode == "restore" ? 3 : 4, 1, "Finding the Mom's Pie files");
            FindBase(ctx);
            Ok("Found " + BaseResource + ": " + ctx.BasePath);
            ctx.ResourcesRoot = FindResourcesRoot(ctx.BasePath);
            ctx.FoundResources = FindResources(ctx.ResourcesRoot);
            ctx.ServerCfg = FindServerCfg(ctx.ResourcesRoot);
            SaveState(ctx);

            if (mode == "restore") return Restore(ctx, locationArg);
            return Replace(ctx, locationArg);
        }
        catch (CancelException)
        {
            Console.WriteLine();
            Info("Cancelled. Nothing was changed.");
            return 2;
        }
        catch (ToolException e)
        {
            Console.WriteLine();
            Error(e.Message);
            return 1;
        }
        catch (Exception e)
        {
            Console.WriteLine();
            Error("Unexpected error: " + e.Message + "\n" + e.StackTrace);
            return 1;
        }
    }

    static int Replace(Context ctx, string locationArg)
    {
        string mode = Setting(ctx.Settings, "Mode", "Fit").ToLowerInvariant();
        if (mode != "fit" && mode != "fill" && mode != "stretch")
            throw new ToolException("Invalid Mode in settings.ini: \"" + mode + "\". Use Fit, Fill or Stretch.");
        double margin;
        if (!double.TryParse(Setting(ctx.Settings, "Margin", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out margin) || margin < 0 || margin > 45)
            throw new ToolException("Invalid Margin in settings.ini. Use a number between 0 and 45.");

        Header(4, 2, "Choose your location");
        ChooseLocation(ctx, locationArg);
        SaveState(ctx);

        Header(4, 3, "Choose your image");
        FileInfo input = WaitForImage(ctx.ToolDir);
        int srcW, srcH;
        bool hasTransparency;
        float[] src = LoadImagePremul(input.FullName, out srcW, out srcH, out hasTransparency);
        Ok("Image: " + input.Name + " (" + srcW + "x" + srcH + ")");
        if (!hasTransparency)
            Warn("This image has no transparent background. The whole image (including its background)\n" +
                 "  will be visible on the sign. For best results use a PNG with a transparent background.");

        Ytd ytd = ReadYtd(ctx.YtdPath);
        Tex tex = FindTexture(ytd, TextureName);
        int W = tex.W, H = tex.H;
        bool needsResize = !(srcW == W && srcH == H && margin == 0);

        Console.WriteLine();
        Write(ConsoleColor.White, "Ready to replace the sign logo:");
        Info("  Location : " + SelectedNames(ctx));
        Info("  Image    : " + input.Name);
        Info("  Size     : " + (needsResize
            ? srcW + "x" + srcH + " -> converted to " + W + "x" + H + " (mode: " + mode + ", margin: " + margin.ToString(CultureInfo.InvariantCulture) + "%)"
            : W + "x" + H + " (already the right size, no conversion needed)"));
        Info("  File     : " + ctx.YtdPath);
        if (!AskYesNo("Continue?")) throw new CancelException();

        Header(4, 4, "Replacing the logo");
        float[] canvas;
        if (!needsResize) canvas = src;
        else
        {
            Step("Converting " + srcW + "x" + srcH + " -> " + W + "x" + H + "...");
            canvas = BuildCanvas(src, srcW, srcH, W, H, mode, margin);
        }

        Step("Encoding DXT5 with " + tex.Levels + " mip levels...");
        int[] fallback = AverageColor(canvas, W * H);
        byte[] encoded = new byte[tex.Size];
        int pos = 0, w = W, h = H;
        float[] level = canvas;
        for (int l = 0; l < tex.Levels; l++)
        {
            byte[] mip = EncodeDxt5(ToRgba8(level, w * h, fallback), w, h);
            Buffer.BlockCopy(mip, 0, encoded, pos, mip.Length);
            pos += mip.Length;
            if (l + 1 < tex.Levels)
            {
                int nw = Math.Max(1, w >> 1), nh = Math.Max(1, h >> 1);
                level = Resize(level, w, h, nw, nh);
                w = nw; h = nh;
            }
        }
        if (pos != tex.Size) throw new ToolException("Internal error: encoded size " + pos + " != " + tex.Size);

        Step("Backing up the current file...");
        string backupDir = Path.Combine(ctx.ToolDir, BackupFolder);
        Directory.CreateDirectory(backupDir);
        string original = OriginalBackupPath(ctx);
        if (!File.Exists(original))
        {
            File.Copy(ctx.YtdPath, original);
            Info("  Original Mom's Pie logo saved: " + original);
        }
        string backup = Path.Combine(backupDir, Path.GetFileName(ctx.YtdPath) + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak");
        File.Copy(ctx.YtdPath, backup, true);

        Step("Writing the new logo...");
        Buffer.BlockCopy(encoded, 0, ytd.Data, tex.Offset, encoded.Length);
        try { WriteYtd(ctx.YtdPath, ytd); }
        catch (IOException e)
        {
            throw new ToolException("Could not write the texture file: " + e.Message +
                "\nIf the server or another program (OpenIV, CodeWalker) has the file open, close it and try again.");
        }

        Step("Verifying...");
        Ytd check = ReadYtd(ctx.YtdPath);
        Tex t2 = FindTexture(check, TextureName);
        for (int i = 0; i < encoded.Length; i++)
            if (check.Data[t2.Offset + i] != encoded[i])
            {
                File.Copy(backup, ctx.YtdPath, true);
                throw new ToolException("Verification failed, the previous file was restored.");
            }

        string previewDir = Path.Combine(ctx.ToolDir, PreviewFolder);
        Directory.CreateDirectory(previewDir);
        byte[] decoded = DecodeDxt5(check.Data, t2.Offset, W, H);
        string previewDark = Path.Combine(previewDir, "logo_preview_on_dark.png");
        SavePng(Path.Combine(previewDir, "logo_512x512.png"), decoded, W, H, false);
        SavePng(previewDark, decoded, W, H, true);

        Console.WriteLine();
        Success("==================================================================");
        Success("  DONE! The Mom's Pie sign logo was replaced successfully.");
        Success("==================================================================");
        Info("  Location : " + SelectedNames(ctx));
        Info("  Image    : " + input.Name + " (" + srcW + "x" + srcH + ")");
        Info("  Preview  : " + previewDark);
        Info("  Backup   : " + backup);
        NextSteps(ctx, false);
        return 0;
    }

    static int Restore(Context ctx, string locationArg)
    {
        Header(3, 2, "Checking the backup");
        string original = OriginalBackupPath(ctx);
        if (!File.Exists(original))
            throw new ToolException("No original backup was found for this server, so the logo has never been\n" +
                "replaced with this tool. Nothing to restore.");
        Ok("Original backup found: " + original);
        ctx.Selected.AddRange(SelectedFromState(ctx, locationArg));

        Header(3, 3, "Restoring the original logo");
        try { File.Copy(original, ctx.YtdPath, true); }
        catch (IOException e)
        {
            throw new ToolException("Could not write the texture file: " + e.Message +
                "\nIf the server or another program (OpenIV, CodeWalker) has the file open, close it and try again.");
        }
        Console.WriteLine();
        Success("==================================================================");
        Success("  DONE! The original Mom's Pie logo was restored.");
        Success("==================================================================");
        NextSteps(ctx, true);
        return 0;
    }

    static bool IsBase(string dir)
    {
        return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, YtdRelative));
    }

    static void FindBase(Context ctx)
    {
        List<string> candidates = new List<string>();
        candidates.Add(Setting(ctx.Settings, "BasePath", ""));
        candidates.Add(Setting(ctx.State, "BasePath", ""));
        candidates.Add(Path.Combine(ctx.ToolDir, ".."));
        foreach (string c in candidates)
        {
            if (c.Length == 0) continue;
            string full = Path.GetFullPath(Path.IsPathRooted(c) ? c : Path.Combine(ctx.ToolDir, c));
            if (IsBase(full)) { SetBase(ctx, full); return; }
        }

        DirectoryInfo d = new DirectoryInfo(ctx.ToolDir);
        for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
        {
            string root = null;
            if (d.Name.Equals("resources", StringComparison.OrdinalIgnoreCase)) root = d.FullName;
            else if (Directory.Exists(Path.Combine(d.FullName, "resources"))) root = Path.Combine(d.FullName, "resources");
            if (root == null) continue;
            string found = SearchBase(root);
            if (found != null) { SetBase(ctx, found); return; }
        }

        Warn("The " + BaseResource + " folder could not be found automatically.");
        Info("  This happens when the logo_tool folder is not inside your server's resources.");
        Console.WriteLine();
        for (int attempt = 0; attempt < 5; attempt++)
        {
            Write(ConsoleColor.White, "Drag & drop your \"" + BaseResource + "\" folder (or your server's \"resources\" folder)");
            Write(ConsoleColor.White, "into this window and press Enter. Type Q to quit.");
            string line = Prompt("> ");
            if (line == null || line.Trim().Equals("q", StringComparison.OrdinalIgnoreCase)) throw new CancelException();
            string p = line.Trim().Trim('"').Trim();
            if (p.Length == 0) continue;
            if (File.Exists(p) && p.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase))
            {
                DirectoryInfo up = new DirectoryInfo(p).Parent;
                if (up != null && up.Parent != null && up.Parent.Parent != null) p = up.Parent.Parent.FullName;
            }
            if (IsBase(p)) { SetBase(ctx, Path.GetFullPath(p)); return; }
            if (Directory.Exists(p))
            {
                Step("Searching in " + p + " ...");
                string found = SearchBase(p);
                if (found != null) { SetBase(ctx, found); return; }
            }
            Warn("\"" + BaseResource + "\" was not found there. Please try again.\n" +
                 "  It is the folder that contains: " + YtdRelative);
        }
        throw new ToolException("Could not find the " + BaseResource + " folder.\n" +
            "Copy the logo_tool folder into your " + BaseResource + " resource folder and run it again.");
    }

    static void SetBase(Context ctx, string dir)
    {
        ctx.BasePath = Path.GetFullPath(dir).TrimEnd('\\');
        ctx.YtdPath = Path.Combine(ctx.BasePath, YtdRelative);
    }

    static string SearchBase(string root)
    {
        foreach (string dir in WalkDirs(root, 6))
            if (Path.GetFileName(dir).Equals(BaseResource, StringComparison.OrdinalIgnoreCase) && IsBase(dir)) return dir;
        return null;
    }

    static IEnumerable<string> WalkDirs(string root, int maxDepth)
    {
        Queue<KeyValuePair<string, int>> q = new Queue<KeyValuePair<string, int>>();
        q.Enqueue(new KeyValuePair<string, int>(root, 0));
        int visited = 0;
        while (q.Count > 0 && visited++ < 30000)
        {
            KeyValuePair<string, int> cur = q.Dequeue();
            string[] subs;
            try { subs = Directory.GetDirectories(cur.Key); }
            catch (Exception) { continue; }
            foreach (string s in subs)
            {
                string name = Path.GetFileName(s);
                if (name.StartsWith(".") || name.Equals("stream", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) continue;
                yield return s;
                if (cur.Value + 1 < maxDepth) q.Enqueue(new KeyValuePair<string, int>(s, cur.Value + 1));
            }
        }
    }

    static string FindResourcesRoot(string basePath)
    {
        for (DirectoryInfo d = new DirectoryInfo(basePath); d != null; d = d.Parent)
            if (d.Name.Equals("resources", StringComparison.OrdinalIgnoreCase)) return d.FullName;
        DirectoryInfo p = new DirectoryInfo(basePath).Parent;
        return p == null ? basePath : (p.Parent != null ? p.Parent.FullName : p.FullName);
    }

    static Dictionary<string, string> FindResources(string root)
    {
        HashSet<string> wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Location l in Locations) foreach (string r in l.Resources) wanted.Add(r);
        Dictionary<string, string> found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string dir in WalkDirs(root, 6))
        {
            string name = Path.GetFileName(dir);
            if (wanted.Contains(name) && !found.ContainsKey(name) &&
                (File.Exists(Path.Combine(dir, "fxmanifest.lua")) || File.Exists(Path.Combine(dir, "__resource.lua"))))
                found[name] = dir;
        }
        return found;
    }

    static bool Installed(Context ctx, Location l)
    {
        foreach (string r in l.Resources) if (!ctx.FoundResources.ContainsKey(r)) return false;
        return true;
    }

    static void ChooseLocation(Context ctx, string locationArg)
    {
        string last = Setting(ctx.State, "LastLocation", "");
        string answer = string.IsNullOrEmpty(locationArg) ? null : locationArg;
        if (answer == null)
        {
            Write(ConsoleColor.White, "Which Mom's Pie location does your server use?");
            Console.WriteLine();
            for (int i = 0; i < Locations.Length; i++)
            {
                Console.Write("  [" + (i + 1) + "] " + Locations[i].Name.PadRight(22));
                if (Installed(ctx, Locations[i])) Write(ConsoleColor.Green, "installed");
                else Write(ConsoleColor.DarkGray, "not found on this server");
            }
            Console.WriteLine("  [" + (Locations.Length + 1) + "] More than one location (all installed ones)");
            Console.WriteLine("  [Q] Quit");
            Console.WriteLine();
            string lastName = LocationNames(last);
            if (lastName != null) Info("Press Enter to use your last choice: " + lastName);
        }

        for (int attempt = 0; ; attempt++)
        {
            string line = answer ?? Prompt("Type a number and press Enter: ");
            answer = null;
            if (line == null) throw new CancelException();
            line = line.Trim();
            if (line.Equals("q", StringComparison.OrdinalIgnoreCase)) throw new CancelException();
            if (line.Length == 0 && LocationNames(last) != null) line = last;

            List<Location> sel = ParseLocations(line);
            if (sel == null)
            {
                if (attempt >= 9) throw new CancelException();
                Warn("Please type a number from 1 to " + (Locations.Length + 1) + ", or Q to quit.");
                continue;
            }
            if (sel.Count == Locations.Length)
            {
                List<Location> inst = sel.FindAll(delegate (Location l) { return Installed(ctx, l); });
                if (inst.Count > 0) sel = inst;
            }
            ctx.Selected = sel;
            break;
        }
        Ok("Location: " + SelectedNames(ctx));

        List<string> missing = new List<string>();
        foreach (Location l in ctx.Selected)
            foreach (string r in l.Resources)
                if (!ctx.FoundResources.ContainsKey(r)) missing.Add(r + "   (from the " + l.Folder + " folder)");
        Console.WriteLine();
        if (missing.Count > 0)
        {
            Warn("These resources for your location were not found in your resources folder:");
            foreach (string m in missing) Write(ConsoleColor.Yellow, "    - " + m);
            Info("  The logo can still be replaced, but the sign only shows up in game when the");
            Info("  location's resources are installed and started.");
            Info("  Searched in: " + ctx.ResourcesRoot);
            if (!AskYesNo("Continue anyway?")) throw new CancelException();
        }
        else
        {
            foreach (Location l in ctx.Selected)
                foreach (string r in l.Resources)
                    Ok(r + " found");
        }

        Console.WriteLine();
        Write(ConsoleColor.White, "Good to know:");
        Info("  The sign logo is stored in " + BaseResource + " and shared by all Mom's Pie locations.");
        Info("  If your server runs more than one location, all of them will show the new logo.");
    }

    static List<Location> ParseLocations(string text)
    {
        List<Location> res = new List<Location>();
        foreach (string partRaw in text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string part = partRaw.Trim().ToLowerInvariant();
            int n;
            if (int.TryParse(part, out n))
            {
                if (n == Locations.Length + 1) { res.Clear(); res.AddRange(Locations); return res; }
                if (n < 1 || n > Locations.Length) return null;
                if (!res.Contains(Locations[n - 1])) res.Add(Locations[n - 1]);
                continue;
            }
            if (part == "all") { res.Clear(); res.AddRange(Locations); return res; }
            Location match = null;
            foreach (Location l in Locations) if (l.Key == part) match = l;
            if (match == null) return null;
            if (!res.Contains(match)) res.Add(match);
        }
        return res.Count > 0 ? res : null;
    }

    static string LocationNames(string keys)
    {
        if (string.IsNullOrEmpty(keys)) return null;
        List<Location> l = ParseLocations(keys);
        if (l == null) return null;
        List<string> names = new List<string>();
        foreach (Location x in l) names.Add(x.Name);
        return string.Join(" + ", names);
    }

    static string SelectedNames(Context ctx)
    {
        if (ctx.Selected.Count == 0) return "all Mom's Pie locations";
        if (ctx.Selected.Count == Locations.Length) return "all locations (" + string.Join(", ", Array.ConvertAll(Locations, l => l.Name)) + ")";
        List<string> names = new List<string>();
        foreach (Location l in ctx.Selected) names.Add(l.Name);
        return string.Join(" + ", names);
    }

    static List<Location> SelectedFromState(Context ctx, string locationArg)
    {
        List<Location> l = ParseLocations(string.IsNullOrEmpty(locationArg) ? Setting(ctx.State, "LastLocation", "") : locationArg);
        if (l != null) return l;
        l = new List<Location>();
        foreach (Location x in Locations) if (Installed(ctx, x)) l.Add(x);
        return l;
    }

    static FileInfo WaitForImage(string toolDir)
    {
        string inputDir = Path.Combine(toolDir, InputFolder);
        Directory.CreateDirectory(inputDir);
        for (int attempt = 0; ; attempt++)
        {
            List<FileInfo> images = new List<FileInfo>();
            foreach (FileInfo f in new DirectoryInfo(inputDir).GetFiles())
                if (Array.IndexOf(ImageExtensions, f.Extension.ToLowerInvariant()) >= 0) images.Add(f);
            if (images.Count > 0)
            {
                images.Sort(delegate (FileInfo a, FileInfo b) { return b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc); });
                if (images.Count > 1)
                    Warn("Found " + images.Count + " images in " + InputFolder + ". Using the newest one: " + images[0].Name +
                         "\n  (Remove the other images from the folder if this is not the one you want.)");
                return images[0];
            }

            if (attempt == 0)
            {
                Warn("No image found in the " + InputFolder + " folder.");
                Info("  1. Copy your logo image into this folder:");
                Write(ConsoleColor.White, "       " + inputDir);
                Info("     Supported: PNG, JPG, BMP, GIF, TIFF. A PNG with a transparent background works best.");
                Info("     Any size is fine, it is converted to 512x512 automatically.");
                Info("  2. Come back to this window and press Enter.");
                if (!Console.IsInputRedirected)
                {
                    try { Process.Start("explorer.exe", "\"" + inputDir + "\""); Info("  (The folder was opened for you.)"); }
                    catch (Exception) { }
                }
            }
            else Warn("Still no image in " + InputFolder + ". Supported: " + string.Join(", ", ImageExtensions));
            string line = Prompt("Press Enter when your image is in the folder, or type Q to quit: ");
            if (line == null || line.Trim().Equals("q", StringComparison.OrdinalIgnoreCase)) throw new CancelException();
        }
    }

    static void NextSteps(Context ctx, bool restored)
    {
        List<string> needed = new List<string>();
        needed.Add(BaseResource);
        foreach (Location l in ctx.Selected) foreach (string r in l.Resources) needed.Add(r);
        string where = ctx.Selected.Count == 1 ? "the Mom's Pie at " + ctx.Selected[0].Name : "your Mom's Pie location";

        Console.WriteLine();
        Write(ConsoleColor.White, "WHAT TO DO NOW");
        Write(ConsoleColor.White, "--------------");

        Write(ConsoleColor.White, "1. Make sure these resources are started by your server:");
        List<string> cfgLines = ServerCfgEnsures(ctx);
        bool allOk = true, notInstalled = false;
        foreach (string r in needed)
        {
            string by = cfgLines == null ? null : EnsuredBy(ctx, r, cfgLines);
            if (r != BaseResource && !ctx.FoundResources.ContainsKey(r))
            {
                Write(ConsoleColor.Yellow, "     ensure " + r.PadRight(24) + "NOT INSTALLED (folder not found in resources)");
                notInstalled = true;
            }
            else if (cfgLines == null) Info("     ensure " + r);
            else if (by != null) Write(ConsoleColor.Green, "     ensure " + r.PadRight(24) + "OK (" + by + ")");
            else { Write(ConsoleColor.Yellow, "     ensure " + r.PadRight(24) + "NOT FOUND in server.cfg"); allOk = false; }
        }
        if (cfgLines == null)
            Info("   Add these lines to your server.cfg if they are not there yet.\n" +
                 "   (If you start the whole folder, for example \"ensure [stream]\", you don't need them.)");
        else if (!allOk)
            Write(ConsoleColor.Yellow, "   Add the missing lines to " + ctx.ServerCfg);
        else
            Info("   Checked: " + ctx.ServerCfg);
        if (notInstalled)
            Write(ConsoleColor.Yellow, "   Copy the missing location folder (for example [Hane_MPie_DelPerro]) into your\n" +
                                       "   server's resources folder, then add its ensure lines.");

        Console.WriteLine();
        Write(ConsoleColor.White, "2. Apply the change (choose one):");
        Info("     - Server console or txAdmin Live Console, type:   restart " + BaseResource);
        Info("     - txAdmin: Resources page > " + BaseResource + " > Restart");
        Info("     - or simply restart the whole server");

        Console.WriteLine();
        Write(ConsoleColor.White, "3. Reconnect to the server and go to " + where + ".");
        Info("   The sign on the building shows the " + (restored ? "original Mom's Pie logo." : "new logo."));

        Console.WriteLine();
        Write(ConsoleColor.White, "4. Still seeing the old logo?");
        Info("   Close FiveM, open this folder (paste it in the Explorer address bar):");
        Info("     %LocalAppData%\\FiveM\\FiveM.app\\data");
        Info("   delete the \"cache\", \"server-cache\" and \"server-cache-priv\" folders, then reconnect.");
        Info("   Every player who already visited the server may need to do this once.");

        Console.WriteLine();
        Write(ConsoleColor.White, "5. " + (restored ? "Want a custom logo again?" : "Not happy with the result?"));
        if (!restored) Info("   Put another image in " + InputFolder + " and run ReplaceLogo.bat again,");
        else Info("   Put an image in " + InputFolder + " and run ReplaceLogo.bat.");
        if (!restored) Info("   or run RestoreOriginalLogo.bat to bring back the original Mom's Pie logo.");
    }

    static string FindServerCfg(string resourcesRoot)
    {
        DirectoryInfo d = new DirectoryInfo(resourcesRoot).Parent;
        if (d == null) return null;
        string cfg = Path.Combine(d.FullName, "server.cfg");
        return File.Exists(cfg) ? cfg : null;
    }

    static List<string> ServerCfgEnsures(Context ctx)
    {
        if (ctx.ServerCfg == null) return null;
        List<string> res = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ReadCfg(ctx.ServerCfg, Path.GetDirectoryName(ctx.ServerCfg), res, seen);
        return res;
    }

    static void ReadCfg(string file, string dataDir, List<string> res, HashSet<string> seen)
    {
        if (!File.Exists(file) || !seen.Add(Path.GetFullPath(file))) return;
        foreach (string raw in File.ReadAllLines(file))
        {
            string line = raw.Trim();
            int c = line.IndexOf('#'); if (c >= 0) line = line.Substring(0, c);
            c = line.IndexOf("//"); if (c >= 0) line = line.Substring(0, c);
            string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            string cmd = parts[0].ToLowerInvariant();
            if (cmd == "ensure" || cmd == "start") res.Add(parts[1].Trim('"'));
            else if (cmd == "exec") ReadCfg(Path.Combine(dataDir, parts[1].Trim('"')), dataDir, res, seen);
        }
    }

    static string EnsuredBy(Context ctx, string resource, List<string> ensures)
    {
        List<string> names = new List<string>();
        names.Add(resource);
        string dir;
        if (resource.Equals(BaseResource, StringComparison.OrdinalIgnoreCase)) dir = ctx.BasePath;
        else ctx.FoundResources.TryGetValue(resource, out dir);
        if (dir != null)
            for (DirectoryInfo d = new DirectoryInfo(dir).Parent; d != null && !d.FullName.Equals(ctx.ResourcesRoot, StringComparison.OrdinalIgnoreCase); d = d.Parent)
                if (d.Name.StartsWith("[")) names.Add(d.Name);
        foreach (string e in ensures)
            foreach (string n in names)
                if (e.Equals(n, StringComparison.OrdinalIgnoreCase)) return "ensure " + e;
        return null;
    }

    static string StatePath(string toolDir) { return Path.Combine(Path.Combine(toolDir, "_tool"), "state.ini"); }

    static string OriginalBackupPath(Context ctx)
    {
        uint h = 2166136261;
        foreach (char ch in ctx.YtdPath.ToLowerInvariant()) { h ^= ch; h *= 16777619; }
        string legacy = Path.Combine(Path.Combine(ctx.ToolDir, BackupFolder), "ORIGINAL_" + Path.GetFileName(ctx.YtdPath) + ".bak");
        string keyed = Path.Combine(Path.Combine(ctx.ToolDir, BackupFolder), "ORIGINAL_" + Path.GetFileName(ctx.YtdPath) + "." + h.ToString("x8") + ".bak");
        return File.Exists(legacy) && !File.Exists(keyed) ? legacy : keyed;
    }

    static void SaveState(Context ctx)
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("; Remembered by the logo tool. Safe to delete.");
            if (ctx.BasePath != null) sb.AppendLine("BasePath=" + ctx.BasePath);
            if (ctx.Selected.Count > 0)
            {
                List<string> keys = new List<string>();
                foreach (Location l in ctx.Selected) keys.Add(l.Key);
                sb.AppendLine("LastLocation=" + string.Join(",", keys));
            }
            else if (ctx.State.ContainsKey("LastLocation")) sb.AppendLine("LastLocation=" + ctx.State["LastLocation"]);
            File.WriteAllText(StatePath(ctx.ToolDir), sb.ToString());
        }
        catch (Exception) { }
    }

    static Dictionary<string, string> LoadIni(string path)
    {
        Dictionary<string, string> s = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return s;
        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#' || line[0] == '[') continue;
            int eq = line.IndexOf('=');
            if (eq > 0) s[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
        }
        return s;
    }

    static string Setting(Dictionary<string, string> s, string key, string def)
    {
        string v;
        return s.TryGetValue(key, out v) && v.Length > 0 ? v : def;
    }

    static string Prompt(string text)
    {
        Write(ConsoleColor.White, text, false);
        string line = Console.ReadLine();
        if (Console.IsInputRedirected && line != null) Console.WriteLine(line);
        return line;
    }

    static bool AskYesNo(string question)
    {
        for (int i = 0; i < 10; i++)
        {
            string a = Prompt(question + " [Y/N]: ");
            if (a == null) return false;
            a = a.Trim().ToLowerInvariant();
            if (a == "y" || a == "yes") return true;
            if (a == "n" || a == "no" || a == "q") return false;
        }
        return false;
    }

    static void Header(int total, int n, string title)
    {
        Console.WriteLine();
        Write(ConsoleColor.Cyan, "STEP " + n + "/" + total + " - " + title);
        Write(ConsoleColor.DarkCyan, new string('-', 12 + title.Length));
    }

    static void Write(ConsoleColor c, string text) { Write(c, text, true); }
    static void Write(ConsoleColor c, string text, bool newline)
    {
        ConsoleColor old = Console.ForegroundColor;
        Console.ForegroundColor = c;
        if (newline) Console.WriteLine(text); else Console.Write(text);
        Console.ForegroundColor = old;
    }
    static void Step(string t) { Write(ConsoleColor.Cyan, "> " + t); }
    static void Ok(string t) { Write(ConsoleColor.Green, "  [OK] " + t); }
    static void Info(string t) { Write(ConsoleColor.Gray, t); }
    static void Warn(string t) { Write(ConsoleColor.Yellow, "WARNING: " + t); }
    static void Error(string t) { Write(ConsoleColor.Red, "ERROR: " + t); }
    static void Success(string t) { Write(ConsoleColor.Green, t); }

    class Ytd { public byte[] Header; public byte[] Data; public int SysSize; }
    class Tex { public string Name; public int W, H, Levels, Offset, Size; }

    static int PageSize(uint f)
    {
        uint n = ((f >> 27) & 1) + (((f >> 26) & 1) << 1) + (((f >> 25) & 1) << 2) + (((f >> 24) & 1) << 3) +
                 (((f >> 17) & 0x7f) << 4) + (((f >> 11) & 0x3f) << 5) + (((f >> 7) & 0xf) << 6) +
                 (((f >> 5) & 3) << 7) + (((f >> 4) & 1) << 8);
        return (int)((0x200u << (int)(f & 0xf)) * n);
    }

    static Ytd ReadYtd(string path)
    {
        byte[] raw = File.ReadAllBytes(path);
        if (raw.Length < 16 || raw[0] != 'R' || raw[1] != 'S' || raw[2] != 'C' || raw[3] != '7')
            throw new ToolException("Not a valid RSC7 texture dictionary: " + path);
        Ytd y = new Ytd();
        y.Header = new byte[16];
        Buffer.BlockCopy(raw, 0, y.Header, 0, 16);
        y.SysSize = PageSize(BitConverter.ToUInt32(raw, 8));
        int expected = y.SysSize + PageSize(BitConverter.ToUInt32(raw, 12));
        using (MemoryStream ms = new MemoryStream(raw, 16, raw.Length - 16))
        using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
        using (MemoryStream outMs = new MemoryStream())
        {
            ds.CopyTo(outMs);
            y.Data = outMs.ToArray();
        }
        if (y.Data.Length < expected) Array.Resize(ref y.Data, expected);
        return y;
    }

    static void WriteYtd(string path, Ytd y)
    {
        using (MemoryStream outMs = new MemoryStream())
        {
            outMs.Write(y.Header, 0, 16);
            using (DeflateStream ds = new DeflateStream(outMs, CompressionLevel.Optimal, true))
                ds.Write(y.Data, 0, y.Data.Length);
            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, outMs.ToArray());
            File.Copy(tmp, path, true);
            File.Delete(tmp);
        }
    }

    static int Ptr(Ytd y, int at)
    {
        ulong p = BitConverter.ToUInt64(y.Data, at);
        int page = (int)(p >> 28), off = (int)(p & 0x0fffffff);
        return page == 6 ? y.SysSize + off : off;
    }

    static string CStr(Ytd y, int at)
    {
        int e = at;
        while (y.Data[e] != 0) e++;
        return System.Text.Encoding.ASCII.GetString(y.Data, at, e - at);
    }

    static Tex FindTexture(Ytd y, string name)
    {
        int list = Ptr(y, 0x30);
        int count = BitConverter.ToUInt16(y.Data, 0x38);
        for (int i = 0; i < count; i++)
        {
            int t = Ptr(y, list + i * 8);
            string texName = CStr(y, Ptr(y, t + 0x28));
            if (!string.Equals(texName, name, StringComparison.OrdinalIgnoreCase)) continue;
            string fmt = System.Text.Encoding.ASCII.GetString(y.Data, t + 0x58, 4);
            if (fmt != "DXT5") throw new ToolException("Texture \"" + texName + "\" is not DXT5; this tool only supports DXT5.");
            Tex tex = new Tex();
            tex.Name = texName;
            tex.W = BitConverter.ToUInt16(y.Data, t + 0x50);
            tex.H = BitConverter.ToUInt16(y.Data, t + 0x52);
            tex.Levels = y.Data[t + 0x5d];
            tex.Offset = Ptr(y, t + 0x70);
            int w = tex.W, h = tex.H;
            for (int l = 0; l < tex.Levels; l++)
            {
                tex.Size += Math.Max(1, (w + 3) >> 2) * Math.Max(1, (h + 3) >> 2) * 16;
                w = Math.Max(1, w >> 1); h = Math.Max(1, h >> 1);
            }
            if (tex.Offset + tex.Size > y.Data.Length) throw new ToolException("Texture data is out of range, the file may be damaged.");
            return tex;
        }
        throw new ToolException("Texture \"" + name + "\" was not found in the texture file.");
    }

    static float[] LoadImagePremul(string path, out int w, out int h, out bool hasTransparency)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Bitmap bmp;
        try { bmp = new Bitmap(new MemoryStream(bytes)); }
        catch (Exception) { throw new ToolException("Could not read the image \"" + Path.GetFileName(path) + "\". The file may be damaged or in an unsupported format."); }
        using (bmp)
        {
            if (Array.IndexOf(bmp.PropertyIdList, 0x0112) >= 0)
            {
                int o = bmp.GetPropertyItem(0x0112).Value[0];
                RotateFlipType[] map = { RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipX,
                    RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate90FlipX,
                    RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipNone };
                if (o > 1 && o < map.Length) bmp.RotateFlip(map[o]);
            }
            w = bmp.Width; h = bmp.Height;
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] bgra = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), bgra, y * w * 4, w * 4);
            bmp.UnlockBits(bd);

            float[] f = new float[w * h * 4];
            hasTransparency = false;
            for (int i = 0; i < w * h; i++)
            {
                byte alpha = bgra[i * 4 + 3];
                if (alpha < 250) hasTransparency = true;
                float a = alpha / 255f;
                f[i * 4] = bgra[i * 4 + 2] * a;
                f[i * 4 + 1] = bgra[i * 4 + 1] * a;
                f[i * 4 + 2] = bgra[i * 4] * a;
                f[i * 4 + 3] = a;
            }
            return f;
        }
    }

    static float[] BuildCanvas(float[] src, int sw, int sh, int W, int H, string mode, double margin)
    {
        int areaW = (int)Math.Round(W * (1 - 2 * margin / 100)), areaH = (int)Math.Round(H * (1 - 2 * margin / 100));
        int tw = areaW, th = areaH;
        if (mode != "stretch")
        {
            double s = mode == "fill" ? Math.Max((double)areaW / sw, (double)areaH / sh) : Math.Min((double)areaW / sw, (double)areaH / sh);
            tw = Math.Max(1, (int)Math.Round(sw * s));
            th = Math.Max(1, (int)Math.Round(sh * s));
        }
        float[] scaled = (tw == sw && th == sh) ? src : Resize(src, sw, sh, tw, th);
        float[] canvas = new float[W * H * 4];
        int ox = (W - tw) / 2, oy = (H - th) / 2;
        int x0 = (W - areaW) / 2, y0 = (H - areaH) / 2;
        for (int y = 0; y < th; y++)
        {
            int cy = oy + y;
            if (cy < y0 || cy >= y0 + areaH) continue;
            for (int x = 0; x < tw; x++)
            {
                int cx = ox + x;
                if (cx < x0 || cx >= x0 + areaW) continue;
                Array.Copy(scaled, (y * tw + x) * 4, canvas, (cy * W + cx) * 4, 4);
            }
        }
        return canvas;
    }

    static List<KeyValuePair<int, float>>[] Weights(int dst, int src)
    {
        List<KeyValuePair<int, float>>[] res = new List<KeyValuePair<int, float>>[dst];
        double scale = (double)src / dst;
        for (int d = 0; d < dst; d++)
        {
            List<KeyValuePair<int, float>> ws = new List<KeyValuePair<int, float>>();
            if (scale >= 1)
            {
                double s0 = d * scale, s1 = s0 + scale;
                for (int s = (int)Math.Floor(s0); s < Math.Min(src, (int)Math.Ceiling(s1)); s++)
                    ws.Add(new KeyValuePair<int, float>(s, (float)(Math.Min(s1, s + 1) - Math.Max(s0, s))));
            }
            else
            {
                double c = Math.Min(src - 1, Math.Max(0, (d + 0.5) * scale - 0.5));
                int s = (int)Math.Floor(c);
                float t = (float)(c - s);
                ws.Add(new KeyValuePair<int, float>(s, 1 - t));
                if (s + 1 < src) ws.Add(new KeyValuePair<int, float>(s + 1, t));
            }
            float sum = 0;
            foreach (KeyValuePair<int, float> kv in ws) sum += kv.Value;
            for (int i = 0; i < ws.Count; i++) ws[i] = new KeyValuePair<int, float>(ws[i].Key, ws[i].Value / sum);
            res[d] = ws;
        }
        return res;
    }

    static float[] Resize(float[] src, int sw, int sh, int dw, int dh)
    {
        List<KeyValuePair<int, float>>[] wx = Weights(dw, sw), wy = Weights(dh, sh);
        float[] tmp = new float[dw * sh * 4];
        for (int y = 0; y < sh; y++)
            for (int x = 0; x < dw; x++)
                foreach (KeyValuePair<int, float> kv in wx[x])
                    for (int c = 0; c < 4; c++) tmp[(y * dw + x) * 4 + c] += src[(y * sw + kv.Key) * 4 + c] * kv.Value;
        float[] outp = new float[dw * dh * 4];
        for (int y = 0; y < dh; y++)
            foreach (KeyValuePair<int, float> kv in wy[y])
                for (int x = 0; x < dw; x++)
                    for (int c = 0; c < 4; c++) outp[(y * dw + x) * 4 + c] += tmp[(kv.Key * dw + x) * 4 + c] * kv.Value;
        return outp;
    }

    static int[] AverageColor(float[] f, int n)
    {
        double r = 0, g = 0, b = 0, a = 0;
        for (int i = 0; i < n; i++) { r += f[i * 4]; g += f[i * 4 + 1]; b += f[i * 4 + 2]; a += f[i * 4 + 3]; }
        if (a <= 0) return new int[] { 255, 255, 255 };
        return new int[] { Clamp((int)Math.Round(r / a)), Clamp((int)Math.Round(g / a)), Clamp((int)Math.Round(b / a)) };
    }

    static int Clamp(int v) { return v < 0 ? 0 : v > 255 ? 255 : v; }

    static byte[] ToRgba8(float[] f, int n, int[] fallback)
    {
        byte[] o = new byte[n * 4];
        for (int i = 0; i < n; i++)
        {
            float a = f[i * 4 + 3];
            for (int c = 0; c < 3; c++)
                o[i * 4 + c] = (byte)(a > 1e-4f ? Clamp((int)Math.Round(f[i * 4 + c] / a)) : fallback[c]);
            o[i * 4 + 3] = (byte)Clamp((int)Math.Round(a * 255));
        }
        return o;
    }

    static int To565(double r, double g, double b)
    {
        return ((int)Math.Round(r * 31 / 255) << 11) | ((int)Math.Round(g * 63 / 255) << 5) | (int)Math.Round(b * 31 / 255);
    }

    static double[] From565(int v)
    {
        return new double[] { ((v >> 11) & 31) * 255.0 / 31, ((v >> 5) & 63) * 255.0 / 63, (v & 31) * 255.0 / 31 };
    }

    static void EncodeBlock(byte[] px, byte[] outp, int off)
    {
        int amin = 255, amax = 0;
        for (int i = 0; i < 16; i++) { int a = px[i * 4 + 3]; if (a < amin) amin = a; if (a > amax) amax = a; }
        double[] apal = new double[8];
        apal[0] = amax; apal[1] = amin;
        for (int j = 1; j < 7; j++) apal[j + 1] = ((7 - j) * amax + j * amin) / 7.0;
        ulong abits = 0;
        if (amax != amin)
            for (int i = 0; i < 16; i++)
            {
                int best = 0; double bd = double.MaxValue;
                for (int j = 0; j < 8; j++) { double d = Math.Abs(apal[j] - px[i * 4 + 3]); if (d < bd) { bd = d; best = j; } }
                abits |= (ulong)best << (3 * i);
            }
        outp[off] = (byte)amax; outp[off + 1] = (byte)amin;
        for (int j = 0; j < 6; j++) outp[off + 2 + j] = (byte)(abits >> (8 * j));

        List<double[]> pts = new List<double[]>();
        for (int i = 0; i < 16; i++) if (px[i * 4 + 3] >= 8) pts.Add(new double[] { px[i * 4], px[i * 4 + 1], px[i * 4 + 2] });
        if (pts.Count == 0) for (int i = 0; i < 16; i++) pts.Add(new double[] { px[i * 4], px[i * 4 + 1], px[i * 4 + 2] });
        double[] mean = new double[3];
        foreach (double[] p in pts) for (int c = 0; c < 3; c++) mean[c] += p[c] / pts.Count;
        double[] cov = new double[6];
        foreach (double[] p in pts)
        {
            double r = p[0] - mean[0], g = p[1] - mean[1], b = p[2] - mean[2];
            cov[0] += r * r; cov[1] += r * g; cov[2] += r * b; cov[3] += g * g; cov[4] += g * b; cov[5] += b * b;
        }
        double[] axis = { 1, 1, 1 };
        for (int k = 0; k < 8; k++)
        {
            double[] n = {
                cov[0] * axis[0] + cov[1] * axis[1] + cov[2] * axis[2],
                cov[1] * axis[0] + cov[3] * axis[1] + cov[4] * axis[2],
                cov[2] * axis[0] + cov[4] * axis[1] + cov[5] * axis[2] };
            double len = Math.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);
            if (len < 1e-6) break;
            axis = new double[] { n[0] / len, n[1] / len, n[2] / len };
        }
        double lo = double.MaxValue, hi = double.MinValue;
        double[] pLo = mean, pHi = mean;
        foreach (double[] p in pts)
        {
            double t = (p[0] - mean[0]) * axis[0] + (p[1] - mean[1]) * axis[1] + (p[2] - mean[2]) * axis[2];
            if (t < lo) { lo = t; pLo = p; }
            if (t > hi) { hi = t; pHi = p; }
        }
        int c0 = To565(pHi[0], pHi[1], pHi[2]), c1 = To565(pLo[0], pLo[1], pLo[2]);
        if (c0 < c1) { int tmp = c0; c0 = c1; c1 = tmp; }
        double[] e0 = From565(c0), e1 = From565(c1);
        double[][] cpal = { e0, e1, new double[3], new double[3] };
        for (int c = 0; c < 3; c++) { cpal[2][c] = (2 * e0[c] + e1[c]) / 3; cpal[3][c] = (e0[c] + 2 * e1[c]) / 3; }
        uint cbits = 0;
        if (c0 != c1)
            for (int i = 0; i < 16; i++)
            {
                int best = 0; double bd = double.MaxValue;
                for (int j = 0; j < 4; j++)
                {
                    double dr = cpal[j][0] - px[i * 4], dg = cpal[j][1] - px[i * 4 + 1], db = cpal[j][2] - px[i * 4 + 2];
                    double d = dr * dr + dg * dg + db * db;
                    if (d < bd) { bd = d; best = j; }
                }
                cbits |= (uint)best << (2 * i);
            }
        outp[off + 8] = (byte)c0; outp[off + 9] = (byte)(c0 >> 8);
        outp[off + 10] = (byte)c1; outp[off + 11] = (byte)(c1 >> 8);
        outp[off + 12] = (byte)cbits; outp[off + 13] = (byte)(cbits >> 8);
        outp[off + 14] = (byte)(cbits >> 16); outp[off + 15] = (byte)(cbits >> 24);
    }

    static byte[] EncodeDxt5(byte[] rgba, int w, int h)
    {
        int bw = Math.Max(1, (w + 3) >> 2), bh = Math.Max(1, (h + 3) >> 2);
        byte[] outp = new byte[bw * bh * 16], px = new byte[64];
        for (int by = 0; by < bh; by++)
            for (int bx = 0; bx < bw; bx++)
            {
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                    {
                        int sx = Math.Min(w - 1, bx * 4 + x), sy = Math.Min(h - 1, by * 4 + y);
                        for (int c = 0; c < 4; c++) px[(y * 4 + x) * 4 + c] = rgba[(sy * w + sx) * 4 + c];
                    }
                EncodeBlock(px, outp, (by * bw + bx) * 16);
            }
        return outp;
    }

    static byte[] DecodeDxt5(byte[] data, int start, int w, int h)
    {
        byte[] o = new byte[w * h * 4];
        int bw = w >> 2;
        int[] A = new int[8];
        for (int by = 0; by < h >> 2; by++)
            for (int bx = 0; bx < bw; bx++)
            {
                int k = start + (by * bw + bx) * 16, a0 = data[k], a1 = data[k + 1];
                A[0] = a0; A[1] = a1;
                if (a0 > a1) for (int j = 1; j < 7; j++) A[j + 1] = ((7 - j) * a0 + j * a1) / 7;
                else { for (int j = 1; j < 5; j++) A[j + 1] = ((5 - j) * a0 + j * a1) / 5; A[6] = 0; A[7] = 255; }
                ulong abits = 0;
                for (int j = 0; j < 6; j++) abits |= (ulong)data[k + 2 + j] << (8 * j);
                double[] C0 = From565(data[k + 8] | (data[k + 9] << 8)), C1 = From565(data[k + 10] | (data[k + 11] << 8));
                double[][] C = { C0, C1, new double[3], new double[3] };
                for (int c = 0; c < 3; c++) { C[2][c] = (2 * C0[c] + C1[c]) / 3; C[3][c] = (C0[c] + 2 * C1[c]) / 3; }
                uint cb = BitConverter.ToUInt32(data, k + 12);
                for (int i = 0; i < 16; i++)
                {
                    int q = ((by * 4 + (i >> 2)) * w + bx * 4 + (i & 3)) * 4, ci = (int)((cb >> (2 * i)) & 3);
                    o[q] = (byte)C[ci][0]; o[q + 1] = (byte)C[ci][1]; o[q + 2] = (byte)C[ci][2];
                    o[q + 3] = (byte)A[(int)((abits >> (3 * i)) & 7)];
                }
            }
        return o;
    }

    static void SavePng(string path, byte[] rgba, int w, int h, bool onDark)
    {
        byte[] bgra = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++)
        {
            float a = rgba[i * 4 + 3] / 255f;
            for (int c = 0; c < 3; c++)
                bgra[i * 4 + 2 - c] = onDark ? (byte)Math.Round(rgba[i * 4 + c] * a + 40 * (1 - a)) : rgba[i * 4 + c];
            bgra[i * 4 + 3] = onDark ? (byte)255 : rgba[i * 4 + 3];
        }
        using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < h; y++)
                Marshal.Copy(bgra, y * w * 4, new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), w * 4);
            bmp.UnlockBits(bd);
            bmp.Save(path, ImageFormat.Png);
        }
    }
}
