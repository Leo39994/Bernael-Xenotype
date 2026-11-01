# Shader reference

The ShaderLab originals of the 1.6 effects, kept as reference for the GLSL shaders that
RimWorld 1.7 compiles at load. Nothing here is built or loaded; the 1.6 Unity project, its
build scripts and the asset bundles were removed with 1.6 (last state: commit cf938f0).

| ShaderLab original | 1.7 ShaderDef | GLSL source under `1.7/Shaders/Bernael/` |
| --- | --- | --- |
| `MaliciousSeal.shader` | `BX_MaliciousSeal` | `BX_Effect.vert.glsl`, `BX_MaliciousSeal.frag.glsl` |
| `DireOrb.shader` | `BX_DireOrb` | `BX_Effect.vert.glsl`, `BX_DireOrb.frag.glsl` |
| `DarkMirage.shader` | `BX_DarkMirage` | `BX_Effect.vert.glsl`, `BX_DarkMirage.frag.glsl` |
| `SoulDrain.shader` | `BX_SoulDrain` | `BX_SoulDrain.vert.glsl`, `BX_SoulDrain.frag.glsl` |
| `SoulDrainAshen.shader` | `BX_SoulDrainAshen` | `BX_Effect.vert.glsl`, `BX_SoulDrainAshen.frag.glsl` |

A change to an effect goes into the GLSL file; the ShaderDefs are in
`1.7/Defs/ShaderDefs/BX_Shaders.xml`, the smoke texture of Soul Drain is
`1.7/Textures/Effects/BX_SoulDrainSmoke_Uncompressed.png`.

## Dark Mirage

A Royalty psycast that creates a stationary decoy using a snapshot of the caster's appearance. The phantom has a dark outline, glowing eyes and rising blue-black flames. All included text is English.

### In-game use

Enable Royalty and the mod's existing dependencies, then restart RimWorld. In developer mode, select **Grant Dark Mirage (click pawn)** from the debug actions menu and click a player-controlled humanlike pawn. This grants the ability, raises the psylink to at least level 2 and fills psyfocus. Draft the pawn and cast **dark mirage** on an open cell.

### Mechanics

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

### Rendering

The vanilla pawn cache renderer captures the caster into a transparent 512-by-512 texture, including supported hair, clothing and headgear. The snapshot and full-precision eye anchors are saved with the decoy. Changing the caster's equipment does not change an existing mirage.

A distance texture is generated once per snapshot. The shader keeps the silhouette, thick outline and close glow stable while transporting translucent flame density upward. A connected flame bed supports wider tongues without isolated gaps. All fire originates outside the pawn silhouette. Animation uses game ticks and stops when paused.

On summoning, a noisy dissolve reveals the decoy from bottom to top over 0.85 seconds. Its eyes brighten from 0.85 to 1.15 seconds, then the flames rise from the base and reach full height at 1.95 seconds. The reveal fits the actual captured silhouette and uses elapsed summon time, independently of the random flame phase. Loading a save resumes that sequence rather than replaying it. Expiry retains the original 36-tick (0.6-second) fade-out.

Each decoy releases its own textures and material when removed. Rendering does not modify the caster's materials or require full-screen post-processing. The capture covers three map cells; unusually large attachments and third-party renderers that bypass the vanilla cache may need additional support.

The layered atmosphere was informed by the Killing Instinct aura in MRK's assorted little gadgets. This implementation uses an independent shader and does not copy or depend on that module's code, assets or framework.
