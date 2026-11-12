using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;

namespace Bernael_Xenotype
{
    [StaticConstructorOnStartup]
    public class CursedFire : Fire
    {
        private int cursedAge;
        private Sustainer sustainer;
        private bool fromDireOrb;
        private Faction sourceFaction;
        private Pawn struck;
        private static readonly MaterialPropertyBlock fireProperties = new MaterialPropertyBlock();

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!fromDireOrb || !DarkMagicAssets.Ready())
            {
                base.DrawAt(drawLoc, flip);
                return;
            }
            // Keep small fires readable without increasing their damage, growth or lifetime. Each burning cell
            // gets its own size, lean and facing so a field of them never reads as a grid.
            int seed = thingIDNumber % 997;
            float r1 = (seed * 31 % 97) / 96f, r2 = (seed * 53 % 89) / 88f, r3 = (seed * 71 % 83) / 82f;
            float scale = parent != null ? 1f : r1 < 0.35f ? Mathf.Lerp(1.05f, 1.25f, r2)
                : r1 < 0.75f ? Mathf.Lerp(0.78f, 0.92f, r2) : Mathf.Lerp(0.5f, 0.62f, r2);
            float size = (0.55f + fireSize * 0.22f) * scale;
            if (parent == null)
            {
                // The entity stays at its exact gameplay cell. Only its flame leans within it.
                drawLoc.x += (r2 - 0.5f) * 0.76f;
                drawLoc.z += (r3 - 0.5f) * 0.6f;
            }
            float grow = growStartTick < 0 ? 1f : Mathf.Clamp01((Find.TickManager.TicksGame - growStartTick) / 22f);
            fireProperties.Clear();
            fireProperties.SetFloat("_Mode", 3f);
            fireProperties.SetFloat("_Phase", Find.TickManager.TicksGame / 60f);
            fireProperties.SetFloat("_Seed", seed * 0.13f);
            fireProperties.SetFloat("_Opacity", 1f);
            fireProperties.SetFloat("_Grow", grow);
            fireProperties.SetFloat("_Flip", r3 > 0.5f ? 1f : 0f);
            float height = size * Mathf.Lerp(1.15f, 1.5f, r3);
            drawLoc.z += height * 0.5f - 0.18f * size;
            DarkMagicAssets.Draw(DarkMagicAssets.Orb, drawLoc, Quaternion.identity,
                new Vector3(size, 1f, height), fireProperties);
        }

        public void ConfigureDireOrb(Pawn caster, Faction faction, Pawn directTarget, int growDelayTicks = 0)
        {
            fromDireOrb = true;
            instigator = caster;
            sourceFaction = faction;
            struck = directTarget;
            growStartTick = Find.TickManager.TicksGame + growDelayTicks;
        }

        // Visual only: when the burst lit this fire. Loaded and merged fires are already fully grown.
        private int growStartTick = -1;

        private bool CanBurn(Pawn pawn) => !pawn.Dead && (!fromDireOrb ||
            (!pawn.Downed && (pawn == struck || (sourceFaction != null ? pawn.HostileTo(sourceFaction) :
                instigator != null && pawn.HostileTo(instigator)))));

        public override string Label
        {
            get
            {
                if (parent != null)
                {
                    return "BX_CursedFireOn".Translate(parent.LabelCap, parent);
                }
                return def.label;
            }
        }

        public override string InspectStringAddon => "BX_CursedBurning".Translate() + " (" + "FireSizeLower".Translate((fireSize * 100f).ToString("F0")) + ")";

        public override int UpdateRateTicks => 15;

        public override void ExposeData()
        {
            base.ExposeData();
            // Fire already scribes fireSize/instigator and its own ticksSinceSpawn. A separate key
            // prevents the base and derived ages from shadowing each other on repeated loads.
            Scribe_Values.Look(ref cursedAge, "cursedAge", 0, forceSave: true);
            if (Scribe.mode == LoadSaveMode.LoadingVars && Scribe.loader.curXmlParent?["cursedAge"] == null)
            {
                var legacy = Scribe.loader.curXmlParent?.SelectNodes("ticksSinceSpawn");
                if (legacy != null && legacy.Count > 0 && int.TryParse(legacy[legacy.Count - 1].InnerText, out int age))
                    cursedAge = age;
            }
            Scribe_Values.Look(ref fromDireOrb, "fromDireOrb");
            Scribe_References.Look(ref sourceFaction, "sourceFaction");
            Scribe_References.Look(ref struck, "struck");
        }
        
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad && parent == null && Spawned)
            {
                MergeExistingFires(map);
            }
        }

        protected override void TickInterval(int delta)
        {
            if (fromDireOrb && parent is Pawn burning && !CanBurn(burning))
            {
                Destroy(DestroyMode.Vanish);
                return;
            }
            if (parent == null && Spawned)
            {
                MergeExistingFires(Map);
            }
            cursedAge += delta;
            float maxLifetime = 600f + (fireSize * 3000f); 
            if (cursedAge >= maxLifetime)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }
            if ((sustainer == null || sustainer.Ended) && !Position.Fogged(Map))
            {
                SoundInfo info = SoundInfo.InMap(new TargetInfo(Position, Map), MaintenanceType.PerTick);
                sustainer = SustainerAggregatorUtility.AggregateOrSpawnSustainerFor(this, SoundDefOf.FireBurning, info);
            }
            if (parent == null && Spawned)
            {
                Pawn steppingPawn = Map.thingGrid.ThingAt<Pawn>(Position);
                if (steppingPawn != null && steppingPawn.Spawned && CanBurn(steppingPawn))
                {
                    if (!steppingPawn.HasAttachment(def))
                    {
                        CursedFire pawnFire = (CursedFire)ThingMaker.MakeThing(def);
                        pawnFire.fireSize = fireSize;
                        pawnFire.instigator = instigator;
                        pawnFire.fromDireOrb = fromDireOrb;
                        pawnFire.sourceFaction = sourceFaction;
                        pawnFire.struck = struck;
                        pawnFire.AttachTo(steppingPawn);
                        GenSpawn.Spawn(pawnFire, Position, Map, Rot4.North);
                    }
                }
            }
            if (parent != null && this.IsHashIntervalTick(150, delta))
            {
                DoCursedDamage(parent);
            }
            if (Spawned)
            {
                float coldPower = fireSize * -80f;
                GenTemperature.PushHeat(Position, Map, coldPower);
                fireSize += 0.0002f * delta;
                if (fireSize > MaxFireSize) 
                {
                    fireSize = MaxFireSize;
                }
            }
        }

        private void MergeExistingFires(Map map)
        {
            bool hasMerged = false;
            List<Thing> thingList = Position.GetThingList(map);
            for (int i = thingList.Count - 1; i >= 0; i--)
            {
                Thing thing = thingList[i];
                if (thing is Fire otherFire && otherFire != this && otherFire.parent == null && CanMerge(otherFire))
                {
                    fireSize += otherFire.fireSize;
                    if (fireSize > MaxFireSize)
                    {
                        fireSize = MaxFireSize;
                    }
                    otherFire.Destroy(DestroyMode.Vanish);
                    hasMerged = true;
                }
            }
            if (hasMerged)
            {
                cursedAge = 0;
            }
        }

        private bool CanMerge(Fire other)
        {
            if (!(other is CursedFire cursed)) return !fromDireOrb;
            if (fromDireOrb != cursed.fromDireOrb) return false;
            return !fromDireOrb || (instigator == cursed.instigator && sourceFaction == cursed.sourceFaction && struck == cursed.struck);
        }

        protected override void Tick()
        {
            // This class owns its sound rather than the base Fire's private sustainer.
            if (sustainer != null && !sustainer.Ended) sustainer.Maintain();
        }

        private void DoCursedDamage(Thing target)
        {
            if (fromDireOrb && (!(target is Pawn victim) || !CanBurn(victim))) return;
            DamageDef damage = fromDireOrb ? DefDatabase<DamageDef>.GetNamed("BX_CursedFlame") : DamageDefOf.Frostbite;
            int damageAmount = GenMath.RoundRandom(Mathf.Clamp(0.0125f + 0.0036f * fireSize, 0.0125f, 0.05f) * 150f);
            if (damageAmount < 1)
            {
                damageAmount = 1;
            }
            if (target is Pawn pawn)
            {
                DamageInfo dinfo = new(damage, damageAmount, 0f, -1f, instigator ?? this);
                dinfo.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Outside);
                pawn.TakeDamage(dinfo);
                if (pawn.apparel != null && pawn.apparel.WornApparel.TryRandomElement(out var apparelResult))
                {
                    apparelResult.TakeDamage(new DamageInfo(damage, damageAmount, 0f, -1f, instigator ?? this));
                }
            }
            else
            {
                target.TakeDamage(new DamageInfo(damage, damageAmount, 0f, -1f, instigator ?? this));
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            // Sustainers may be shared by nearby fires; remove only this reporter.
            if (sustainer != null)
            {
                if (sustainer.externalParams.sizeAggregator == null)
                    sustainer.externalParams.sizeAggregator = new SoundSizeAggregator();
                sustainer.externalParams.sizeAggregator.RemoveReporter(this);
            }
            base.DeSpawn(mode);
        }
    }
}
