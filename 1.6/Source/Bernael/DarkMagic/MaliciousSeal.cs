using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Bernael_Xenotype
{
    public sealed class CompProperties_MaliciousSeal : CompProperties_AbilityEffect
    {
        // Radius comes from the ability's Ability_EffectRadius, which vanilla also previews.
        // Ticks from casting until the seal starts to close; its brand applies the BX_MaliciousSeal hediff.
        public int durationTicks = 108;
        public SoundDef inscribeSound;
        public SoundDef brandSound;
        public CompProperties_MaliciousSeal() { compClass = typeof(CompAbilityEffect_MaliciousSeal); }
    }

    public sealed class CompAbilityEffect_MaliciousSeal : CompAbilityEffect
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!DarkMagicAssets.Ready())
            {
                if (throwMessages) Messages.Message("BX_DarkMagicShaderMissing".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            Map map = parent.pawn.Map;
            bool valid = map != null && target.Cell.InBounds(map) && target.Cell.Walkable(map);
            if (!valid && throwMessages)
                Messages.Message("BX_SealBlocked".Translate(), MessageTypeDefOf.RejectInput, false);
            return valid;
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Valid(target);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!Valid(target, true)) return;
            base.Apply(target, dest);
            parent.pawn.Map.GetComponent<MaliciousSealMapComponent>().Inscribe(parent.pawn, target.Cell, parent.def);
        }
    }

    public sealed class SealTether : IExposable
    {
        public Pawn victim;
        public int boundTick;
        public int releasedTick = -1;
        public bool Active => releasedTick < 0;

        public void ExposeData()
        {
            Scribe_References.Look(ref victim, "victim");
            Scribe_Values.Look(ref boundTick, "boundTick");
            Scribe_Values.Look(ref releasedTick, "releasedTick", -1);
        }
    }

    // A chain the seal throws from a sealed corpse to the pawn it spreads to. Visual only, so not saved.
    public sealed class SealLink
    {
        public Vector3 origin;
        public Pawn victim;
        public int startTick;
        public float seed;
    }

    public sealed class MaliciousSeal : IExposable
    {
        public Pawn caster;
        public Faction faction;
        public AbilityDef def;
        public IntVec3 cell;
        public int startTick;
        public float seed;
        public bool branded;
        public List<SealTether> tethers = new List<SealTether>();
        private CompProperties_MaliciousSeal settings;

        public CompProperties_MaliciousSeal Settings =>
            settings ?? (settings = def?.comps.OfType<CompProperties_MaliciousSeal>().FirstOrDefault());
        public int CloseTick => startTick + Settings.durationTicks;
        public float Radius => def.EffectRadius;

        public bool HostileTo(Pawn pawn) => MaliciousSealUtility.HostileTo(pawn, caster, faction);

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Values.Look(ref branded, "branded");
            Scribe_Collections.Look(ref tethers, "tethers", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (tethers == null) tethers = new List<SealTether>();
                tethers.RemoveAll(t => t?.victim == null);
            }
        }
    }

    // Seals save their timeline and tethers. Sealed pawns are found again from their hediff, so only
    // the ground seals need saving. Shader resources are shared.
    public sealed class MaliciousSealMapComponent : MapComponent
    {
        // Authored against MaliciousSeal.shader: inscription, then binding, then a 0.66 s close in which the
        // daggers strike the skull at StrikeTicks and the chains carry the brand to the victims by BrandTicks.
        private const int InscribeTicks = 66;
        private const int BindStartTicks = 72;
        private const int CloseTicks = 40;
        private const int StrikeTicks = 12;
        private const int BrandTicks = 26;
        private const int TetherGrowTicks = 21;
        private const int ReleaseTicks = 15;
        private const int ScanInterval = 10;
        private const int MaxTethers = 12;
        private const int TrackInterval = 30;
        // The shader's Extent (how far the seal's quad reaches, in ring radii) and ChainWidth.
        private const float SealExtent = 1.6f;
        private const float ChainWidth = 0.8f;
        // The brand over a sealed pawn's head stamps down over StampTicks, landing at StampLanding of them,
        // and fades out over the seal's last FadeTicks.
        private const float BrandSize = 1.5f;
        private const float BrandHeight = 1.2f;
        private const int StampTicks = 24;
        private const float StampLanding = 0.55f;
        private const int FadeTicks = 60;
        // The brand's stab (1 s) and bob (2 s) both divide a minute, so its phase wraps without a jump.
        private const int LoopTicks = 3600;
        private const int LinkGrowTicks = 14;
        private const int LinkTicks = 50;
        private List<MaliciousSeal> seals = new List<MaliciousSeal>();
        private readonly List<Pawn> sealedPawns = new List<Pawn>();
        private readonly List<SealLink> links = new List<SealLink>();
        private MaterialPropertyBlock properties;
        public MaliciousSealMapComponent(Map map) : base(map) { }

        public void Inscribe(Pawn caster, IntVec3 cell, AbilityDef def)
        {
            int now = Find.TickManager.TicksGame;
            var seal = new MaliciousSeal {
                caster = caster, faction = caster.Faction, def = def, cell = cell, startTick = now,
                seed = caster.thingIDNumber % 251 * 0.17f + now % 37 * 0.11f
            };
            if (seal.Settings == null) return;
            seals.Add(seal);
            seal.Settings.inscribeSound?.PlayOneShot(new TargetInfo(cell, map));
        }

        public void Track(Pawn pawn)
        {
            if (!sealedPawns.Contains(pawn)) sealedPawns.Add(pawn);
        }

        public void Link(IntVec3 origin, Pawn victim)
        {
            int now = Find.TickManager.TicksGame;
            links.Add(new SealLink {
                origin = origin.ToVector3Shifted(), victim = victim, startTick = now,
                seed = victim.thingIDNumber % 13 * 0.7f + now % 17 * 0.13f
            });
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % TrackInterval == 0) FindSealedPawns();
            links.RemoveAll(l => now - l.startTick >= LinkTicks);
            for (int i = seals.Count - 1; i >= 0; i--)
            {
                MaliciousSeal seal = seals[i];
                if (seal.Settings == null) { seals.RemoveAt(i); continue; }
                int age = now - seal.startTick;
                int closeTick = seal.CloseTick;
                if (!seal.branded && now >= closeTick + BrandTicks) Brand(seal);
                else if (age >= BindStartTicks && now < closeTick && age % ScanInterval == 0) Bind(seal, now);
                seal.tethers.RemoveAll(t => !t.Active && now - t.releasedTick >= ReleaseTicks);
                if (now >= closeTick + CloseTicks) seals.RemoveAt(i);
            }
        }

        // A tethered victim is marked: running out of the circle or falling down does not save it.
        private void Bind(MaliciousSeal seal, int now)
        {
            int active = 0;
            foreach (SealTether tether in seal.tethers)
            {
                if (!tether.Active) continue;
                Pawn victim = tether.victim;
                if (victim.Dead || !victim.Spawned || victim.Map != map)
                {
                    tether.releasedTick = now;
                    continue;
                }
                active++;
            }
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count && active < MaxTethers; i++)
            {
                Pawn victim = pawns[i];
                if (victim.Dead || !victim.Position.InHorDistOf(seal.cell, seal.Radius) ||
                    !seal.HostileTo(victim) || !MaliciousSealUtility.CanSeal(victim) ||
                    seal.tethers.Any(t => t.Active && t.victim == victim) ||
                    !GenSight.LineOfSight(seal.cell, victim.Position, map)) continue;
                seal.tethers.Add(new SealTether { victim = victim, boundTick = now });
                active++;
            }
        }

        private void Brand(MaliciousSeal seal)
        {
            seal.branded = true;
            seal.Settings.brandSound?.PlayOneShot(new TargetInfo(seal.cell, map));
            foreach (SealTether tether in seal.tethers)
            {
                Pawn victim = tether.victim;
                if (!tether.Active || victim.Dead || !victim.Spawned || victim.Map != map) continue;
                MaliciousSealUtility.Brand(victim, seal.caster, seal.faction);
            }
        }

        private void FindSealedPawns()
        {
            sealedPawns.Clear();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (MaliciousSealUtility.IsSealed(pawns[i])) sealedPawns.Add(pawns[i]);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref seals, "maliciousSeals", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (seals == null) seals = new List<MaliciousSeal>();
                seals.RemoveAll(s => s?.Settings == null);
            }
        }

        public override void FinalizeInit() => FindSealedPawns();

        public override void MapRemoved()
        {
            seals.Clear();
            sealedPawns.Clear();
            links.Clear();
        }

        public override void MapComponentDraw()
        {
            if (map != Find.CurrentMap || seals.Count + sealedPawns.Count + links.Count == 0 ||
                !DarkMagicAssets.Ready()) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            int now = Find.TickManager.TicksGame;
            float ground = AltitudeLayer.MoteLow.AltitudeFor();
            // Chains lie under the seal, so they run out from beneath its skull and ring.
            float chains = ground - Altitudes.AltInc * 0.3f;
            DrawBrands(now);
            DrawLinks(now, chains);
            foreach (MaliciousSeal seal in seals)
            {
                float phase = (now - seal.startTick) / 60f;
                int closing = now - seal.CloseTick;
                // A chain's pulse leaves the skull as the daggers strike it and reaches the victim halfway through
                // its release, as the brand lands.
                float release = Mathf.Clamp01((closing - StrikeTicks) / (float)(2 * (BrandTicks - StrikeTicks)));
                Vector3 center = seal.cell.ToVector3Shifted();
                if (!seal.cell.Fogged(map))
                {
                    float size = seal.Radius * 2f * SealExtent;
                    center.y = ground;
                    Set(phase, seal.seed, 0, Mathf.Clamp01((now - seal.startTick) / (float)InscribeTicks),
                        Mathf.Clamp01(closing / (float)CloseTicks), 1f, 0f, size);
                    DarkMagicAssets.Draw(DarkMagicAssets.Seal, center, Quaternion.identity, Vector3.one * size, properties);
                }
                center.y = chains;
                foreach (SealTether tether in seal.tethers)
                {
                    Pawn victim = tether.victim;
                    if (!victim.Spawned || victim.Map != map || victim.Position.Fogged(map)) continue;
                    float opacity = tether.Active ? 1f : 1f - Mathf.Clamp01((now - tether.releasedTick) / (float)ReleaseTicks);
                    DrawChain(center, victim, phase, seal.seed + victim.thingIDNumber % 13 * 0.7f,
                        Mathf.Clamp01((now - tether.boundTick) / (float)TetherGrowTicks), release, opacity);
                }
            }
        }

        // The seal branded over each sealed pawn's head.
        private void DrawBrands(int now)
        {
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor();
            foreach (Pawn pawn in sealedPawns)
            {
                if (pawn.Dead || !pawn.Spawned || pawn.Map != map || pawn.Position.Fogged(map)) continue;
                Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(BernaelDefOf.BX_MaliciousSeal);
                if (hediff == null) continue;
                int start = hediff.TryGetComp<HediffComp_MaliciousSeal>()?.startTick ?? -1;
                int age = start < 0 ? now : now - start;
                int left = hediff.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear ?? FadeTicks;
                float stamp = start < 0 ? 1f : Mathf.Clamp01(age / (float)StampTicks);
                float fade = 1f - Mathf.Clamp01(left / (float)FadeTicks);
                float scale = Mathf.Sqrt(Mathf.Clamp(pawn.BodySize, 0.5f, 4f));
                float size = BrandSize * scale * StampScale(stamp) * (1f - 0.15f * fade);
                Vector3 at = pawn.DrawPos + new Vector3(0f, 0f, BrandHeight * scale);
                at.y = overhead;
                Set(age % LoopTicks / 60f, 0f, 2, stamp, fade, 1f, 0f, size);
                DarkMagicAssets.Draw(DarkMagicAssets.Seal, at, Quaternion.identity, Vector3.one * size, properties);
            }
        }

        // The brand slams down from nearly twice its size, lands at StampLanding and bounces once.
        private static float StampScale(float stamp) => stamp < StampLanding
            ? 1f + 0.9f * Mathf.Pow(1f - stamp / StampLanding, 2f)
            : 1f - 0.06f * Mathf.Sin(Mathf.PI * (stamp - StampLanding) / (1f - StampLanding));

        private void DrawLinks(int now, float altitude)
        {
            foreach (SealLink link in links)
            {
                Pawn victim = link.victim;
                if (victim == null || !victim.Spawned || victim.Map != map || victim.Position.Fogged(map)) continue;
                int age = now - link.startTick;
                Vector3 from = link.origin;
                from.y = altitude;
                DrawChain(from, victim, age / 60f, link.seed, Mathf.Clamp01(age / (float)LinkGrowTicks),
                    Mathf.Clamp01((age - LinkGrowTicks) / (float)(LinkTicks - LinkGrowTicks)), 1f);
            }
        }

        private void DrawChain(Vector3 from, Pawn victim, float phase, float seed, float grow, float release, float opacity)
        {
            Vector3 feet = victim.DrawPos;
            feet.y = from.y;
            Vector3 delta = feet - from;
            float length = delta.MagnitudeHorizontal();
            if (length < 0.3f) return;
            Set(phase, seed, 1, grow, release, opacity, length, length);
            DarkMagicAssets.Draw(DarkMagicAssets.Seal, from + delta * 0.5f, DarkMagicAssets.Along(delta),
                new Vector3(length, 1f, ChainWidth), properties);
        }

        private void Set(float phase, float seed, float mode, float inscribe, float close, float opacity, float length,
            float size)
        {
            properties.Clear();
            properties.SetFloat("_Phase", phase);
            properties.SetFloat("_Seed", seed);
            properties.SetFloat("_Mode", mode);
            properties.SetFloat("_Inscribe", inscribe);
            properties.SetFloat("_Close", close);
            properties.SetFloat("_Opacity", opacity);
            properties.SetFloat("_Length", length);
            properties.SetFloat("_Size", size);
        }
    }
}
