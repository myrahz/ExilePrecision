using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ExilePrecision.Core.Combat.Skills;
using ExilePrecision.Features.Targeting.EntityInformation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace ExilePrecision.Routines.WyvernDruidLeveling.Strategy
{
    public class SkillPriority
    {
        private long _lastEleWeakCastTime = 0; 
        private bool _eleWeakFirstCast = true;

        private int _grenadeIndex = 0;  // 0 = Explosive, 1 = Gas, 2 = Oil

        // CONFIGS HERE
        private bool castFusiladeOnNormalMonsters = false;
        private int orbLimit = 2;
        private int NEARBY_MONSTER_RADIUS = 15;
        private int ORB_OF_STORM_RADIUS = 30;
        private int MAX_DISTANCE_FOR_NON_ARC = 70;

        private long CurrentTime => Environment.TickCount64;
        private readonly GameController _gameController;
        private readonly HashSet<string> _trackedSkills = new()
        {
            "WyvernRendPlayer",
            "WolfPouncePlayer",
            "WyvernDevourPlayer",
            "OilBarragePlayer",
            "FerociousRoarMetaPlayer"


        };

        


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
        private ActiveSkill FindSkill(List<ActiveSkill> skills, string skillName)
        {
            return skills.FirstOrDefault(x => x.Name == skillName);
        }

        private const float NEARBY_MONSTER_RANGE = 10f; // Adjust as needed
        private const float NEARBY_MONSTER_RANGE_WARCRY = 30f; // Adjust as needed
        private const float MELEE_RANGE = 10f; // Adjust as needed

        private ActiveSkill DetermineEliteMonsterSkill(
            EntityInfo target,
            List<ActiveSkill> availableSkills,
            SkillMonitor skillMonitor)
        {
            var player = _gameController.Player;

            var wyvernRend = FindSkill(availableSkills, "WyvernRendPlayer");
            var wolfPounce = FindSkill(availableSkills, "WolfPouncePlayer");
            var wyvernDevour = FindSkill(availableSkills, "WyvernDevourPlayer");
            var ferociousRoar = FindSkill(availableSkills, "FerociousRoarMetaPlayer");
            var oilBarrage = FindSkill(availableSkills, "OilBarragePlayer");

            var (currentCharges, chargeTime) = GetPowerCharges(player);


            // ============================================================
            // 1) WYVERN DEVOUR - For cullable Unique/Rare monsters
            // ============================================================
            var cullableUniqueOrRare = GetNearbyCullableUniqueOrRare(player);

            if (cullableUniqueOrRare != null && wyvernDevour != null && skillMonitor.CanUseSkill(wyvernDevour))
            {
                return wyvernDevour;
            }
            // ============================================================
            // 0) OIL BARRAGE
            // ============================================================


            if (currentCharges >=1 && skillMonitor.CanUseSkill(oilBarrage))
            {
                return oilBarrage;
            }

            // ============================================================
            // 2) WYVERN DEVOUR - For corpses or cullable monsters when not at max power charges
            // ============================================================
 
            bool hasMaxCharges = currentCharges >= 3;

            if (!hasMaxCharges && wyvernDevour != null && skillMonitor.CanUseSkill(wyvernDevour))
            {
                bool hasNearbyCorpse = HasNearbyCorpse(player);
                bool hasCullableMonster = HasNearbyCullableMonster(player);

                if (hasNearbyCorpse || hasCullableMonster)
                {
                    return wyvernDevour;
                }
            }

            // ============================================================
            // 3) WOLF POUNCE - If target is further than melee range
            // ============================================================
            float distanceToTarget = Vector2.Distance(player.GridPos, target.GridPos);

            if (distanceToTarget > MELEE_RANGE && wolfPounce != null && skillMonitor.CanUseSkill(wolfPounce))
            {
                return wolfPounce;
            }

            // ============================================================
            // 4) WOLF POUNCE - If target is Rare/Unique and doesn't have cross_slash_mark
            // ============================================================
            if ((target.Rarity == MonsterRarity.Rare || target.Rarity == MonsterRarity.Unique) &&
                !HasCrossSlashMark(target.Entity) &&
                wolfPounce != null && skillMonitor.CanUseSkill(wolfPounce))
            {
                return wolfPounce;
            }

            // ============================================================
            // 5) FEROCIOUS ROAR - If 5+ nearby monsters OR Rare/Unique nearby, and no empowered attacks
            // ============================================================
            int nearbyMonsterCount = CountNearbyMonsters(player);
            bool hasRareOrUniqueNearby = HasRareOrUniqueNearby(player);
            bool hasEmpoweredAttacks = HasEmpoweredAttacks(player);

            if ((nearbyMonsterCount > 5 || hasRareOrUniqueNearby) &&
                !hasEmpoweredAttacks &&
                ferociousRoar != null && skillMonitor.CanUseSkill(ferociousRoar))
            {
                return ferociousRoar;
            }

            // ============================================================
            // 6) WYVERN REND - Default spam skill
            // ============================================================
            if (wyvernRend != null && skillMonitor.CanUseSkill(wyvernRend))
            {
                return wyvernRend;
            }

            return null;
        }

        private ActiveSkill DetermineNormalMonsterSkill(
            EntityInfo target,
            List<ActiveSkill> availableSkills,
            SkillMonitor skillMonitor)
        {
            var player = _gameController.Player;

            var wyvernRend = FindSkill(availableSkills, "WyvernRendPlayer");
            var wolfPounce = FindSkill(availableSkills, "WolfPouncePlayer");
            var wyvernDevour = FindSkill(availableSkills, "WyvernDevourPlayer");
            var ferociousRoar = FindSkill(availableSkills, "FerociousRoarMetaPlayer");

            // ============================================================
            // 1) WYVERN DEVOUR - For cullable Unique/Rare monsters
            // ============================================================
            var cullableUniqueOrRare = GetNearbyCullableUniqueOrRare(player);

            if (cullableUniqueOrRare != null && wyvernDevour != null && skillMonitor.CanUseSkill(wyvernDevour))
            {
                return wyvernDevour;
            }

            // ============================================================
            // 2) WYVERN DEVOUR - For corpses or cullable monsters when not at max power charges
            // ============================================================
            var (currentCharges, chargeTime) = GetPowerCharges(player);
            bool hasMaxCharges = currentCharges >= 3;

            if (!hasMaxCharges && wyvernDevour != null && skillMonitor.CanUseSkill(wyvernDevour))
            {
                bool hasNearbyCorpse = HasNearbyCorpse(player);
                bool hasCullableMonster = HasNearbyCullableMonster(player);

                if (hasNearbyCorpse || hasCullableMonster)
                {
                    return wyvernDevour;
                }
            }

            // ============================================================
            // 3) WOLF POUNCE - If target is further than melee range
            // ============================================================
            float distanceToTarget = Vector2.Distance(player.GridPos, target.GridPos);

            if (distanceToTarget > MELEE_RANGE && wolfPounce != null && skillMonitor.CanUseSkill(wolfPounce))
            {
                return wolfPounce;
            }

            // ============================================================
            // 4) WOLF POUNCE - If target is Rare/Unique and doesn't have cross_slash_mark
            // ============================================================
            if ((target.Rarity == MonsterRarity.Rare || target.Rarity == MonsterRarity.Unique) &&
                !HasCrossSlashMark(target.Entity) &&
                wolfPounce != null && skillMonitor.CanUseSkill(wolfPounce))
            {
                return wolfPounce;
            }

            // ============================================================
            // 5) FEROCIOUS ROAR - If 5+ nearby monsters OR Rare/Unique nearby, and no empowered attacks
            // ============================================================
            int nearbyMonsterCount = CountNearbyMonsters(player);
            bool hasRareOrUniqueNearby = HasRareOrUniqueNearby(player);
            bool hasEmpoweredAttacks = HasEmpoweredAttacks(player);

            if ((nearbyMonsterCount > 5 || hasRareOrUniqueNearby) &&
                !hasEmpoweredAttacks &&
                ferociousRoar != null && skillMonitor.CanUseSkill(ferociousRoar))
            {
                return ferociousRoar;
            }

            // ============================================================
            // 6) WYVERN REND - Default spam skill
            // ============================================================
            if (wyvernRend != null && skillMonitor.CanUseSkill(wyvernRend))
            {
                return wyvernRend;
            }

            return null;
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================

        private (int charges, long remainingTime) GetPowerCharges(Entity player)
        {
            try
            {
                if (!player.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                    return (0, 0);

                var powerChargeBuff = buffs.BuffsList.FirstOrDefault(b => b.Name == "power_charge");
                if (powerChargeBuff == null)
                    return (0, 0);

                return (powerChargeBuff.BuffCharges, (long)powerChargeBuff.Timer);
            }
            catch
            {
                return (0, 0);
            }
        }

        private bool HasEmpoweredAttacks(Entity player)
        {
            try
            {
                if (!player.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                    return false;

                return buffs.BuffsList.Any(b => b.Name == "display_num_empowered_attacks");
            }
            catch
            {
                return false;
            }
        }

        private bool HasCrossSlashMark(Entity target)
        {
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                    return false;

                return buffs.BuffsList.Any(b => b.Name == "cross_slash_mark");
            }
            catch
            {
                return false;
            }
        }

        private bool IsCullable(Entity monster)
        {
            try
            {
                if (!monster.TryGetComponent<Life>(out var life))
                    return false;

                float hpPercent = life.HPPercentage;

                return monster.Rarity switch
                {
                    MonsterRarity.Unique => hpPercent <= 0.05f,
                    MonsterRarity.Rare => hpPercent <= 0.10f,
                    MonsterRarity.Magic => hpPercent <= 0.20f,
                    MonsterRarity.White => hpPercent <= 0.30f,
                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }

        private Entity GetNearbyCullableUniqueOrRare(Entity player)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Type == EntityType.Monster)
                    .Where(x => x.IsAlive)
                    .Where(x => x.Rarity == MonsterRarity.Unique || x.Rarity == MonsterRarity.Rare)
                    .Where(x => x.Distance(player) <= NEARBY_MONSTER_RANGE)
                    .FirstOrDefault(x => IsCullable(x));
            }
            catch
            {
                return null;
            }
        }

        private bool HasNearbyCorpse(Entity player)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Type == EntityType.Monster)
                    .Where(x => x.IsDead)
                    .Any(x => x.Distance(player) <= NEARBY_MONSTER_RANGE);
            }
            catch
            {
                return false;
            }
        }

        private bool HasNearbyCullableMonster(Entity player)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Type == EntityType.Monster)
                    .Where(x => x.IsAlive)
                    .Where(x => x.Distance(player) <= NEARBY_MONSTER_RANGE)
                    .Any(x => IsCullable(x));
            }
            catch
            {
                return false;
            }
        }

        private int CountNearbyMonsters(Entity player)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Type == EntityType.Monster)
                    .Where(x => x.IsAlive)
                    .Count(x => x.Distance(player) <= NEARBY_MONSTER_RANGE_WARCRY);
            }
            catch
            {
                return 0;
            }
        }

        private bool HasRareOrUniqueNearby(Entity player)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Type == EntityType.Monster)
                    .Where(x => x.IsAlive)
                    .Where(x => x.Rarity == MonsterRarity.Rare || x.Rarity == MonsterRarity.Unique)
                    .Any(x => x.Distance(player) <= NEARBY_MONSTER_RANGE);
            }
            catch
            {
                return false;
            }
        }
    }
}