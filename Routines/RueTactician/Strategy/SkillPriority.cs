using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ExilePrecision.Core.Combat.Skills;
using ExilePrecision.Features.Targeting.EntityInformation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExilePrecision.Routines.RueTactician.Strategy
{
    public class SkillPriority
    {
        private readonly GameController _gameController;
        private readonly HashSet<string> _trackedSkills = new()
        {
            "GalvanicShardsAmmoPlayer",
            "GalvanicShardsPlayer",
            "StormblastBoltsAmmoPlayer",
            "StormblastBoltsPlayer",
            "MeleeCrossbowPlayer"
           
        };

        private const float NEARBY_MONSTER_RANGE = 40.0f;
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
            var player = _gameController.Player;

            var galvanicShardsAmmo = FindSkill(availableSkills, "GalvanicShardsAmmoPlayer");
            var galvanicShards = FindSkill(availableSkills, "GalvanicShardsPlayer");
            var stormblastBoltsAmmo = FindSkill(availableSkills, "StormblastBoltsAmmoPlayer");
            var stormblastBolts = FindSkill(availableSkills, "StormblastBoltsPlayer");
            var meleeCrossbow = FindSkill(availableSkills, "MeleeCrossbowPlayer");

            // ============================================================
            // 1) CHECK IF TARGET HAS 5+ ENEMIES NEARBY
            // ============================================================
            int nearbyEnemyCount = CountNearbyMonstersAroundTarget(target.Entity);

            // ============================================================
            // 2) IF 5+ ENEMIES, USE GALVANIC SHARDS
            // ============================================================
            if (nearbyEnemyCount >= 5)
            {
                // Load Galvanic Shards ammo first
                if (galvanicShardsAmmo != null)
                {
                    if (galvanicShardsAmmo.Skill.SkillUseStage == 3 && skillMonitor.CanUseSkill(galvanicShardsAmmo))
                        return galvanicShardsAmmo;
                }

                // Fire Galvanic Shards
                if (galvanicShards != null && skillMonitor.CanUseSkill(galvanicShards))
                    return galvanicShards;


                if (meleeCrossbow != null && skillMonitor.CanUseSkill(meleeCrossbow))
                    return meleeCrossbow;
            }

            // ============================================================
            // 3) OTHERWISE, USE STORMBLAST BOLTS
            // ============================================================
            // Load Stormblast Bolts ammo first
            if (stormblastBoltsAmmo != null)
            {
                if (stormblastBoltsAmmo.Skill.SkillUseStage == 3 && skillMonitor.CanUseSkill(stormblastBoltsAmmo))
                    return stormblastBoltsAmmo;
            }

            // Fire Stormblast Bolts
            if (stormblastBolts != null && skillMonitor.CanUseSkill(stormblastBolts))
                return stormblastBolts;


            if (meleeCrossbow != null && skillMonitor.CanUseSkill(meleeCrossbow))
                return meleeCrossbow;

            // ============================================================
            // 4) FALLBACK - Melee Crossbow
            // ============================================================
            if (meleeCrossbow != null && skillMonitor.CanUseSkill(meleeCrossbow))
                return meleeCrossbow;

            return null;
        }

        private ActiveSkill DetermineNormalMonsterSkill(
            EntityInfo target,
            List<ActiveSkill> availableSkills,
            SkillMonitor skillMonitor)
        {
            var player = _gameController.Player;

            var galvanicShardsAmmo = FindSkill(availableSkills, "GalvanicShardsAmmoPlayer");
            var galvanicShards = FindSkill(availableSkills, "GalvanicShardsPlayer");
            var stormblastBoltsAmmo = FindSkill(availableSkills, "StormblastBoltsAmmoPlayer");
            var stormblastBolts = FindSkill(availableSkills, "StormblastBoltsPlayer");
            var meleeCrossbow = FindSkill(availableSkills, "MeleeCrossbowPlayer");

            // ============================================================
            // 1) ALWAYS USE GALVANIC SHARDS FOR NORMAL MONSTERS
            // ============================================================
            // Load Galvanic Shards ammo first
            if (galvanicShardsAmmo != null)
            {
                if (galvanicShardsAmmo.Skill.SkillUseStage == 3 && skillMonitor.CanUseSkill(galvanicShardsAmmo))
                    return galvanicShardsAmmo;
            }

            // Fire Galvanic Shards
            if (galvanicShards != null && skillMonitor.CanUseSkill(galvanicShards))
                return galvanicShards;            
                
       
            if (meleeCrossbow != null && skillMonitor.CanUseSkill(meleeCrossbow))
                return meleeCrossbow;

            // ============================================================
            // 2) FALLBACK - Stormblast Bolts if Galvanic not available
            // ============================================================
            // Load Stormblast Bolts ammo first
            if (stormblastBoltsAmmo != null)
            {
                if (stormblastBoltsAmmo.Skill.SkillUseStage == 3 && skillMonitor.CanUseSkill(stormblastBoltsAmmo))
                    return stormblastBoltsAmmo;
            }

            // Fire Stormblast Bolts
            if (stormblastBolts != null && skillMonitor.CanUseSkill(stormblastBolts))
                return stormblastBolts;

            // ============================================================
            // 3) FALLBACK - Melee Crossbow
            // ============================================================
            if (meleeCrossbow != null && skillMonitor.CanUseSkill(meleeCrossbow))
                return meleeCrossbow;

            return null;
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================

        private int CountNearbyMonstersAroundTarget(Entity target)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Type == EntityType.Monster)
                    .Where(x => x.IsAlive)
                    .Where(x => x != target) // Don't count the target itself
                    .Count(x => x.Distance(target) <= NEARBY_MONSTER_RANGE);
            }
            catch
            {
                return 0;
            }
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
       











    }
}