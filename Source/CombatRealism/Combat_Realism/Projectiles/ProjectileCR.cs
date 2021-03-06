﻿using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Combat_Realism
{
    public abstract class ProjectileCR : ThingWithComps
    {
        protected Vector3 origin;
        protected Vector3 destination;
        protected Thing assignedTarget;
        public bool canFreeIntercept;
        protected ThingDef equipmentDef;
        protected Thing launcher;
        private Thing assignedMissTargetInt;
        protected bool landed;
        private float suppressionAmount;
        protected int ticksToImpact;
        private Sustainer ambientSustainer;
        private static List<IntVec3> checkedCells = new List<IntVec3>();
        public static readonly String[] robotBodyList = { "AIRobot", "HumanoidTerminator" };

        public Thing AssignedMissTarget
        {
            get { return assignedMissTargetInt; }
            set
            {
                if (value.def.Fillage == FillCategory.Full)
                {
                    return;
                }
                assignedMissTargetInt = value;
            }
        }

        protected int StartingTicksToImpact
        {
            get
            {
                int num =
                    Mathf.RoundToInt((float)((origin - destination).magnitude / (Math.Cos(shotAngle) * shotSpeed / 100f)));
                if (num < 1)
                {
                    num = 1;
                }
                return num;
            }
        }

        protected IntVec3 DestinationCell
        {
            get
            {
                return new IntVec3(destination);
            }
        }

        public virtual Vector3 ExactPosition
        {
            get
            {
                Vector3 b = (destination - origin) * (1f - ticksToImpact / (float)StartingTicksToImpact);
                return origin + b + Vector3.up * def.Altitude;
            }
        }

        public virtual Quaternion ExactRotation
        {
            get
            {
                return Quaternion.LookRotation(destination - origin);
            }
        }

        public override Vector3 DrawPos
        {
            get
            {
                return ExactPosition;
            }
        }

        //New variables
        private const float treeCollisionChance = 0.5f; //Tree collision chance is multiplied by this factor
        public float shotAngle;
        public float shotHeight = 0f;
        public float shotSpeed = -1f;

        private float distanceFromOrigin
        {
            get
            {
                Vector3 currentPos = Vector3.Scale(ExactPosition, new Vector3(1, 0, 1));
                return (currentPos - origin).magnitude;
            }
        }


        /*
         * *** End of class variables ***
        */

        //Keep track of new variables
        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving && launcher != null && launcher.Destroyed)
            {
                launcher = null;
            }
            Scribe_Values.LookValue(ref origin, "origin", default(Vector3), false);
            Scribe_Values.LookValue(ref destination, "destination", default(Vector3), false);
            Scribe_References.LookReference(ref assignedTarget, "assignedTarget");
            Scribe_Values.LookValue(ref canFreeIntercept, "canFreeIntercept", false, false);
            Scribe_Defs.LookDef(ref equipmentDef, "equipmentDef");
            Scribe_References.LookReference(ref launcher, "launcher");
            Scribe_References.LookReference(ref assignedMissTargetInt, "assignedMissTarget");
            Scribe_Values.LookValue(ref landed, "landed", false, false);
            Scribe_Values.LookValue(ref ticksToImpact, "ticksToImpact", 0, false);

            //Here be new variables
            Scribe_Values.LookValue(ref shotAngle, "shotAngle", 0f, true);
            Scribe_Values.LookValue(ref shotAngle, "shotHeight", 0f, true);
            Scribe_Values.LookValue(ref shotSpeed, "shotSpeed", 0f, true);
        }

        public static float GetProjectileHeight(float zeroheight, float distance, float angle, float velocity)
        {
            const float gravity = CR_Utility.gravityConst;
            float height =
                (float)
                    (zeroheight +
                     (distance * Math.Tan(angle) - gravity * Math.Pow(distance, 2) / (2 * Math.Pow(velocity * Math.Cos(angle), 2))));

            return height;
        }

        //Added new calculations for downed pawns, destination
        public virtual void Launch(Thing launcher, Vector3 origin, LocalTargetInfo targ, Thing equipment = null)
        {
            if (shotSpeed < 0)
            {
                shotSpeed = def.projectile.speed;
            }
            this.launcher = launcher;
            this.origin = origin;
            if (equipment != null)
            {
                equipmentDef = equipment.def;
            }
            else
            {
                equipmentDef = null;
            }
            //Checking if target was downed on launch
            if (targ.Thing != null)
            {
                assignedTarget = targ.Thing;
            }
            //Checking if a new destination was set
            if (destination == null)
            {
                destination = targ.Cell.ToVector3Shifted() +
                              new Vector3(Rand.Range(-0.3f, 0.3f), 0f, Rand.Range(-0.3f, 0.3f));
            }

            ticksToImpact = StartingTicksToImpact;
            if (!def.projectile.soundAmbient.NullOrUndefined())
            {
                SoundInfo info = SoundInfo.InMap(this, MaintenanceType.PerTick);
                ambientSustainer = def.projectile.soundAmbient.TrySpawnSustainer(info);
            }
        }

        //Added new method, takes Vector3 destination as argument
        public void Launch(Thing launcher, Vector3 origin, LocalTargetInfo targ, Vector3 target, Thing equipment = null)
        {
            destination = target;
            Launch(launcher, origin, targ, equipment);
        }

        //Removed minimum collision distance
        private bool CheckForFreeInterceptBetween(Vector3 lastExactPos, Vector3 newExactPos)
        {
            IntVec3 lastPos = lastExactPos.ToIntVec3();
            IntVec3 newPos = newExactPos.ToIntVec3();
            if (newPos == lastPos)
            {
                return false;
            }
            if (!lastPos.InBounds(base.Map) || !newPos.InBounds(base.Map))
            {
                return false;
            }
            if ((newPos - lastPos).LengthManhattan == 1)
            {
                return CheckForFreeIntercept(newPos);
            }
            //Check for minimum collision distance
            float distToTarget = assignedTarget != null
                ? (assignedTarget.DrawPos - origin).MagnitudeHorizontal()
                : (destination - origin).MagnitudeHorizontal();
            if (def.projectile.alwaysFreeIntercept
                || distToTarget <= 1f
                ? origin.ToIntVec3().DistanceToSquared(newPos) > 1f
                : origin.ToIntVec3().DistanceToSquared(newPos) > Mathf.Min(12f, distToTarget / 2))
            {
                Vector3 currentExactPos = lastExactPos;
                Vector3 flightVec = newExactPos - lastExactPos;
                Vector3 sectionVec = flightVec.normalized * 0.2f;
                int numSections = (int)(flightVec.MagnitudeHorizontal() / 0.2f);
                checkedCells.Clear();
                int currentSection = 0;
                while (true)
                {
                    currentExactPos += sectionVec;
                    IntVec3 intVec3 = currentExactPos.ToIntVec3();
                    if (!checkedCells.Contains(intVec3))
                    {
                        if (CheckForFreeIntercept(intVec3))
                        {
                            break;
                        }
                        checkedCells.Add(intVec3);
                    }
                    currentSection++;
                    if (currentSection > numSections)
                    {
                        return false;
                    }
                    if (intVec3 == newPos)
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        //Added collision detection for cover objects, changed pawn collateral chances
        private bool CheckForFreeIntercept(IntVec3 cell)
        {
            //Check for minimum collision distance
            float distFromOrigin = (cell.ToVector3Shifted() - origin).MagnitudeHorizontal();
            float distToTarget = assignedTarget != null
                ? (assignedTarget.DrawPos - origin).MagnitudeHorizontal()
                : (destination - origin).MagnitudeHorizontal();
            if (!def.projectile.alwaysFreeIntercept
                && distToTarget <= 1f
                ? distFromOrigin < 1f
                : distFromOrigin < Mathf.Min(12f, distToTarget / 2))
            {
                return false;
            }
            List<Thing> mainThingList = new List<Thing>(base.Map.thingGrid.ThingsListAt(cell));

            //Find pawns in adjacent cells and append them to main list
            List<IntVec3> adjList = new List<IntVec3>();
            Vector3 shotVec = (destination - origin).normalized;

            //Check if bullet is going north-south or west-east
            if (Math.Abs(shotVec.x) < Math.Abs(shotVec.z))
            {
                adjList = GenAdj.CellsAdjacentCardinal(cell, Rotation, new IntVec2(0, 1)).ToList();
            }
            else
            {
                adjList = GenAdj.CellsAdjacentCardinal(cell, Rotation, new IntVec2(1, 0)).ToList();
            }

            //Iterate through adjacent cells and find all the pawns
            for (int i = 0; i < adjList.Count; i++)
            {
                if (adjList[i].InBounds(base.Map) && !adjList[i].Equals(cell))
                {
                    List<Thing> thingList = new List<Thing>(base.Map.thingGrid.ThingsListAt(adjList[i]));
                    List<Thing> pawns =
                        thingList.Where(
                            thing => thing.def.category == ThingCategory.Pawn && !mainThingList.Contains(thing))
                            .ToList();
                    mainThingList.AddRange(pawns);
                }
            }

            //Check for entries first so we avoid doing costly height calculations
            if (mainThingList.Count > 0)
            {
                float height = GetProjectileHeight(shotHeight, distanceFromOrigin, shotAngle, shotSpeed);
                for (int i = 0; i < mainThingList.Count; i++)
                {
                    Thing thing = mainThingList[i];
                    if (thing.def.Fillage == FillCategory.Full) //ignore height
                    {
                        Impact(thing);
                        return true;
                    }
                    //Check for trees		--		HARDCODED RNG IN HERE
                    if (thing.def.category == ThingCategory.Plant && thing.def.altitudeLayer == AltitudeLayer.Building &&
                        Rand.Value <
                        thing.def.fillPercent * Mathf.Clamp(distFromOrigin / 40, 0f, 1f / treeCollisionChance) *
                        treeCollisionChance)
                    {
                        Impact(thing);
                        return true;
                    }
                    //Checking for pawns/cover
                    if (thing.def.category == ThingCategory.Pawn ||
                        (ticksToImpact < StartingTicksToImpact / 2 && thing.def.fillPercent > 0)) //Need to check for fillPercent here or else will be impacting things like motes, etc.
                    {
                        return ImpactThroughBodySize(thing, height);
                    }
                }
            }
            return false;
        }



        /// <summary>
        ///     Takes into account the target being downed and the projectile having been fired while the target was downed, and
        ///     the target's bodySize
        /// </summary>
        private bool ImpactThroughBodySize(Thing thing, float height)
        {
            Pawn pawn = thing as Pawn;

            if (pawn != null)
            {
                PersonalShield shield = null;
                if (pawn.RaceProps.Humanlike)
                {
                    // check for shield user

                    List<Apparel> wornApparel = pawn.apparel.WornApparel;
                    for (int i = 0; i < wornApparel.Count; i++)
                    {
                        if (wornApparel[i] is PersonalShield)
                        {
                            shield = (PersonalShield)wornApparel[i];
                            break;
                        }
                    }
                }
                //Add suppression
                CompSuppressable compSuppressable = pawn.TryGetComp<CompSuppressable>();
                if (compSuppressable != null)
                {
                    if (shield == null || (shield != null && shield?.ShieldState == ShieldState.Resetting))
                    {
                        /*
                        if (pawn.skills.GetSkill(SkillDefOf.Shooting).level >= 1)
                        {
                            suppressionAmount = (def.projectile.damageAmountBase * (1f - ((pawn.skills.GetSkill(SkillDefOf.Shooting).level) / 100) * 3));
                        }
                        else suppressionAmount = def.projectile.damageAmountBase;
                        */
                        suppressionAmount = def.projectile.damageAmountBase;
                        ProjectilePropertiesCR propsCR = def.projectile as ProjectilePropertiesCR;
                        float penetrationAmount = propsCR == null ? 0f : propsCR.armorPenetration;
                        suppressionAmount *= 1 - Mathf.Clamp(compSuppressable.parentArmor - penetrationAmount, 0, 1);
                        compSuppressable.AddSuppression(suppressionAmount, origin.ToIntVec3());
                    }
                }

                //Check horizontal distance
                Vector3 dest = destination;
                Vector3 orig = origin;
                Vector3 pawnPos = pawn.DrawPos;
                float closestDistToPawn = Math.Abs((dest.z - orig.z) * pawnPos.x - (dest.x - orig.x) * pawnPos.z +
                                                 dest.x * orig.z - dest.z * orig.x)
                                        /
                                        (float)
                                            Math.Sqrt((dest.z - orig.z) * (dest.z - orig.z) +
                                                      (dest.x - orig.x) * (dest.x - orig.x));
                if (closestDistToPawn <= CR_Utility.GetCollisionWidth(pawn))
                {
                    //Check vertical distance
                    float pawnHeight = CR_Utility.GetCollisionHeight(pawn);
                    if (height < pawnHeight)
                    {
                        Impact(thing);
                        return true;
                    }
                }
            }
            if (thing.def.fillPercent > 0 || thing.def.Fillage == FillCategory.Full)
            {
                if (height < CR_Utility.GetCollisionHeight(thing) || thing.def.Fillage == FillCategory.Full)
                {
                    Impact(thing);
                    return true;
                }
            }
            return false;
        }

        //Modified collision with downed pawns
        private void ImpactSomething()
        {
            //Not modified, just mortar code
            if (def.projectile.flyOverhead)
            {
                RoofDef roofDef = base.Map.roofGrid.RoofAt(base.Position);
                if (roofDef != null && roofDef.isThickRoof)
                {
                    def.projectile.soundHitThickRoof.PlayOneShot(new TargetInfo(base.Position, base.Map, false));
                    Destroy(DestroyMode.Vanish);
                    return;
                }
            }

            //Modified
            if (assignedTarget != null && assignedTarget.Position == Position)
            //it was aimed at something and that something is still there
            {
                ImpactThroughBodySize(assignedTarget,
                    GetProjectileHeight(shotHeight, distanceFromOrigin, shotAngle, shotSpeed));
            }
            else
            {
                Thing thing = base.Map.thingGrid.ThingAt(Position, ThingCategory.Pawn);
                if (thing != null)
                {
                    ImpactThroughBodySize(thing,
                        GetProjectileHeight(shotHeight, distanceFromOrigin, shotAngle, shotSpeed));
                    return;
                }
                List<Thing> list = base.Map.thingGrid.ThingsListAt(Position);
                float height = list.Count > 0
                    ? GetProjectileHeight(shotHeight, distanceFromOrigin, shotAngle, shotSpeed)
                    : 0;
                if (height > 0)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Thing thing2 = list[i];
                        bool impacted = ImpactThroughBodySize(thing2, height);
                        if (impacted)
                            return;
                    }
                }
                Impact(null);
            }
        }

        //Unmodified
        public void Launch(Thing launcher, LocalTargetInfo targ, Thing equipment = null)
        {
            Launch(launcher, Position.ToVector3Shifted(), targ, equipment);
        }

        //Unmodified
        public override void Tick()
        {
            base.Tick();
            if (landed)
            {
                return;
            }
            Vector3 exactPosition = ExactPosition;
            ticksToImpact--;
            if (!ExactPosition.InBounds(base.Map))
            {
                ticksToImpact++;
                Position = ExactPosition.ToIntVec3();
                Destroy(DestroyMode.Vanish);
                return;
            }
            Vector3 exactPosition2 = ExactPosition;
            if (!def.projectile.flyOverhead && canFreeIntercept &&
                CheckForFreeInterceptBetween(exactPosition, exactPosition2))
            {
                return;
            }
            Position = ExactPosition.ToIntVec3();
            if (ticksToImpact == 60f && Find.TickManager.CurTimeSpeed == TimeSpeed.Normal &&
                def.projectile.soundImpactAnticipate != null)
            {
                def.projectile.soundImpactAnticipate.PlayOneShot(this);
            }
            if (ticksToImpact <= 0)
            {
                if (DestinationCell.InBounds(base.Map))
                {
                    Position = DestinationCell;
                }
                ImpactSomething();
                return;
            }
            if (ambientSustainer != null)
            {
                ambientSustainer.Maintain();
            }
            // attack shooting expression
            if (this.launcher is Building_TurretGunCR == false)
            {
                if (Rand.Value > 0.7
                    && this.launcher.def.race.Humanlike
                    && !robotBodyList.Contains(this.launcher.def.race.body.defName)
                    && Gen.IsHashIntervalTick(launcher, Rand.Range(280, 700)))
                {
                    AGAIN: string rndswear = RulePackDef.Named("AttackMote").Rules.RandomElement().Generate();
                    if (rndswear == "[swear]" || rndswear == "" || rndswear == " ")
                    {
                        goto AGAIN;
                    }
                    MoteMaker.ThrowText(launcher.Position.ToVector3Shifted(), this.launcher.Map, rndswear);
                }
            }
        }

        //Unmodified
        public override void Draw()
        {
            Graphics.DrawMesh(MeshPool.plane10, DrawPos, ExactRotation, def.DrawMatSingle, 0);
            Comps_PostDraw();
        }

        //Unmodified
        protected virtual void Impact(Thing hitThing)
        {
            CompExplosiveCR comp = this.TryGetComp<CompExplosiveCR>();
            if (comp != null)
            {
                comp.Explode(launcher, Position, Find.VisibleMap);
            }
            Destroy(DestroyMode.Vanish);
        }

        //Unmodified
        public void ForceInstantImpact()
        {
            if (!DestinationCell.InBounds(base.Map))
            {
                Destroy(DestroyMode.Vanish);
                return;
            }
            ticksToImpact = 0;
            Position = DestinationCell;
            ImpactSomething();
        }
    }
}