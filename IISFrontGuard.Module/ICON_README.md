# Creating an Icon for IISFrontGuard NuGet Package

## Overview
The `icon.png` file is currently a placeholder. You need to replace it with an actual PNG image to display on NuGet.org.

## Icon Requirements

### Technical Specifications
- **Format**: PNG
- **Recommended Size**: 128x128 pixels
- **Minimum Size**: 64x64 pixels
- **Maximum File Size**: 1 MB
- **Background**: Transparent background is recommended but not required

### Design Guidelines
The icon should:
- Be recognizable at small sizes (32x32)
- Represent security/protection theme
- Be professional and clean
- Use colors that work well on both light and dark backgrounds

## Suggested Design Elements

### Visual Concepts
1. **Shield** - Classic security symbol
2. **Lock/Padlock** - Protection and access control
3. **Firewall Symbol** - Network protection
4. **Guard/Sentinel** - Protection theme
5. **IIS Logo Elements** - To indicate IIS integration

### Color Schemes
- **Blue tones** (#0078D4, #005A9E) - Trust, security, Microsoft ecosystem
- **Green tones** (#107C10, #00BCF2) - Safety, go/approved
- **Orange/Red accents** - Alert, protection, firewall
- **Dark backgrounds** (#2D2D30, #1E1E1E) with light foreground

## Quick Creation Options

### Option 1: Free Online Tools
1. **Canva** (https://www.canva.com)
   - Search for "shield icon" or "security logo"
   - Customize with IISFrontGuard text
   - Export as PNG (128x128)

2. **IconArchive** (https://www.iconarchive.com)
   - Search for free security icons
   - Download and resize to 128x128

3. **Flaticon** (https://www.flaticon.com)
   - Search for "shield" or "firewall"
   - Download free PNG version

### Option 2: Professional Design Tools
- Adobe Illustrator
- Adobe Photoshop
- Affinity Designer
- Figma

### Option 3: Free Desktop Tools
- **GIMP** (https://www.gimp.org) - Free Photoshop alternative
- **Inkscape** (https://inkscape.org) - Free vector graphics editor
- **Paint.NET** (https://www.getpaint.net) - Free Windows image editor

## Simple Icon Ideas

### Idea 1: Shield with "IIS"
```
???????????????
?   /\        ?
?  /  \       ?
? /IIS \      ?
? \    /      ?
?  \  /       ?
?   \/        ?
???????????????
```

### Idea 2: Lock in Shield
```
???????????????
?   /\        ?
?  /??\       ?
? /    \      ?
? \    /      ?
?  \  /       ?
?   \/        ?
???????????????
```

### Idea 3: Firewall Barrier
```
???????????????
?  ?? ?? ??  ?
?  ?? ?? ??  ?
?  ?? ?? ??  ?
?   GUARD    ?
???????????????
```

## Step-by-Step: Create Icon with Canva

1. Go to https://www.canva.com
2. Create new design: 128x128 pixels
3. Search for "shield" in elements
4. Select a shield icon (use free version)
5. Add text "IIS" or "FG" in center
6. Choose color scheme (blue/green recommended)
7. Download as PNG with transparent background
8. Save as `icon.png` in the IISFrontGuard.Module folder

## AI-Generated Icon

You can also use AI tools to generate an icon:

### DALL-E / Midjourney Prompt:
```
"A minimalist shield icon for cybersecurity software, blue gradient, 
professional, clean lines, simple design suitable for app icon, 
transparent background, 128x128 pixels"
```

### ChatGPT DALL-E Prompt:
```
"Create a simple, professional icon for an IIS security module. 
The icon should feature a shield with subtle network/firewall elements. 
Use blue and white colors. Transparent background. Suitable for NuGet package."
```

## Verification

After creating your icon, verify:
1. File is named exactly `icon.png`
2. File is located in `IISFrontGuard.Module\` folder
3. File size is under 1 MB
4. Image dimensions are at least 64x64 (128x128 recommended)
5. Icon looks good at small sizes (32x32)
6. Transparent background (optional but recommended)

## Testing the Icon

1. Build the NuGet package:
   ```
   nuget pack IISFrontGuard.Module.nuspec
   ```

2. The icon should appear in:
   - NuGet Package Explorer
   - Visual Studio NuGet Package Manager
   - NuGet.org (when published)

## Copyright Notice

?? **Important**: Ensure you have the right to use any icon you choose:
- Use royalty-free icons
- Check license requirements (some require attribution)
- Create your own original design, or
- Purchase commercial license if needed

## Need Help?

If you need professional icon design:
- Fiverr (https://www.fiverr.com) - $5-$50
- 99designs (https://99designs.com) - Logo contests
- Upwork (https://www.upwork.com) - Hire a designer

---

**Current Status**: Placeholder file created
**Action Required**: Replace `icon.png` with actual PNG image
