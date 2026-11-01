# Version update 1.6 -> 1.7: Bernael Xenotype
Game installed: 1.7.4995 rev309 (unstable beta)   Unity: gone (UnityStubs, Waylith)
Harmony: compile NuGet Lib.Harmony 3.0.0-rc.4 (net10.0)   runtime Workshop Harmony 3.0.0.0
Game refs: NuGet Krafs.Rimworld.Ref 1.7.4995-unstable (no local install needed, CI builds)
Framework: net48 -> net10.0   Runtime: CoreCLR (waylithclr)   SDK: 10.0.402
Layout: LoadFolders <v1.7> = Common, 1.7, 1.7/Nephilim (IfModActive Sov.Nephilim)
Branch: 1_7_update (not committed)
1.6 after the update: removed (decided 2026-11-01: unreleased mod, 1.7 only, no freeze tag)
Save surface: no fallbacks for renamed defs (decided: unreleased, no player saves)

## Decisions
- 1.6 is dropped completely: `1.6/` (DLL, AssetBundles, Unity shader project, sources, defs)
  deleted, `About.xml` lists only 1.7. History keeps it (last state: main cf938f0).
- The one 1.7 source tree is `1.7/Source/BernaelXenotype17/` (Font). The shared-source variant
  `1.7/Source/BernaelXenotype.csproj` and every `#if RIMWORLD_1_7` are gone.
- BX_EssenceReservoir (Mortis) and BX_UnstableEssenceReservoir (Font) merged into
  BX_EssenceReservoir: Mortis' label, icon, complexity 5; Font's ability BX_RancorOfSouls,
  geneClass Gene, selectionWeight 0.

## Pass 0 - Repair
- [x] CS0128 in SoulDrainVisuals (merge leftover of pr/16): resolved in the 1.7 copy, 1.6 deleted

## Pass A - One version, 1.7
- [x] 1.6 folder, shared csproj, dead root `Textures/` (identical to Common) deleted
- [x] LoadFolders.xml: only <v1.7>; About.xml: supportedVersions 1.7
- [x] csproj OutputPath -> 1.7/Assemblies for all three projects, no .deps.json
- [x] CI builds the three 1.7 projects and commits `1.7/Assemblies/*.dll`
- [x] .gitignore: 1.6 and Unity entries removed, QA entries moved to BernaelXenotype17
- [x] 1.7/README.md rewritten
- [x] load_folders.py --version 1.7: exit 0
- [x] Versions block: not added (decided: no CLAUDE.md in this repo)
- [x] ShaderLab originals + Dark Mirage README kept as reference in `1.7/Source/ShaderReference/`
      (not loaded, not in the release zip)

## Pass T - Toolchain
- [x] all three csproj on Krafs.Rimworld.Ref 1.7.4995-unstable + Lib.Harmony 3.0.0-rc.4
- [x] Compile items: explicit list kept in BernaelXenotype17 (Unity editor scripts excluded)
- [x] Release build 0 errors, 0 warnings (all three)

## Pass B - Port 1.6 content into 1.7
Three-way merge per file, base 011b577 (the state Font's copy was forked from),
ours = 1.7 tree, theirs = 1.6 HEAD with `#if RIMWORLD_1_7` reduced to its 1.7 branch.
- [x] 50 new files: Cursed Fire, Bogeyman/Kidnap, Gaze of Caligo + incidents, Fold Severance,
      Wing Burst, Warlockracy, Soul Feeding Frenzy, DutyDefs, JobDefs, PawnKinds, Storyteller,
      Gas, ThinkTree patch
- [x] 12 files taken from 1.6 (renames and balance of 5ce8a84), 11 kept from Font (TickInterval
      port, shader fallbacks, Vanquish, homing projectile), 3 merged cleanly
- [x] 6 conflicts resolved: shader files -> Font's 1.7 version; Reaper's Ecstasy -> Font's
      logic (every kill refills, hostile kills stack) with gene BX_SoulEater; BX_Genes.xml ->
      1.6 plus Font's two behaviour descriptions (Withered Soul, Reaper's Ecstasy);
      BX_Xenotypes.xml -> 1.6
- [x] BernaelDefOf restored from 1.6 (Font had removed the 24 fields of the features he lacked)
- [x] BernaelSoul: BX_SoulStarved -> BX_SoulEater
- [x] runtime port grep (GetHashCode, culture string calls, BinaryFormatter, Unity headless,
      AssetBundle, Shader.Find): no hit
- [x] patch_probe: 22 targets, 0 problems; 3 skipped by Prepare (2 VEF, 1 EBSG ResourceGene.Tick,
      which still exists in EBSG 1.7)
- [x] rw-smith validate_mod: 0 errors; find_duplicate_defs: none; vanilla parents and XPath
      targets exist in 1.7 Data; every defName used in Tests/*.rwtest exists
- [ ] game started, Player.log worked top to bottom (needs deploy, user's call)
- [ ] ported features checked in game: Cursed Fire, Wing Burst, Fold Severance, Gaze of Caligo,
      Warlockracy, Bogeyman, Soul Feeding Frenzy, Essence Reservoir -> Rancor of Souls
- [ ] .rwtest suite: rwtest-refresh for 1.7 before the first run

## After every beta patch (until 1.7 is on main)
- [x] 2026-10-28 1.7.4990 rev1069: build ok, 19 patches, log clean (contributors, 1.7/README.md)
- [ ] 2026-11-01 1.7.4995 rev309: build ok, 22 patches probed ok, log open

## Open points
- [x] `XenoIconBernael` texPath pointed to `UI/XenoIconBernael` (broken since 5ce8a84); now
  `UI/BX_Bernael_XenoIcon`, the file in `Common/Textures/UI`. defName unchanged.
- Projectile_SoulLantern is no longer used by any def (Font switched the lantern to
  BernaelSoul's Projectile_HomingSoul); dead code candidate, own change.
- BernaelSoul / BernaelVanquish as separate DLLs: fold into BernaelXenotype later (own change).
- Tests/tools/check-defs.py (git-ignored, local) now reads 1.7; it reports 19 helper classes as
  "not referenced by a def" (its name heuristics), no missing keys or defs.
- Contributors (Font, Mortis, Leo, Marukundaibichi, Slvt_Crvsher) need to know: 1.6 is gone,
  PRs go to `1.7/Source/BernaelXenotype17` and `1.7/Defs`.
- Nephilim (Sov.Nephilim) has no 1.7 release yet; `1.7/Nephilim` patches and the pendulum
  gene stay inactive until it does.
