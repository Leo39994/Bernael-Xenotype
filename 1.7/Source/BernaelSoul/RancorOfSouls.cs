using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
    [DefOf]
    public static class BernaelSoulDefOf
    {
        public static GeneDef BX_SoulEater;

        static BernaelSoulDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BernaelSoulDefOf));
        }
    }

    public class CompProperties_RancorOfSouls : CompProperties_AbilityEffect
    {
        public ThingDef projectileDef;
        public float resourcePerProjectile = 0.05f;
        public int maximumProjectiles = 20;
        public float searchRadius = 29.5f;
        public int ticksBetweenProjectiles = 2;

        public CompProperties_RancorOfSouls()
        {
            compClass = typeof(CompAbilityEffect_RancorOfSouls);
        }
    }

    public class Ability_RancorOfSouls : Ability
    {
        public Ability_RancorOfSouls()
        {
        }

        public Ability_RancorOfSouls(Pawn pawn)
            : base(pawn)
        {
        }

        public Ability_RancorOfSouls(Pawn pawn, AbilityDef def)
            : base(pawn, def)
        {
        }

        public Ability_RancorOfSouls(Pawn pawn, Precept sourcePrecept, AbilityDef def)
            : base(pawn, sourcePrecept, def)
        {
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            CompAbilityEffect_RancorOfSouls effect = CompOfType<CompAbilityEffect_RancorOfSouls>();
            string reason = null;
            if (effect == null || !effect.CanActivate(out reason, false))
            {
                if (!reason.NullOrEmpty())
                {
                    Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            return base.Activate(target, dest);
        }
    }

    public class CompAbilityEffect_RancorOfSouls : CompAbilityEffect
    {
        public new CompProperties_RancorOfSouls Props => (CompProperties_RancorOfSouls)props;

        public bool CanActivate(out string reason, bool useTargetCache)
        {
            if (!TryGetResource(out Gene_Resource resource) || ProjectileCount(resource) < 1)
            {
                reason = "BX_RancorRequiresSoul".Translate(parent.pawn);
                return false;
            }

            if (!HasTarget(useTargetCache))
            {
                reason = "BX_RancorNoTargets".Translate();
                return false;
            }

            reason = null;
            return true;
        }

        public override bool GizmoDisabled(out string reason)
        {
            return !CanActivate(out reason, true);
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return false;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (!TryGetResource(out Gene_Resource resource))
            {
                parent.ResetCooldown();
                return;
            }

            int projectileCount = ProjectileCount(resource);
            List<Pawn> targets = GatherTargets();
            if (projectileCount < 1 || targets.Count == 0)
            {
                parent.ResetCooldown();
                return;
            }

            resource.Value = 0f;
            MapComponent_RancorVolley volleyComponent = parent.pawn.Map?.GetComponent<MapComponent_RancorVolley>();
            if (volleyComponent != null)
            {
                volleyComponent.QueueVolley(parent.pawn, Props.projectileDef, projectileCount,
                    Props.searchRadius, Props.ticksBetweenProjectiles, targets[0].Position);
            }
            else
            {
                for (int index = 0; index < projectileCount; index++)
                {
                    LaunchProjectile(parent.pawn, Props.projectileDef,
                        new LocalTargetInfo(targets[index % targets.Count]));
                }
            }

            cachedTargetTick = -1;
        }

        private bool TryGetResource(out Gene_Resource resource)
        {
            Gene gene = parent.pawn.genes?.GetGene(BernaelSoulDefOf.BX_SoulEater);
            resource = gene as Gene_Resource;
            return resource != null && gene.Active;
        }

        // The souls a cast would release now; the warmup's vortex shows this many.
        internal int PendingProjectiles() => TryGetResource(out Gene_Resource resource) ? ProjectileCount(resource) : 0;

        private int ProjectileCount(Gene_Resource resource)
        {
            if (Props.resourcePerProjectile <= 0f)
            {
                return 0;
            }

            int count = Mathf.FloorToInt((resource.ValuePercent + 0.0001f) / Props.resourcePerProjectile);
            return Mathf.Clamp(count, 0, Props.maximumProjectiles);
        }

        private bool HasTarget(bool useCache)
        {
            int currentTick = Find.TickManager.TicksGame;
            if (useCache && cachedTargetTick >= 0 && currentTick - cachedTargetTick < 30)
            {
                return cachedHasTarget;
            }

            cachedHasTarget = GatherTargets(true).Count != 0;
            cachedTargetTick = currentTick;
            return cachedHasTarget;
        }

        private List<Pawn> GatherTargets(bool stopAfterFirst = false)
        {
            List<Pawn> targets = new List<Pawn>();
            Pawn caster = parent.pawn;
            Map map = caster.Map;
            IAttackTargetSearcher searcher = caster;
            if (map == null || !caster.Spawned)
            {
                return targets;
            }

            List<IAttackTarget> potentialTargets = map.attackTargetsCache.GetPotentialTargetsFor(searcher);
            float radiusSquared = Props.searchRadius * Props.searchRadius;
            for (int index = 0; index < potentialTargets.Count; index++)
            {
                Pawn candidate = potentialTargets[index].Thing as Pawn;
                if (!ValidTarget(candidate, caster, map, radiusSquared))
                {
                    continue;
                }

                targets.Add(candidate);
                if (stopAfterFirst)
                {
                    return targets;
                }
            }

            targets.Sort((left, right) =>
                left.Position.DistanceToSquared(caster.Position).CompareTo(right.Position.DistanceToSquared(caster.Position)));
            return targets;
        }

        private static bool ValidTarget(Pawn candidate, Pawn caster, Map map, float radiusSquared)
        {
            if (candidate == null || candidate.Dead || candidate.Downed || !candidate.Spawned || candidate.Map != map)
            {
                return false;
            }

            if (!caster.HostileTo(candidate) || candidate.Position.DistanceToSquared(caster.Position) > radiusSquared)
            {
                return false;
            }

            return GenSight.LineOfSight(caster.Position, candidate.Position, map);
        }

        internal static void LaunchProjectile(Pawn caster, ThingDef projectileDef, LocalTargetInfo target,
            Vector3? burstCenter = null, float? burstAngle = null)
        {
            if (caster == null || projectileDef == null || !target.IsValid || caster.Map == null)
            {
                return;
            }

            Projectile projectile = ThingMaker.MakeThing(projectileDef) as Projectile;
            if (projectile == null)
            {
                return;
            }

            Vector3 origin = burstCenter ?? caster.DrawPos;
            GenSpawn.Spawn(projectile, origin.ToIntVec3(), caster.Map);
            projectile.Launch(caster, origin, target, target, ProjectileHitFlags.IntendedTarget, true);
            if (burstAngle.HasValue && projectile is Projectile_HomingSoul soul)
                soul.BeginRancorBurst(burstAngle.Value);
        }

        private int cachedTargetTick = -1;
        private bool cachedHasTarget;
    }

    public class MapComponent_RancorVolley : MapComponent
    {
        public const int GatherTicks = 14;
        // The release burst's spirit smoke takes this long to clear.
        public const int BurstVisualTicks = 84;
        private const int ImpactVisualTicks = 24;
        public MapComponent_RancorVolley(Map map)
            : base(map)
        {
        }

        // A soul bursting on whatever it struck. Visual only, so it is not saved.
        public void Notify_SoulImpact(Vector3 at, int seed = 0)
        {
            impacts.Add(new SoulImpact { at = at, tick = Find.TickManager.TicksGame,
                angle = Mathf.Repeat(seed * 137.50776f, 360f) * Mathf.Deg2Rad });
        }

        private struct SoulImpact
        {
            public Vector3 at;
            public int tick;
            public float angle;
        }

        private readonly List<SoulImpact> impacts = new List<SoulImpact>();
        private static AbilityDef rancorDef;

        public void QueueVolley(Pawn caster, ThingDef projectileDef, int projectileCount,
            float searchRadius, int ticksBetweenProjectiles, IntVec3 fallbackTargetCell)
        {
            if (caster == null || projectileDef == null || projectileCount < 1)
            {
                return;
            }

            RancorVolley volley = new RancorVolley
            {
                caster = caster,
                projectileDef = projectileDef,
                shotsRemaining = projectileCount,
                searchRadius = searchRadius,
                ticksBetweenProjectiles = Mathf.Max(1, ticksBetweenProjectiles),
                nextLaunchTick = Find.TickManager.TicksGame + GatherTicks,
                fallbackTargetCell = fallbackTargetCell,
                radialBurst = true,
                burstCenter = caster.DrawPos,
                burstCount = projectileCount,
                burstAngle = caster.thingIDNumber % 31 * 11.61f
            };

            volleys.Add(volley);
        }

        public override void MapComponentTick()
        {
            // Age impacts even when this map is not being drawn. Visual variation must not consume gameplay RNG.
            int currentTick = Find.TickManager.TicksGame;
            for (int i = impacts.Count - 1; i >= 0; i--)
                if (currentTick - impacts[i].tick > ImpactVisualTicks || currentTick < impacts[i].tick)
                    impacts.RemoveAt(i);

            if (volleys.Count == 0)
            {
                return;
            }

            for (int index = volleys.Count - 1; index >= 0; index--)
            {
                RancorVolley volley = volleys[index];
                if (volley != null && volley.radialBurst && volley.shotsRemaining == 0)
                {
                    if (currentTick - volley.nextLaunchTick > BurstVisualTicks) volleys.RemoveAt(index);
                    continue;
                }
                if (volley == null || !VolleyCanContinue(volley))
                {
                    volleys.RemoveAt(index);
                    continue;
                }

                if (currentTick < volley.nextLaunchTick)
                {
                    if (volley.radialBurst) volley.burstCenter = volley.caster.DrawPos;
                    continue;
                }

                if (volley.radialBurst)
                {
                    // All souls leave in the same tick. The initial directions cover a full circle,
                    // regardless of where the enemies stand; each soul then bends toward its target.
                    tmpTargets.Clear();
                    GatherTargets(volley.caster, volley.searchRadius, tmpTargets);
                    int count = volley.shotsRemaining;
                    for (int shot = 0; shot < count; shot++)
                    {
                        LocalTargetInfo target = tmpTargets.Count > 0
                            ? new LocalTargetInfo(tmpTargets[shot % tmpTargets.Count])
                            : new LocalTargetInfo(volley.fallbackTargetCell);
                        CompAbilityEffect_RancorOfSouls.LaunchProjectile(volley.caster, volley.projectileDef,
                            target, volley.burstCenter, volley.burstAngle + 360f * shot / count);
                    }
                    volley.shotsRemaining = 0;
                    continue;
                }

                // Finish volleys already in progress in saves made before the radial burst update.
                LaunchNext(volley);
                if (volley.shotsRemaining <= 0)
                {
                    volleys.RemoveAt(index);
                }
            }
        }

        public override void MapComponentDraw()
        {
            if (map != Find.CurrentMap || !RancorVisuals.Ready()) return;
            int now = Find.TickManager.TicksGame;
            DrawWarmups(now);
            float warmupSeconds = rancorDef?.verbProperties?.warmupTime ?? 1f;
            foreach (RancorVolley volley in volleys)
            {
                if (!volley.radialBurst || volley.burstCenter.ToIntVec3().Fogged(map)) continue;
                RancorVisuals.DrawBurst(volley.burstCenter, (now - volley.nextLaunchTick) / 60f,
                    volley.burstAngle, volley.burstCount, volley.caster?.thingIDNumber ?? 0, warmupSeconds);
            }
            for (int i = impacts.Count - 1; i >= 0; i--)
            {
                SoulImpact impact = impacts[i];
                if (!impact.at.ToIntVec3().Fogged(map))
                    RancorVisuals.DrawImpact(impact.at, (now - impact.tick) / 60f, impact.angle);
            }
        }

        // While a caster warms up Rancor of Souls, the reservoir stirs inside it. Read from the stance itself, so a
        // warmup restored from a save keeps its charge without separate bookkeeping.
        private void DrawWarmups(int now)
        {
            rancorDef ??= DefDatabase<AbilityDef>.GetNamedSilentFail("BX_RancorOfSouls");
            if (rancorDef == null) return;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!(pawn.stances?.curStance is Stance_Warmup warmup) || !(warmup.verb is Verb_CastAbility cast) ||
                    cast.Ability?.def != rancorDef || pawn.Position.Fogged(map)) continue;
                int elapsed = Mathf.Max(0, now - warmup.startedTick);
                float total = Mathf.Max(1, elapsed + warmup.ticksLeft);
                int souls = cast.Ability.CompOfType<CompAbilityEffect_RancorOfSouls>()?.PendingProjectiles() ?? 0;
                RancorVisuals.DrawGather(pawn.DrawPos, elapsed / 60f, elapsed / total, pawn.thingIDNumber, souls);
            }
        }

        public override void MapRemoved()
        {
            volleys.Clear();
            impacts.Clear();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref volleys, "rancorVolleys", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && volleys == null)
            {
                volleys = new List<RancorVolley>();
            }
        }

        private void LaunchNext(RancorVolley volley)
        {
            tmpTargets.Clear();
            GatherTargets(volley.caster, volley.searchRadius, tmpTargets);
            LocalTargetInfo target = tmpTargets.Count > 0
                ? new LocalTargetInfo(tmpTargets[volley.targetIndex % tmpTargets.Count])
                : new LocalTargetInfo(volley.fallbackTargetCell);
            volley.targetIndex++;
            CompAbilityEffect_RancorOfSouls.LaunchProjectile(volley.caster, volley.projectileDef, target);
            volley.shotsRemaining--;
            volley.nextLaunchTick = Find.TickManager.TicksGame + volley.ticksBetweenProjectiles;
        }

        private static bool VolleyCanContinue(RancorVolley volley)
        {
            Pawn caster = volley.caster;
            return volley.shotsRemaining > 0 && caster != null && !caster.Dead && caster.Spawned &&
                caster.Map != null;
        }

        private static void GatherTargets(Pawn caster, float searchRadius, List<Pawn> targets)
        {
            Map map = caster.Map;
            IAttackTargetSearcher searcher = caster;
            if (map == null || !caster.Spawned)
            {
                return;
            }

            List<IAttackTarget> potentialTargets = map.attackTargetsCache.GetPotentialTargetsFor(searcher);
            float radiusSquared = searchRadius * searchRadius;
            for (int index = 0; index < potentialTargets.Count; index++)
            {
                Pawn candidate = potentialTargets[index].Thing as Pawn;
                if (candidate == null || candidate.Dead || candidate.Downed || !candidate.Spawned ||
                    candidate.Map != map || !caster.HostileTo(candidate) ||
                    candidate.Position.DistanceToSquared(caster.Position) > radiusSquared ||
                    !GenSight.LineOfSight(caster.Position, candidate.Position, map))
                {
                    continue;
                }

                targets.Add(candidate);
            }

            targets.Sort((left, right) =>
                left.Position.DistanceToSquared(caster.Position).CompareTo(
                    right.Position.DistanceToSquared(caster.Position)));
        }

        private List<RancorVolley> volleys = new List<RancorVolley>();
        private readonly List<Pawn> tmpTargets = new List<Pawn>();
    }

    public class RancorVolley : IExposable
    {
        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_Defs.Look(ref projectileDef, "projectileDef");
            Scribe_Values.Look(ref shotsRemaining, "shotsRemaining");
            Scribe_Values.Look(ref searchRadius, "searchRadius", 29.5f);
            Scribe_Values.Look(ref ticksBetweenProjectiles, "ticksBetweenProjectiles", 2);
            Scribe_Values.Look(ref nextLaunchTick, "nextLaunchTick");
            Scribe_Values.Look(ref targetIndex, "targetIndex");
            Scribe_Values.Look(ref fallbackTargetCell, "fallbackTargetCell");
            Scribe_Values.Look(ref radialBurst, "radialBurst", false);
            Scribe_Values.Look(ref burstCenter, "burstCenter");
            Scribe_Values.Look(ref burstCount, "burstCount");
            Scribe_Values.Look(ref burstAngle, "burstAngle");
        }

        public Pawn caster;
        public ThingDef projectileDef;
        public int shotsRemaining;
        public float searchRadius;
        public int ticksBetweenProjectiles;
        public int nextLaunchTick;
        public int targetIndex;
        public IntVec3 fallbackTargetCell;
        public bool radialBurst;
        public Vector3 burstCenter;
        public int burstCount;
        public float burstAngle;
    }
}
