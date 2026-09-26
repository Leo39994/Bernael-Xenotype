# Dark Mirage

A Royalty psycast that creates a stationary decoy using a snapshot of the caster's appearance. The phantom has a dark outline, glowing eyes and rising blue-black flames. All included text is English.

## In-game use

Enable Royalty and the mod's existing dependencies, then restart RimWorld. In developer mode, select **Grant Dark Mirage (click pawn)** from the debug actions menu and click a player-controlled humanlike pawn. This grants the ability, raises the psylink to at least level 2 and fills psyfocus. Draft the pawn and cast **dark mirage** on an open cell.

## Mechanics

| Property | Value |
| --- | --- |
| Psycast level | 2 |
| Cast time / range | 0.6 seconds / 19.9 cells |
| Cost | 4% psyfocus, 12 neural heat |
| Cooldown | 15 seconds |
| Decoy | Stationary, 40 hit points, 10-second duration |
| Targeting | Attackable; ranged target score multiplier of 2.5 |
| Dissipation | 4 frostbite damage within 2.4 cells and line of sight, affecting standing hostile flesh creatures |
| Limit | One decoy per caster; replacement causes no damage |

Caster death, departure or faction changes dismiss the decoy without damage. The effect does not create spreading fire.

## Rendering

The vanilla pawn cache renderer captures the caster into a transparent 512-by-512 texture, including supported hair, clothing and headgear. The snapshot and full-precision eye anchors are saved with the decoy. Changing the caster's equipment does not change an existing mirage.

A distance texture is generated once per snapshot. The shader keeps the silhouette, thick outline and close glow stable while transporting translucent flame density upward. A connected flame bed supports wider tongues without isolated gaps. All fire originates outside the pawn silhouette. Animation uses game ticks and stops when paused.

On summoning, a noisy dissolve reveals the decoy from bottom to top over 0.85 seconds. Its eyes brighten from 0.85 to 1.15 seconds, then the flames rise from the base and reach full height at 1.95 seconds. The reveal fits the actual captured silhouette and uses elapsed summon time, independently of the random flame phase. Loading a save resumes that sequence rather than replaying it. Expiry retains the original 36-tick (0.6-second) fade-out.

Each decoy releases its own textures and material when removed. Rendering does not modify the caster's materials or require full-screen post-processing. The capture covers three map cells; unusually large attachments and third-party renderers that bypass the vanilla cache may need additional support.

The layered atmosphere was informed by the Killing Instinct aura in MRK's assorted little gadgets. This implementation uses an independent shader and does not copy or depend on that module's code, assets or framework.

## Build

Run from the mod root:

```powershell
& .\Source\DarkMirageUnity\build.ps1
msbuild "Source\Bernael Xenotype.csproj" -restore /t:Rebuild /p:Configuration=Debug /v:minimal /nologo
```

The shader project uses Unity 2022.3.62f3 and the Built-in Render Pipeline. The build script uses `-noUpm`; pass `-UnityPath` to select another editor installation. The C# code is compiled into the main mod assembly by `Source/Bernael Xenotype.csproj`; there is no separate project. It keeps the namespace `Bernael.DarkMirageDemo` so the defs and saved decoys stay valid. Pass `-p:RimWorldDir=...` if the game is not found.

Runtime files are `1.6/Assemblies/Bernael Xenotype.dll` and `1.6/AssetBundles/darkmirage_win`. Both must be distributed with the Defs and English language data. The shader bundle currently targets Windows / Direct3D 11.

The release build does not require local QA files. Test harnesses, verification scripts, preview tools, captures, videos, logs, temporary profiles and Unity caches are excluded from Git. Source assets, required Unity metadata, build scripts and runtime packages remain eligible for commit.
