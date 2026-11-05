using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Bernael_Xenotype
{
    [StaticConstructorOnStartup]
    public class Command_ToggleAbility : Command_Ability
    {
        public Func<bool> isActive;
        public SoundDef turnOnSound = SoundDefOf.Checkbox_TurnedOn;
        public SoundDef turnOffSound = SoundDefOf.Checkbox_TurnedOff;
        public bool hideIconIfDisabled;

        public Command_ToggleAbility(Ability ability, Pawn pawn, Func<bool> isActive) : base(ability, pawn)
        {
            this.isActive = isActive;
        }

        public override SoundDef CurActivateSound
        {
            get
            {
                if (isActive())
                {
                    return turnOffSound;
                }
                return turnOnSound;
            }
        }

        public override void ProcessInput(Event ev)
        {
            CurActivateSound?.PlayOneShotOnCamera();
            base.ProcessInput(ev);
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);
            if (!disabled || !hideIconIfDisabled)
            {
                Rect rect = new(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
                Rect position = new(rect.x + rect.width - 24f, rect.y, 24f, 24f);
                Texture2D image = (isActive != null && isActive()) ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex;
                GUI.DrawTexture(position, image);
            }
            return result;
        }
    }
}