using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Bernael_Xenotype
{
    public sealed class HediffCompProperties_MaliciousSeal : HediffCompProperties
    {
        // A sealed pawn that dies passes the seal to this many hostile minds around its corpse.
        public float spreadRadius = 4.9f;
        public int maxSpread = 3;
        public SoundDef spreadSound;
        public HediffCompProperties_MaliciousSeal() { compClass = typeof(HediffComp_MaliciousSeal); }
    }

    // Silencing lives in MaliciousSealPatches; this comp remembers who cast the seal and carries it on death.
    public sealed class HediffComp_MaliciousSeal : HediffComp
    {
        public Pawn caster;
        public Faction faction;
        public int startTick = -1;
        private bool spread;

        public HediffCompProperties_MaliciousSeal Props => (HediffCompProperties_MaliciousSeal)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            startTick = Find.TickManager.TicksGame;
            MaliciousSealUtility.Interrupt(Pawn);
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            if (spread) return;
            spread = true;
            MaliciousSealUtility.Spread(Pawn, caster, faction, Props);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref startTick, "startTick", -1);
            Scribe_Values.Look(ref spread, "spread");
        }

        public override string CompDebugString() =>
            $"Sealed by: {caster?.LabelShort ?? "none"} ({faction?.Name ?? "no faction"})";
    }

    public static class MaliciousSealUtility
    {
        // MaliciousSeal.shader's blue.
        public static readonly Color SealColor = new Color(0.51f, 0.71f, 0.86f);
        private static Type vefVerbType;
        private static bool vefResolved;

        public static bool IsSealed(Pawn pawn) =>
            pawn?.health?.hediffSet != null && pawn.health.hediffSet.HasHediff(BernaelDefOf.BX_MaliciousSeal);

        // The seal outlives its caster; hostility follows the faction that cast it.
        public static bool HostileTo(Pawn pawn, Pawn caster, Faction faction) =>
            faction != null ? pawn.HostileTo(faction) : caster != null && pawn.HostileTo(caster);

        public static bool CanSeal(Pawn pawn) => pawn.GetStatValue(StatDefOf.PsychicSensitivity) > 0f;

        // Seals the victim, or restarts the rune's timer if it already carries one.
        public static void Brand(Pawn victim, Pawn caster, Faction faction)
        {
            HediffDef def = BernaelDefOf.BX_MaliciousSeal;
            Hediff seal = victim.health.hediffSet.GetFirstHediffOfDef(def);
            if (seal == null)
            {
                seal = HediffMaker.MakeHediff(def, victim);
                Remember(seal, caster, faction);
                victim.health.AddHediff(seal);
            }
            else
            {
                Remember(seal, caster, faction);
                seal.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
                // The renewed brand stamps down again.
                HediffComp_MaliciousSeal comp = seal.TryGetComp<HediffComp_MaliciousSeal>();
                if (comp != null) comp.startTick = Find.TickManager.TicksGame;
            }
            if (victim.Spawned) victim.Map.GetComponent<MaliciousSealMapComponent>()?.Track(victim);
        }

        private static void Remember(Hediff seal, Pawn caster, Faction faction)
        {
            HediffComp_MaliciousSeal comp = seal.TryGetComp<HediffComp_MaliciousSeal>();
            if (comp == null) return;
            comp.caster = caster;
            comp.faction = faction;
        }

        // Breaks a cast the pawn was already warming up when the seal landed.
        public static void Interrupt(Pawn pawn)
        {
            if (!(pawn.stances?.curStance is Stance_Warmup warmup) || !IsAbilityVerb(warmup.verb)) return;
            pawn.stances.CancelBusyStanceHard();
            if (pawn.Spawned)
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "BX_SealInterrupted".Translate(), SealColor);
        }

        private static bool IsAbilityVerb(Verb verb)
        {
            if (verb is IAbilityVerb) return true;
            if (!vefResolved)
            {
                vefResolved = true;
                vefVerbType = AccessTools.TypeByName("VEF.Abilities.Verb_CastAbility");
            }
            return vefVerbType != null && vefVerbType.IsInstanceOfType(verb);
        }

        // Called from the corpse's hediff, so the pawn is already despawned: use its held position.
        public static void Spread(Pawn dead, Pawn caster, Faction faction, HediffCompProperties_MaliciousSeal props)
        {
            Map map = dead.MapHeld;
            if (map == null || props.maxSpread <= 0) return;
            IntVec3 origin = dead.PositionHeld;
            List<Pawn> victims = map.mapPawns.AllPawnsSpawned
                .Where(p => p != dead && !p.Dead && p.Position.InHorDistOf(origin, props.spreadRadius) &&
                    HostileTo(p, caster, faction) && !IsSealed(p) && CanSeal(p) &&
                    GenSight.LineOfSight(origin, p.Position, map))
                .OrderBy(p => p.Position.DistanceToSquared(origin))
                .Take(props.maxSpread)
                .ToList();
            if (victims.Count == 0) return;
            props.spreadSound?.PlayOneShot(new TargetInfo(origin, map));
            MaliciousSealMapComponent component = map.GetComponent<MaliciousSealMapComponent>();
            foreach (Pawn victim in victims)
            {
                Brand(victim, caster, faction);
                component?.Link(origin, victim);
            }
        }
    }
}
