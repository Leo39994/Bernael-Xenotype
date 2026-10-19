using Verse;
using UnityEngine;

namespace Bernael_Xenotype
{
    public class HediffCompProperties_FoldShiftVisual : HediffCompProperties
    {
        public Color visualColor = Color.white;

        public HediffCompProperties_FoldShiftVisual()
        {
            compClass = typeof(HediffComp_FoldShiftVisual);
        }
    }

    public class HediffComp_FoldShiftVisual : HediffComp
    {
        public HediffCompProperties_FoldShiftVisual Props => (HediffCompProperties_FoldShiftVisual)props;
        private FoldShiftVisuals visuals;
        
        public override void CompPostPostRemoved()
        {
            visuals?.Dispose();
            visuals = null;
        }

        public void Draw(Vector3 drawLoc, Pawn pawn)
        {
            if (visuals == null)
            {
                visuals = new FoldShiftVisuals();
            }

            Shader shader = ShaderDatabase.Transparent; 

            visuals.InitOrUpdate(pawn, shader);
            visuals.Draw(drawLoc, Props.visualColor);
        }
    }
}