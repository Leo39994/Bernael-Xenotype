using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;

namespace Bernael_Xenotype
{
    [StaticConstructorOnStartup]
    public class GameCondition_GazeCaligo : GameCondition
    {
        [StaticConstructorOnStartup]
	    private class GazeCaligoOverlay : SkyOverlay
	    {
	    	private static readonly Material GazeCaligoOverlayWorld = MaterialPool.MatFrom("Other/BX_GazeCaligoOverlayWorld", ShaderDatabase.MetaOverlay);
	    	private static readonly ComplexCurve speedCurve = new ComplexCurve(new UnityEngine.Keyframe(0f, 0f), new UnityEngine.Keyframe(1f, 1f), new UnityEngine.Keyframe(2f, 1.1f), new UnityEngine.Keyframe(3f, 1.2f));
	    	private TexturePannerSpeedCurve panner0 = new TexturePannerSpeedCurve(GazeCaligoOverlayWorld, "_MainTex", speedCurve, new Vector2(-1f, -0.2f), 0.0001f);
	    	private TexturePannerSpeedCurve panner1 = new TexturePannerSpeedCurve(GazeCaligoOverlayWorld, "_MainTex2", speedCurve, new Vector2(0.35f, -1f), 5E-05f);

	    	public override void SetOverlayColor(Color color)
	    	{
	    		GazeCaligoOverlayWorld.color = color;
	    	}

	    	public override void DrawOverlay(Map map)
	    	{
	    		SkyOverlay.DrawWorldOverlay(map, GazeCaligoOverlayWorld);
	    	}

	    	public override void TickOverlay(Map map, float lerpFactor)
	    	{
	    		panner0.Tick();
	    		panner1.Tick();
	    	}
	    }

        private int curColorIndex = -1;
	    private int prevColorIndex = -1;
	    private float curColorTransition;

        private Vector3 currentPupilPos = Vector3.zero;
        private Vector3 targetPupilPos = Vector3.zero;
        private int nextPupilMoveTick = -1;

        private const int MoveIntervalTicks = 3000; // 50s or 1 in-game hour
        private const float MinPupilScaleLimit = 0.75f;
        private const float MoveSpeedPerTick = 0.05f;
        private const float DiameterMapFraction = 0.33f;

        // or AltitudeLayer.Weather.AltitudeFor() - 0.03658537f
        private static readonly float PupilAltitude = AltitudeLayer.Weather.AltitudeFor() + 0.03658537f;

        private static readonly MaterialPropertyBlock pupilPropertyBlock = new MaterialPropertyBlock();
        private static readonly Material PupilMaterial = MaterialPool.MatFrom("Other/BX_Pupil", ShaderDatabase.Transparent);
        private static readonly MaterialPropertyBlock glowPropertyBlock = new MaterialPropertyBlock();
        private static readonly Material GlowCircleMaterial = MaterialPool.MatFrom("Other/BX_GlowCircle", ShaderDatabase.MoteGlow);
        private Color pupilBaseColor = new Color(0f, 0f, 0f);
        private Color glowBaseColor = new Color(0.6f, 0.8f, 1f);

        private int nextBlinkTick = -1;
        private int blinkEndTick = -1;
        private bool isBlinking = false;
        private IntRange BlinkIntervalTicks = new IntRange(600, 1800); // 10s ~ 30s
        private int blinkHoldTicks = 60;
        private const int BlinkFadeOutTicks = 30;   // 0.5s
        private const int BlinkFadeInTicks = 30;    // 0.5s

        private const float MaxSkyLerpFactor = 0.5f;
        private const float Glow = 0.85f;
        private const float BaseBrightness = 0.73f;
	    private static readonly GazeCaligoOverlay gazeCaligoOverlay = new GazeCaligoOverlay();
	    private static readonly List<SkyOverlay> overlays = new List<SkyOverlay> { gazeCaligoOverlay };
	    private static readonly Color[] Colors = new Color[5]
	    {
	    	new Color(0.2f, 0.4f, 1f),
	    	new Color(0.3f, 0.6f, 1f),
	    	new Color(0.5f, 0.7f, 1f),
	    	new Color(0.5f, 0.5f, 1f),
	    	new Color(0.3f, 0.3f, 1f)
	    };
        private static readonly Color ShadowColorTint = new ColorInt(20, 40, 60).ToColor;
	    public Color CurrentColor => Color.Lerp(Colors[prevColorIndex], Colors[curColorIndex], curColorTransition);
        private int TransitionDurationTicks => 280;

        private const int CheckInterval = 3251;
        private const int HediffDuration = 3256;

        // Audio & Event Flags
        private Sustainer sustainer;
        private Sustainer sustainerMoving;
        private bool hasTriggeredOutcome = false;
        private bool isHighResonance = false;

        public override void Init()
        {
            base.Init();
		    curColorIndex = Rand.Range(0, Colors.Length);
		    prevColorIndex = curColorIndex;
		    curColorTransition = 1f;

            blinkHoldTicks = 60;
            pupilBaseColor = new Color(0f, 0f, 0f, 1f);
            glowBaseColor = new Color(0.6f, 0.8f, 1f, 1f);

            int currentTick = Find.TickManager.TicksGame;
            nextPupilMoveTick = -1;
            ScheduleNextBlink(currentTick);

            hasTriggeredOutcome = false;
            SoundDefOf.Thunder_OnMap.PlayOneShotOnCamera();

            List<Map> affectedMaps = AffectedMaps;
            if (affectedMaps != null && affectedMaps.Count > 0)
            {
                CenterAndZoomOutCamera(affectedMaps[0]);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
		    Scribe_Values.Look(ref curColorIndex, "curColorIndex", 0);
		    Scribe_Values.Look(ref prevColorIndex, "prevColorIndex", 0);
		    Scribe_Values.Look(ref curColorTransition, "curColorTransition", 0f);

            Scribe_Values.Look(ref nextBlinkTick, "nextBlinkTick", -1);
            Scribe_Values.Look(ref blinkEndTick, "blinkEndTick", -1);
            Scribe_Values.Look(ref isBlinking, "isBlinking", false);

            Scribe_Values.Look(ref hasTriggeredOutcome, "hasTriggeredOutcome", false);
            Scribe_Values.Look(ref isHighResonance, "isHighResonance", false);
        }

        public override void GameConditionTick()
        {
            List<Map> maps = AffectedMaps;
            int currentTick = Find.TickManager.TicksGame;

		    curColorTransition += 1f / TransitionDurationTicks;
		    if (curColorTransition >= 1f)
		    {
		    	prevColorIndex = curColorIndex;
		    	curColorIndex = GetNewColorIndex();
		    	curColorTransition = 0f;
		    }

            // Update blinking state
            if (!isBlinking && currentTick + MoveIntervalTicks >= startTick + Duration && !hasTriggeredOutcome)
            {
                blinkHoldTicks = MoveIntervalTicks / 3;
                hasTriggeredOutcome = true;
                isBlinking = true;
                BernaelDefOf.BX_Gaze_Blink.PlayOneShotOnCamera();
            }
            else if (!isBlinking && currentTick >= nextBlinkTick && !hasTriggeredOutcome)
            {
                isBlinking = true;
                BernaelDefOf.BX_Gaze_Blink.PlayOneShotOnCamera();
            }
            else if (isBlinking && currentTick >= blinkEndTick)
            {
                isBlinking = false;
                ScheduleNextBlink(currentTick);
            }

            if (sustainer == null || sustainer.Ended)
            {
                sustainer = BernaelDefOf.BX_Ambient_Gaze.TrySpawnSustainer(SoundInfo.OnCamera(MaintenanceType.PerTick));
            }
            else
            {
                sustainer?.Maintain();
            }

            if (currentTick % CheckInterval == 0)
            {
                for (int m = 0; m < maps.Count; m++)
                {
                    Map map = maps[m];
                    if (map == null) continue;

                    IReadOnlyList<Pawn> allPawns = map.mapPawns.AllPawnsSpawned;
                    for (int i = 0; i < allPawns.Count; i++)
                    {
                        Pawn pawn = allPawns[i];
                        if (pawn.kindDef.immuneToGameConditionEffects) continue;

                        // Check Psylink
                        HediffDef hediff = pawn.health.hediffSet.HasHediff(HediffDefOf.PsychicAmplifier)
                            ? BernaelDefOf.BX_GazeCaligoBuff
                            : BernaelDefOf.BX_GazeCaligoDebuff;

                        GiveOrUpdateHediff(pawn, hediff);
                    }
                }
            }

            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                if (HiddenByOtherCondition(map)) continue;

                gazeCaligoOverlay.SetOverlayColor(CurrentColor);
                gazeCaligoOverlay.TickOverlay(map, 1f);
                MovePupil(map, currentTick);
            }
        }

        public override void GameConditionDraw(Map map)
        {
            if (!HiddenByOtherCondition(map))
		    {
		    	gazeCaligoOverlay.DrawOverlay(map);
                DrawPupil(map);
                DrawGlow(map);
		    }
        }

        public override void End()
        {
            List<Map> maps = AffectedMaps;

            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                IncidentDef selectedIncident = isHighResonance 
                    ? GetRandomHighResonanceIncident() 
                    : GetRandomLowResonanceIncident();

                OutcomeIncident(selectedIncident, map);
            }
            
            sustainer?.End();
            sustainer = null;

            SoundDefOf.Thunder_OffMap.PlayOneShotOnCamera();
            base.End();
        }

        public override float SkyTargetLerpFactor(Map map) => GameConditionUtility.LerpInOutValue(this, TransitionTicks, MaxSkyLerpFactor);

        public override SkyTarget? SkyTarget(Map map)
        {
            if (map.GameConditionManager.IsAlwaysDarkOutside)
		    {
		    	return null;
		    }
		    Color currentColor = CurrentColor;
            return new SkyTarget(Mathf.Max(GenCelestial.CurCelestialSunGlow(map), Glow), new SkyColorSet(currentColor * GetBlinkFactor(map) * Brightness(map), ShadowColorTint * GetBlinkFactor(map), currentColor * Brightness(map), 1.25f), 4.5f, 0.8f);
        }

        public override List<SkyOverlay> SkyOverlays(Map map)
	    {
	    	return overlays;
	    }

        private void ScheduleNextBlink(int currentTick)
        {
            nextBlinkTick = currentTick + BlinkIntervalTicks.RandomInRange;
            blinkEndTick = nextBlinkTick + blinkHoldTicks;
        }

        private void CenterAndZoomOutCamera(Map map)
        {
            if (map == null || map != Find.CurrentMap) return;

            CameraDriver driver = Find.CameraDriver;
            if (driver == null) return;

            Vector3 mapCenter = map.Center.ToVector3Shifted();
            float maxSize = driver.config.sizeRange.max;

            driver.SetRootPosAndSize(mapCenter, maxSize);
        }

        private int GetNewColorIndex()
	    {
	    	return (from x in Enumerable.Range(0, Colors.Length)
	    		where x != curColorIndex
	    		select x).RandomElement();
	    }

	    private void GiveOrUpdateHediff(Pawn target, HediffDef hediffDef)
	    {
            if (target?.health?.hediffSet == null) return;
            Hediff hediff = target.health.hediffSet.GetFirstHediffOfDef(hediffDef) ?? target.health.AddHediff(hediffDef);
            HediffComp_Disappears comp = hediff?.TryGetComp<HediffComp_Disappears>();
            if (comp != null) comp.ticksToDisappear = HediffDuration;
        }

        private void MovePupil(Map map, int currentTick)
        {
            Vector3 mapCenter = new Vector3(map.Size.x * 0.5f, PupilAltitude, map.Size.z * 0.5f);

            if (nextPupilMoveTick <= 0)
            {
                currentPupilPos = mapCenter;
                targetPupilPos = mapCenter;
                nextPupilMoveTick = currentTick + MoveIntervalTicks / 10;
            }
            else if (currentTick + (MoveIntervalTicks * 1.2f) >= startTick + Duration)
            {
                targetPupilPos = mapCenter;
                targetPupilPos.y = PupilAltitude;
                nextPupilMoveTick = currentTick + (int)(MoveIntervalTicks * 1.2f);
            }
            else if (currentTick >= nextPupilMoveTick && currentTick + MoveIntervalTicks < startTick + Duration)
            {
                targetPupilPos = NewPupilTargetPos(map, MinPupilScaleLimit);
                targetPupilPos.y = PupilAltitude;
                nextPupilMoveTick = currentTick + MoveIntervalTicks;
            }

            // Step movement calculation
            Vector3 toTargetPos = targetPupilPos - currentPupilPos;
            toTargetPos.y = 0f;

            float distanceToTargetPos = toTargetPos.magnitude;
            if (distanceToTargetPos > 0f)
            {
                float step = Mathf.Min(MoveSpeedPerTick, distanceToTargetPos);
                currentPupilPos += toTargetPos / distanceToTargetPos * step;
                if (sustainerMoving == null || sustainerMoving.Ended)
                {
                    sustainerMoving = BernaelDefOf.BX_Gaze_Move.TrySpawnSustainer(SoundInfo.OnCamera(MaintenanceType.PerTick));
                }
                else
                {
                    sustainerMoving?.Maintain();
                }
            }
            else
            {
                sustainerMoving?.End();
                sustainerMoving = null;
            }
        }

        private void DrawPupil(Map map)
        {
            Vector3 mapCenter = new Vector3(map.Size.x * 0.5f, PupilAltitude, map.Size.z * 0.5f);

            float minMapSize = Mathf.Min(map.Size.x, map.Size.z);
            float diameter = minMapSize * DiameterMapFraction;
            float pupilRadius = diameter * 0.5f;
            float maxValidRadius = Mathf.Max(0f, (minMapSize * 0.5f) - pupilRadius);

            // Pupil distortion calculation
            Vector3 offsetFromCenter = currentPupilPos - mapCenter;
            float distFromCenter = offsetFromCenter.magnitude;
            float maxOffsetRadius = Mathf.Max(0.01f, maxValidRadius);
            float normalizedDist = Mathf.Clamp01(distFromCenter / maxOffsetRadius);
            float circleArc = Mathf.Sqrt(1f - (normalizedDist * normalizedDist));
            float radialScale = Mathf.Lerp(MinPupilScaleLimit, 1f, circleArc);

            float scaleValue = Mathf.Min(TicksPassed, TicksLeft, 60) / 60f;
            float scaleX = diameter * radialScale * Mathf.Lerp(0.1f, 1.0f, scaleValue);
            float scaleZ = diameter * Mathf.Lerp(0.1f, 1.0f, scaleValue);

            Quaternion rotation = Quaternion.identity;
            if (distFromCenter > 0.0001f)
            {
                float angleDeg = Mathf.Atan2(offsetFromCenter.z, offsetFromCenter.x) * Mathf.Rad2Deg;
                rotation = Quaternion.AngleAxis(-angleDeg, Vector3.up); 
            }

            Matrix4x4 pupilMatrix = Matrix4x4.TRS(currentPupilPos, rotation, new Vector3(scaleX, 1f, scaleZ));
            pupilPropertyBlock.SetColor("_Color", new Color(pupilBaseColor.r, pupilBaseColor.g, pupilBaseColor.b, 1f * GetBlinkFactor(map) * scaleValue));
            Graphics.DrawMesh(MeshPool.plane10, pupilMatrix, PupilMaterial, 0, null, 0, pupilPropertyBlock);
        }

        private void DrawGlow(Map map)
        {
            Vector3 mapCenter = new Vector3(map.Size.x * 0.5f, PupilAltitude, map.Size.z * 0.5f);

            float scaleX = map.Size.x;
            float scaleZ = map.Size.z * Mathf.Lerp(0.1f, 1.0f, Mathf.Min(TicksPassed, TicksLeft, 180) / 180f);

            Vector3 glowScale = new Vector3(scaleX, 1f, scaleZ);
            Matrix4x4 glowMatrix = default;
            glowMatrix.SetTRS(mapCenter, Quaternion.identity, glowScale);
            glowPropertyBlock.SetColor("_Color", new Color(glowBaseColor.r, glowBaseColor.g, glowBaseColor.b, 1f * GetBlinkFactor(map) * Mathf.Min(TicksPassed, TicksLeft, 30)/ 30f));
            Graphics.DrawMesh(MeshPool.plane10, glowMatrix, GlowCircleMaterial, 0, null, 0, glowPropertyBlock);
        }

        private Vector3 NewPupilTargetPos(Map map, float MinPupilScaleLimit)
        {
            float minMapSize = Mathf.Min(map.Size.x, map.Size.z);
            float pupilRadius = minMapSize * DiameterMapFraction * 0.5f;
            Vector3 mapCenter = new Vector3(map.Size.x * 0.5f, PupilAltitude, map.Size.z * 0.5f);
            float maxValidRadius = Mathf.Max(0f, (minMapSize * 0.5f) - (pupilRadius * MinPupilScaleLimit));

            float randomAngle = Random.Range(0f, Mathf.PI * 2f);
            float randomDistance = Random.Range(maxValidRadius / 4f, maxValidRadius);

            Vector3 offset = new Vector3(Mathf.Cos(randomAngle) * randomDistance, 0f, Mathf.Sin(randomAngle) * randomDistance);
            Vector3 newTarget = mapCenter + offset;
            Vector3 displacementFromCenter = newTarget - mapCenter;
            displacementFromCenter.y = 0f;

            if (displacementFromCenter.magnitude > maxValidRadius)
            {
                newTarget = mapCenter + displacementFromCenter.normalized * maxValidRadius;
            }

            newTarget.y = PupilAltitude;
            return newTarget;
        }

        private float GetBlinkFactor(Map map)
        {
            if (!isBlinking) return 1.0f;

            int currentTick = Find.TickManager.TicksGame;
            int elapsed = currentTick - nextBlinkTick;

            // Fade Out
            if (elapsed < BlinkFadeOutTicks)
            {
                float progress = (float)elapsed / BlinkFadeOutTicks;
                return Mathf.Lerp(1.0f, 0.01f, progress);
            }

            // Fade In
            if (currentTick >= blinkEndTick - BlinkFadeInTicks)
            {
                // Last Long Blink Hold & Color Transformation
                if (currentTick + MoveIntervalTicks >= startTick + Duration)
                {
                    isHighResonance = CalculateResonance(map);
                    pupilBaseColor = isHighResonance ? Color.blue : Color.red;
                    glowBaseColor = isHighResonance ? Color.blue : Color.red;
                }

                int fadeInElapsed = currentTick - (blinkEndTick - BlinkFadeInTicks);
                float progress = (float)fadeInElapsed / BlinkFadeInTicks;
                return Mathf.Lerp(0.01f, 1.0f, progress);
            }

            return 0.0f;
        }

	    private float Brightness(Map map)
	    {
	    	return Mathf.Max(BaseBrightness, GenCelestial.CurCelestialSunGlow(map));
	    }

        // ====================== Outcome event ======================

        private bool CalculateResonance(Map map)
        {
            IReadOnlyList<Pawn> allPawns = map.mapPawns?.AllPawnsSpawned;
            if (allPawns == null) return false;

            float baseChance = 0.25f;
            float psyCasterCount = 0;
            float totalPsylinkLevel = 0;

            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn pawn = allPawns[i];
                if (pawn == null || pawn.Dead || !pawn.IsColonist) continue;

                if (pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PsychicAmplifier) is Hediff_Psylink psylink)
                {
                    psyCasterCount++;
                    totalPsylinkLevel += psylink.level;
                }
            }

            return Rand.Chance(baseChance + psyCasterCount * 0.1f + totalPsylinkLevel * 0.05f);
        }

        private IncidentDef GetRandomHighResonanceIncident()
        {
            IncidentDef[] highIncidents = {
                BernaelDefOf.BX_AstralAwakening,
                BernaelDefOf.BX_EthersBlessing,
                BernaelDefOf.BX_PsycasterPilgrimage,
                BernaelDefOf.BX_PsychicOvercharge
            };
            return highIncidents[Rand.Range(0, highIncidents.Length)];
        }

        private IncidentDef GetRandomLowResonanceIncident()
        {
            IncidentDef[] lowIncidents = {
                BernaelDefOf.BX_PsychicBurnout,
                BernaelDefOf.BX_PsychicRupture
            };
            return lowIncidents[Rand.Range(0, lowIncidents.Length)];
        }

	    private void OutcomeIncident(IncidentDef incidentDef, Map map)
        {
            if (incidentDef == null) return;
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, map);
            incidentDef.Worker.TryExecute(parms);
        }
    }
}