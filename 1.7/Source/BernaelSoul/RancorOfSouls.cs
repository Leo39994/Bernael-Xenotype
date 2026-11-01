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

        internal static void LaunchProjectile(Pawn caster, ThingDef projectileDef, LocalTargetInfo target)
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

            GenSpawn.Spawn(projectile, caster.Position, caster.Map);
            projectile.Launch(caster, caster.DrawPos, target, target, ProjectileHitFlags.IntendedTarget, true);
        }

        private int cachedTargetTick = -1;
        private bool cachedHasTarget;
    }

    public class MapComponent_RancorVolley : MapComponent
    {
        public MapComponent_RancorVolley(Map map)
            : base(map)
        {
        }

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
                nextLaunchTick = Find.TickManager.TicksGame,
                fallbackTargetCell = fallbackTargetCell
            };

            volleys.Add(volley);
            LaunchNext(volley);
            if (volley.shotsRemaining <= 0)
            {
                volleys.Remove(volley);
            }
        }

        public override void MapComponentTick()
        {
            if (volleys.Count == 0)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            for (int index = volleys.Count - 1; index >= 0; index--)
            {
                RancorVolley volley = volleys[index];
                if (volley == null || !VolleyCanContinue(volley))
                {
                    volleys.RemoveAt(index);
                    continue;
                }

                if (currentTick < volley.nextLaunchTick)
                {
                    continue;
                }

                LaunchNext(volley);
                if (volley.shotsRemaining <= 0)
                {
                    volleys.RemoveAt(index);
                }
            }
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
        }

        public Pawn caster;
        public ThingDef projectileDef;
        public int shotsRemaining;
        public float searchRadius;
        public int ticksBetweenProjectiles;
        public int nextLaunchTick;
        public int targetIndex;
        public IntVec3 fallbackTargetCell;
    }
}
