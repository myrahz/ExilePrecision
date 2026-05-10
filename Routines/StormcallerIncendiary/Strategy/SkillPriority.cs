using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ExilePrecision.Core.Combat.Skills;
using ExilePrecision.Features.Targeting.EntityInformation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExilePrecision.Routines.StormcallerIncendiary.Strategy
{
    public class SkillPriority
    {
        private readonly GameController _gameController;
        private readonly HashSet<string> _trackedSkills = new()
        {
            "IceTippedArrowsPlayer",
            "WarBannerPlayer",
            "FrostBombPlayer",
            "IncendiaryShotAmmoPlayer",
            "BarragePlayer",
            "IncendiaryShotPlayer",
            "StormcallerArrowPlayer",
            "MeleeCrossbowPlayer",
            "LightningArrowPlayer",
            "LightningRodPlayer",
            "OrbOfStormsPlayer",
            "VoltaicMarkPlayer",
            "FreezingSalvoPlayer",
            "BarragePlayer",
            "MeleeBowPlayer",
            "ContagionPlayer"
        };

        private const float NEARBY_MONSTER_RADIUS = 30.0f;
        private const float STORM_CLOUD_RADIUS = 30.0f;
        private const float EFFECTIVE_ORB_RANGE = 35.0f;

        public SkillPriority(GameController gameController)
        {
            _gameController = gameController;
        }

        public ActiveSkill GetNextSkill(
            EntityInfo target,
            IReadOnlyCollection<ActiveSkill> availableSkills,
            SkillMonitor skillMonitor)
        {
            var skills = availableSkills.Where(s => _trackedSkills.Contains(s.Name)).ToList();
            if (!skills.Any() || target == null)
                return null;

            if (target.Rarity is MonsterRarity.Unique or MonsterRarity.Rare)
                return DetermineEliteMonsterSkill(target, skills, skillMonitor);

            return DetermineNormalMonsterSkill(target, skills, skillMonitor);
        }

        private ActiveSkill DetermineEliteMonsterSkill(
            EntityInfo target,
            List<ActiveSkill> availableSkills,
            SkillMonitor skillMonitor)
        {



            if (_gameController.Player.GetComponent<Player>().Level < 7)
            {
                if(CountNearbyMonsters(target.Entity) > 1)
                {
                    var lightningArrow = FindSkill(availableSkills, "LightningArrowPlayer");
                    if (lightningArrow != null && skillMonitor.CanUseSkill(lightningArrow))
                        return lightningArrow;

                }

                var stormCallerArrow = FindSkill(availableSkills, "StormcallerArrowPlayer");
                if (stormCallerArrow != null && skillMonitor.CanUseSkill(stormCallerArrow))
                    return stormCallerArrow;

                var XbowShot = FindSkill(availableSkills, "MeleeCrossbowPlayer");
                if (XbowShot != null && skillMonitor.CanUseSkill(XbowShot))
                    return XbowShot;


                var bowShot = FindSkill(availableSkills, "MeleeBowPlayer");
                if (bowShot != null && skillMonitor.CanUseSkill(bowShot))
                    return bowShot;

            }
            //var frostbomb = FindSkill(availableSkills, "FrostBombPlayer");
            //if (frostbomb != null && skillMonitor.CanUseSkill(frostbomb))
            //            return frostbomb;
  

            var orbOfStorms = FindSkill(availableSkills, "OrbOfStormsPlayer");

            // mark if I have it
            if (!HasVoltaicMark(target.Entity))
            {
                var voltaic = FindSkill(availableSkills, "VoltaicMarkPlayer");
                if (voltaic != null && skillMonitor.CanUseSkill(voltaic))
                    return voltaic;
            }


            // freezing salvo if is ready



            if (target.Rarity == MonsterRarity.Unique || target.Rarity == MonsterRarity.Rare)
            {
                var freezingSalvo = FindSkill(availableSkills, "FreezingSalvoPlayer"); 
				if (freezingSalvo != null)
				{
					if(skillMonitor.CanUseSkill(freezingSalvo) && FreezingSalvoCharges() >= 10)
                    {
                        return freezingSalvo;
					}
					
				}



                // incendiary shot if I can



                var IncendiaryShotAmmo = FindSkill(availableSkills, "IncendiaryShotAmmoPlayer");

                if (IncendiaryShotAmmo != null)
                {
                    if (IncendiaryShotAmmo.Skill.SkillUseStage == 3 && skillMonitor.CanUseSkill(IncendiaryShotAmmo))
                        return IncendiaryShotAmmo;
                }


                var IncendiaryShot = FindSkill(availableSkills, "IncendiaryShotPlayer");
                if (IncendiaryShot != null && skillMonitor.CanUseSkill(IncendiaryShot))
                    return IncendiaryShot;


                // barrage if I can
                var barrage = FindSkill(availableSkills, "BarragePlayer");
                if (barrage != null && skillMonitor.CanUseSkill(barrage) && !hasBarrageBuff())
                    return barrage;
                // stormcaller arrow if I can

                var stormCallerArrow = FindSkill(availableSkills, "StormcallerArrowPlayer");
                if (stormCallerArrow != null && skillMonitor.CanUseSkill(stormCallerArrow))
                    return stormCallerArrow;

                // ORB OF STORMS LR LA TECH


                //var stormCallerArrowAux = FindSkill(availableSkills, "StormcallerArrowPlayer");
                //if (HasNearbyStormCloud(target.Entity) && stormCallerArrowAux == null && !skillMonitor.CanUseSkill(stormCallerArrowAux))
                //if (HasNearbyStormCloud(target.Entity))
                //{
                //    var lightningRod = FindSkill(availableSkills, "LightningRodPlayer");
                //    if (lightningRod != null && skillMonitor.CanUseSkill(lightningRod))
                //        return lightningRod;
                //}
				if (orbOfStorms != null)
                {
					if (target.Distance <= EFFECTIVE_ORB_RANGE )
					{
						
						if (orbOfStorms != null && skillMonitor.CanUseSkill(orbOfStorms))
							return orbOfStorms;
					}
				}else{
					if (ShouldUseLightningRod(target.Entity))
					{
						var lightningRod = FindSkill(availableSkills, "LightningRodPlayer");
						if (lightningRod != null && skillMonitor.CanUseSkill(lightningRod))
							return lightningRod;
					}


                    
                    



                    var lightningArrow = FindSkill(availableSkills, "LightningArrowPlayer");
					if (lightningArrow != null && skillMonitor.CanUseSkill(lightningArrow))
						return lightningArrow;




                }
            }
            //else 
            //{
            //    if (ShouldUseLightningRod(target.Entity))
            //    {
            //        var lightningRod = FindSkill(availableSkills, "LightningRodPlayer");
            //        if (lightningRod != null && skillMonitor.CanUseSkill(lightningRod))
            //            return lightningRod;
            //    }

            //    var barrage = FindSkill(availableSkills, "BarragePlayer");
            //    if (barrage != null && skillMonitor.CanUseSkill(barrage) && !hasBarrageBuff())
            //        return barrage;

               

            //    var lightningArrow = FindSkill(availableSkills, "LightningArrowPlayer");
            //    if (lightningArrow != null && skillMonitor.CanUseSkill(lightningArrow))
            //        return lightningArrow;


            //    var bowShot2 = FindSkill(availableSkills, "MeleeBowPlayer");
            //    if (bowShot2 != null && skillMonitor.CanUseSkill(bowShot2))
            //        return bowShot2;

            //    var XbowShot2 = FindSkill(availableSkills, "MeleeCrossbowPlayer");
            //    if (XbowShot2 != null && skillMonitor.CanUseSkill(XbowShot2))
            //        return XbowShot2;
            //}


            var bowShot2 = FindSkill(availableSkills, "MeleeBowPlayer");
            if (bowShot2 != null && skillMonitor.CanUseSkill(bowShot2))
                return bowShot2;

            var XbowShot2 = FindSkill(availableSkills, "MeleeCrossbowPlayer");
            if (XbowShot2 != null && skillMonitor.CanUseSkill(XbowShot2))
                return XbowShot2;
            return null;
        }

        private ActiveSkill DetermineNormalMonsterSkill(
            EntityInfo target,
            List<ActiveSkill> availableSkills,
            SkillMonitor skillMonitor)
        {

            if (_gameController.Player.GetComponent<Player>().Level < 8 )
            {
                var contagion = FindSkill(availableSkills, "ContagionPlayer");
                if (contagion != null && skillMonitor.CanUseSkill(contagion) && !AnyNearbyMonsterHasContagion(target.Entity) && CountNearbyMonsters(target.Entity) > 1)
                    return contagion;

            }

            var freezingSalvo = FindSkill(availableSkills, "FreezingSalvoPlayer");
            if (freezingSalvo != null && CountNearbyMonsters(target.Entity) > 5)
            {
                if (skillMonitor.CanUseSkill(freezingSalvo) && FreezingSalvoCharges() >= 10)
                {
                    return freezingSalvo;
                }
            }

            if (CountNearbyMonsters(target.Entity) <1 && _gameController.Player.GetComponent<Player>().Level < 28 )
            {
                var IncendiaryShotAmmo = FindSkill(availableSkills, "IncendiaryShotAmmoPlayer");

                if (IncendiaryShotAmmo != null)
                {
                    if (IncendiaryShotAmmo.Skill.SkillUseStage == 3 && skillMonitor.CanUseSkill(IncendiaryShotAmmo))
                        return IncendiaryShotAmmo;
                }


                var IncendiaryShot = FindSkill(availableSkills, "IncendiaryShotPlayer");
                if (IncendiaryShot != null && skillMonitor.CanUseSkill(IncendiaryShot))
                    return IncendiaryShot;

                if (_gameController.Player.GetComponent<Player>().Level < 7)
                {
                    var XbowShot2 = FindSkill(availableSkills, "MeleeCrossbowPlayer");
                    if (XbowShot2 != null && skillMonitor.CanUseSkill(XbowShot2))
                        return XbowShot2;


                    var bowShot2 = FindSkill(availableSkills, "MeleeBowPlayer");
                    if (bowShot2 != null && skillMonitor.CanUseSkill(bowShot2))
                        return bowShot2;
                }


            }



            //var barrage = FindSkill(availableSkills, "BarragePlayer");
            //if (barrage != null && skillMonitor.CanUseSkill(barrage) && !hasBarrageBuff())
            //    return barrage;

            //var itarrows = FindSkill(availableSkills, "IceTippedArrowsPlayer");
            //if (itarrows != null && skillMonitor.CanUseSkill(itarrows) && !hasITArrowsBuff())
            //    return itarrows;

            var stormCallerArrow = FindSkill(availableSkills, "StormcallerArrowPlayer");
            if (stormCallerArrow != null && skillMonitor.CanUseSkill(stormCallerArrow))
                return stormCallerArrow;

            if (ShouldUseLightningRod(target.Entity))
            {
                var lightningRod = FindSkill(availableSkills, "LightningRodPlayer");
                if (lightningRod != null && skillMonitor.CanUseSkill(lightningRod))
                    return lightningRod;
            }

            var lightningArrow = FindSkill(availableSkills, "LightningArrowPlayer");
            if (lightningArrow != null && skillMonitor.CanUseSkill(lightningArrow))
                return lightningArrow;

            return null;
        }

        private ActiveSkill FindSkill(List<ActiveSkill> skills, string skillName)
        {
            return skills.FirstOrDefault(x => x.Name == skillName);
        }

        private bool HasVoltaicMark(Entity target)
        {
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs))
                    return false;

                return buffs.BuffsList?.Any(buff => buff.Name == "thaumaturgist_mark") ?? false;
            }
            catch (Exception)
            {
                return false;
            }
        }
        private bool HasContagion(Entity target)
        {
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs))
                    return false;

                return buffs.BuffsList?.Any(buff => buff.Name == "contagion") ?? false;
            }
            catch (Exception)
            {
                return false;
            }
        }
        private bool hasBarrageBuff()
        {
            var player = _gameController.Player;
            try
            {
                if (!player.TryGetComponent<Buffs>(out var buffs))
                    return false;

                return buffs.BuffsList?.Any(buff => buff.Name == "empower_barrage_visual") ?? false;
            }
            catch (Exception)
            {
                return false;
            }

        }        
        private bool hasITArrowsBuff()
        {
            var player = _gameController.Player;
            try
            {
                if (!player.TryGetComponent<Buffs>(out var buffs))
                    return false;

                return buffs.BuffsList?.Any(buff => buff.Name == "shearing_bolts") ?? false;
            }
            catch (Exception)
            {
                return false;
            }

        }
        private bool HasNearbyStormCloud(Entity target)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Path?.Contains("Metadata/Effects/SleepableEffect") ?? false)
                    .Where(HasStormCloudAnimation)
                    .Any(x => x.Distance(target) <= STORM_CLOUD_RADIUS);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool HasStormCloudAnimation(Entity entity)
        {
            try
            {
                if (!entity.TryGetComponent<Animated>(out var animated))
                    return false;

                return animated?.MiscAnimated?.Id == "StormCloudRangeIndicator";
            }
            catch (Exception)
            {
                return false;
            }
        }
        private int CountNearbyLightningRods(Entity target)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Path?.Contains("Metadata/MiscellaneousObjects/LightningRod") ?? false)
                    .Count(x => x.Distance(target) <= NEARBY_MONSTER_RADIUS);
            }
            catch (Exception)
            {
                return 0;
            }
        }
        private int CountNearbyMonsters(Entity target)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => (x?.Type.Equals(EntityType.Monster) ?? false) && (x?.IsAlive ?? false) && (x?.IsTargetable ?? false))
                    .Count(x => x.Distance(target) <= NEARBY_MONSTER_RADIUS) - 1; // minus one to remove the monster itself
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private bool ShouldUseLightningRod(Entity target)
        {
            try
            {
                if (target?.Rarity is MonsterRarity.White or MonsterRarity.Magic)
                    return false;

                if(target?.Rarity is MonsterRarity.Unique && CountNearbyLightningRods(target) < 8)
                {
                    return true;
                }
                return !HasNearbyLightningRod(target);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private int FreezingSalvoCharges()
        {

            // support_static_charge
            var player = _gameController.Player;
            try
            {
                if (!player.TryGetComponent<Buffs>(out var buffs))
                    return 0;
                var freezingSalvoBuff = buffs.BuffsList?.FirstOrDefault(buff => buff.Name == "freezing_salvo_seals");

                return freezingSalvoBuff?.BuffCharges ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private bool HasNearbyLightningRod(Entity target)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Path?.Contains("Metadata/MiscellaneousObjects/LightningRod") ?? false)
                    .Any(x => x.Distance(target) <= NEARBY_MONSTER_RADIUS);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool AnyNearbyMonsterHasContagion(Entity target)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Type.Equals(EntityType.Monster) ?? false)
                    .Where(x => x?.IsAlive ?? false)
                    .Where(x => x.Distance(target) <= NEARBY_MONSTER_RADIUS*2)
                    .Any(monster =>
                    {
                        if (!monster.TryGetComponent<Buffs>(out var buffs))
                            return false;
                        return buffs.BuffsList?.Any(buff => buff.Name == "contagion") ?? false;
                    });
            }
            catch (Exception)
            {
                return false;
            }
        }




    }
}