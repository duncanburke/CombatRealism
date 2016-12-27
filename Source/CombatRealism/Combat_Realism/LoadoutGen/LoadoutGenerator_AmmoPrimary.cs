using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Combat_Realism
{
    public class LoadoutGenerator_AmmoPrimary : LoadoutGenerator_List
    {
        /// <summary>
        /// Initializes availableDefs and adds all available ammo types of the currently equipped weapon 
        /// </summary>
        /// 

        protected override void InitAvailableDefs()
        {
            Pawn pawn = compInvInt.parent as Pawn;
            if(pawn == null)
            {
                Log.Error("Tried generating ammo loadout defs with null pawn");
                return;
            }
            Log.Message(String.Format("AmmoPrimary: Pawn {0}", pawn));
            if (pawn.equipment != null && pawn.equipment.Primary != null)
            {
                ThingWithComps eq = pawn.equipment.Primary;
                Log.Message(String.Format("AmmoPrimary: Pawn equipment {0}", eq));
                availableDefs = new List<ThingDef>();
                AddAvailableAmmoFor(eq);
            }
        }

        protected void AddAvailableAmmoFor(ThingWithComps eq)
        {
            if (eq == null || availableDefs == null)
            {
                return;
            }
            CompAmmoUser compAmmo = eq.TryGetComp<CompAmmoUser>();
            Log.Message(String.Format("AmmoPrimary: compAmmo {0}", compAmmo));
            if (compAmmo != null && !compAmmo.Props.ammoSet.ammoTypes.NullOrEmpty())
            {

                List<ThingDef> listammo = (from ThingDef g in compAmmo.Props.ammoSet.ammoTypes
                                           where g.canBeSpawningInventory
                                           select g).ToList<ThingDef>();
                Log.Message(String.Format("AmmoPrimary: listammo {0}", String.Join(", ", (from x in listammo select x.ToString()).ToArray())));
                if (!listammo.NullOrEmpty())
                {
                    ThingDef randomammo = GenCollection.RandomElement<ThingDef>(listammo);
                    Log.Message(String.Format("AmmoPrimary: randomammo {0} ({1})", randomammo, randomammo.canBeSpawningInventory));
                    availableDefs.Add(randomammo);
                }
                else return;
            }
        }

        protected override float GetWeightForDef(ThingDef def)
        {
            float weight = 1;
            AmmoDef ammo = def as AmmoDef;
            if (ammo != null && ammo.ammoClass.advanced)
                weight *= 0.2f;
            return weight;
        }
    }
}
