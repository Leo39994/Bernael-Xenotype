# RimWorld 1.7

The mod supports RimWorld 1.7 only. Version-specific content lives in `1.7/`
(Assemblies, Defs, Patches, Nephilim patches, GLSL shaders, ShaderDefs); textures,
sounds and translations shared with any later version live in `Common/`.

Dark Mirage, Malicious Seal, Dire Orb and Soul Drain render through five native
ShaderDefs and their GLSL sources under `Shaders/Bernael`, which replace the Unity
asset bundles of 1.6. Shader algorithms, timing, blending and render queues follow
the original ShaderLab effects.

## Building

Requires the .NET 10 SDK. All references come from NuGet (`Krafs.Rimworld.Ref`,
`Lib.Harmony` 3), so no local RimWorld install is needed:

```powershell
dotnet build 1.7/Source/BernaelXenotype17/BernaelXenotype17.csproj -c Release
dotnet build 1.7/Source/BernaelSoul/BernaelSoul.csproj -c Release
dotnet build 1.7/Source/BernaelVanquish/BernaelVanquish.csproj -c Release
```

Each project writes its DLL straight into `1.7/Assemblies`. The GitHub workflow runs
the same three builds and commits the DLLs.

## Validation

Checked on 2026-10-28 against RimWorld **1.7.4990 rev1069**, Harmony **3.0.0-rc.3**,
before the 1.6-only features were merged in:

- All five ShaderDefs: material layouts and seven shader stages compile using
  the game's shader compiler; a deliberately invalid shader is rejected.
- Isolated startup and new game with Harmony and all installed DLC: no additional
  loading errors compared with the dependency-only baseline.
- Rendered game: 19 main Harmony patches, pawn capture, full-pawn ashen shading,
  Mirage, Malicious Seal, Dire Orb flight/burst and an actual Soul Drain cast.
- Save/reload: soul resource, seal, Mirage caster/snapshot, the active Soul Drain
  channel/snapshot and the Dire Orb burst state preserved.

Not yet checked in game: Cursed Fire, Wing Burst, Fold Severance, Gaze of Caligo,
Warlockracy, the Bogeyman and the Soul Feeding Frenzy mental break, which came
over from 1.6 on 2026-11-01. Optional VEF/VPE and Nephilim/EBSG combinations were
not included in any run.
