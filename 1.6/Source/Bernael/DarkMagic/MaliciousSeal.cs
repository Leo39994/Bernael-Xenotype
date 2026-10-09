using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Bernael_Xenotype
{
    public sealed class CompProperties_MaliciousSeal : CompProperties_AbilityEffect
    {
        // Played as the brand lands on the target; it applies the BX_MaliciousSeal hediff at once.
        public SoundDef brandSound;
        public CompProperties_MaliciousSeal() { compClass = typeof(CompAbilityEffect_MaliciousSeal); }
    }

    // Brands the targeted pawn. With psychic set on the props, Psycast already turns away targets without psychic
    // sensitivity and shows each target's sensitivity while aiming.
    public sealed class CompAbilityEffect_MaliciousSeal : CompAbilityEffect
    {
        public new CompProperties_MaliciousSeal Props => (CompProperties_MaliciousSeal)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!DarkMagicAssets.Ready())
            {
                if (throwMessages) Messages.Message("BX_DarkMagicShaderMissing".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            Pawn pawn = target.Pawn;
            return pawn != null && pawn != parent.pawn && MaliciousSealUtility.CanSeal(pawn) && base.Valid(target, throwMessages);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Valid(target);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!Valid(target, true)) return;
            base.Apply(target, dest);
            Pawn victim = target.Pawn;
            MaliciousSealUtility.Brand(victim, parent.pawn, parent.pawn.Faction);
            Props.brandSound?.PlayOneShot(new TargetInfo(victim.Position, victim.Map));
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

    // Draws the brand over each sealed pawn and the chains of a seal spreading from a corpse. Sealed pawns are found
    // again from their hediff, so nothing here is saved. Shader resources are shared.
    public sealed class MaliciousSealMapComponent : MapComponent
    {
        // Authored against MaliciousSeal.shader. The brand floats over a sealed pawn's head, clear of the pawn: its
        // quad reaches the shader's Extent, 1.75 ring radii, so its ring's radius is 0.27 cells and its lowest spike
        // ends 0.36 cells below its middle. It stamps down over StampTicks, landing at StampLanding of them, and fades
        // out over the seal's last FadeTicks.
        private const float BrandSize = 0.95f;
        private const float BrandHeight = 1.1f;
        private const int StampTicks = 24;
        private const float StampLanding = 0.55f;
        private const int FadeTicks = 60;
        // The brand's stab (1 s) and bob (2 s) both divide a minute, so its phase wraps without a jump.
        private const int LoopTicks = 3600;
        private const int LinkGrowTicks = 14;
        private const int LinkTicks = 50;
        private const int TrackInterval = 30;
        // The shader's ChainWidth.
        private const float ChainWidth = 0.8f;
        private readonly List<Pawn> sealedPawns = new List<Pawn>();
        private readonly List<SealLink> links = new List<SealLink>();
        private MaterialPropertyBlock properties;
        public MaliciousSealMapComponent(Map map) : base(map) { }

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
        }

        private void FindSealedPawns()
        {
            sealedPawns.Clear();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (MaliciousSealUtility.IsSealed(pawns[i])) sealedPawns.Add(pawns[i]);
        }

        public override void FinalizeInit() => FindSealedPawns();

        public override void MapRemoved()
        {
            sealedPawns.Clear();
            links.Clear();
        }

        public override void MapComponentDraw()
        {
            if (map != Find.CurrentMap || sealedPawns.Count + links.Count == 0 || !DarkMagicAssets.Ready()) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            int now = Find.TickManager.TicksGame;
            DrawBrands(now);
            DrawLinks(now, AltitudeLayer.MoteLow.AltitudeFor());
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
                Vector3 feet = victim.DrawPos;
                feet.y = altitude;
                Vector3 delta = feet - from;
                float length = delta.MagnitudeHorizontal();
                if (length < 0.3f) continue;
                Set(age / 60f, link.seed, 1, Mathf.Clamp01(age / (float)LinkGrowTicks),
                    Mathf.Clamp01((age - LinkGrowTicks) / (float)(LinkTicks - LinkGrowTicks)), 1f, length, length);
                DarkMagicAssets.Draw(DarkMagicAssets.Seal, from + delta * 0.5f, DarkMagicAssets.Along(delta),
                    new Vector3(length, 1f, ChainWidth), properties);
            }
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
