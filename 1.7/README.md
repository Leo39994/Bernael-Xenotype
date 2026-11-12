# RimWorld 1.7

Bernael Xenotype supports RimWorld 1.7. Version-specific definitions, patches,
assemblies and shaders live in `1.7/`. Textures, sounds and translations shared
across versions live in `Common/`.

## Building

Install the .NET 10 SDK, then run from the repository root:

```powershell
dotnet build 1.7/Source/BernaelXenotype.slnx -c Release
```

The solution builds `BernaelXenotype`, `BernaelSoul` and `BernaelVanquish` into
`1.7/Assemblies/`. Game references and Harmony come from NuGet, so building does
not require a local RimWorld installation.

## Rendering

The effects use native ShaderDefs and GLSL sources in `1.7/Shaders/Bernael/`.
Changes belong in those GLSL files and `1.7/Defs/ShaderDefs/BX_Shaders.xml`.
Imported Piloto Studio texture sources and transformations are listed in
`Common/Textures/Effects/Piloto/SOURCES.json`.

Spatial noise uses the integer lattice hash in `BX_ShaderCommon.glsl`.
Texture samples that need automatic mip selection must execute outside
divergent branches.

## Gameplay tests

The `Tests/` tree contains gameplay and visual scenarios in `.rwtest` format.
They are maintained alongside the corresponding abilities and effects.
The release archive contains only game files; source and test files stay in
the repository.

## Design

[Bernael xenotype](https://docs.google.com/document/d/1BgKwyOawYDHXorZIPq1jJHHtnLrTC3mw_e1Bu71AsiE/edit?tab=t.0)
contains the shared design. Last checked on 2026-11-12 (Asia/Shanghai); the
observed modification time was 2026-10-31T11:08:12.280Z.
