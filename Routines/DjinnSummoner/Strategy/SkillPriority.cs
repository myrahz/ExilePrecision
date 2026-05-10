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

namespace ExilePrecision.Routines.DjinnSummoner.Strategy
{
    public class SkillPriority
    {


        private bool _useExplosiveNextNext = true;

        private int _djinnSkillRotation = 0; // 0=kelarisBrutality, 1=kelarisDeception, 2=navirasFracturing
        private long _lastFrostBombCastTime = 0;
        private bool _frostBombFirstCast = true;
        private long _lastEleWeakCastTime = 0;
        private bool _eleWeakFirstCast = true;

        // CONFIGS HERE
        private bool castFusiladeOnNormalMonsters = false;
        private int orbLimit = 5;
        private int CHILLED_GROUND_RADIUS = 30;
        private int ORB_OF_STORM_RADIUS = 30;
        private int MAX_DISTANCE_FOR_NON_ARC = 70;
        private const float NEARBY_MONSTER_RADIUS = 30.0f;


        private long CurrentTime => Environment.TickCount64;
        private readonly GameController _gameController;
        private readonly HashSet<string> _trackedSkills = new()
        {
            "ContagionPlayer",
            "EssenceDrainPlayer",
            "FlameWallPlayer",
            "CommandSkeletalSniperPlayer",
            "CommandFireDjinnFireRunesPlayer",
            "CommandFireDjinnLivingBombPlayer",
            "CommandSandDjinnKnifeThrowPlayer",
            "FrostBombPlayer",
            "IceNovaPlayer",
            "ElementalWeaknessPlayer",
            "CommandWaterDjinnGroundBurstPlayer",
            "CommandWaterDjinnBubblePlayer",
            "CommandSandDjinnExplosiveTeleportPlayer"

            



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

        // Add this class-level field to track alternating state

        // Add this class-level field to track alternating state
        // Add this class-level field to track alternating state
        //private bool _useKnivesNext = true;
        //private int _djinnSkillRotation = 0; // 0=knives, 1=livingBomb, 2=fireRunes, 3=teleport

        //private ActiveSkill DetermineEliteMonsterSkill(
        //    EntityInfo target,
        //    List<ActiveSkill> availableSkills,
        //    SkillMonitor skillMonitor)
        //{
        //    var player = _gameController.Player;
        //    Vector2 interpolatedPosition = Vector2.Lerp(player.GridPos, target.GridPos, 0.5f);

        //    var essencedrain = FindSkill(availableSkills, "EssenceDrainPlayer");
        //    var contagion = FindSkill(availableSkills, "ContagionPlayer");
        //    var flamewall = FindSkill(availableSkills, "FlameWallPlayer");
        //    var commandSniper = FindSkill(availableSkills, "CommandSkeletalSniperPlayer");
        //    var knives = FindSkill(availableSkills, "CommandSandDjinnKnifeThrowPlayer");
        //    var teleport = FindSkill(availableSkills, "CommandSandDjinnExplosiveTeleportPlayer");
        //    var fireRunes = FindSkill(availableSkills, "CommandFireDjinnFireRunesPlayer");
        //    var livingBomb = FindSkill(availableSkills, "CommandFireDjinnLivingBombPlayer");

        //    // ============================================================
        //    // 1) CAST CONTAGION if no nearby monster has it and pack size > 1
        //    // ============================================================
        //    if (contagion != null && skillMonitor.CanUseSkill(contagion) &&
        //        !AnyNearbyMonsterHasContagion(target.Entity) &&
        //        CountNearbyMonsters(target.Entity) > 1)
        //        return contagion;

        //    // ============================================================
        //    // 2) CAST ESSENCE DRAIN on elite if it doesn't have essence drain
        //    //    AND no normal monster nearby has both contagion + essence drain
        //    // ============================================================
        //    if (essencedrain != null && skillMonitor.CanUseSkill(essencedrain))
        //    {
        //        bool eliteHasEssenceDrain = HasEssenceDrain(target.Entity);
        //        bool anyNormalHasBoth = AnyNearbyNormalMonsterHasBothBuffs(target.Entity);

        //        if (!eliteHasEssenceDrain && !anyNormalHasBoth)
        //            return essencedrain;
        //    }

        //    // ============================================================
        //    // 3) ROTATE BETWEEN DJINN SKILLS: Knives -> Living Bomb -> Fire Runes -> Teleport
        //    // ============================================================
        //    int startRotation = _djinnSkillRotation;

        //    for (int attempt = 0; attempt < 4; attempt++)
        //    {
        //        int currentRotation = (_djinnSkillRotation + attempt) % 4;
        //        ActiveSkill selectedSkill = null;
        //        bool shouldIncrement = false;

        //        switch (currentRotation)
        //        {
        //            case 0: // Knives
        //                if (knives != null && skillMonitor.CanUseSkill(knives))
        //                {
        //                    selectedSkill = knives;
        //                    shouldIncrement = true;
        //                }
        //                else if (knives != null) // Skill exists but can't use (cooldown/charges)
        //                {
        //                    shouldIncrement = true;
        //                }
        //                break;

        //            case 1: // Living Bomb (only if target doesn't have it)
        //                if (livingBomb != null && skillMonitor.CanUseSkill(livingBomb))
        //                {
        //                    if (!HasLivingBomb(target.Entity))
        //                    {
        //                        selectedSkill = livingBomb;
        //                    }
        //                    shouldIncrement = true; // Always increment even if target has bomb
        //                }
        //                else if (livingBomb != null) // Skill exists but can't use
        //                {
        //                    shouldIncrement = true;
        //                }
        //                break;

        //            case 2: // Fire Runes
        //                if (fireRunes != null && skillMonitor.CanUseSkill(fireRunes))
        //                {
        //                    selectedSkill = fireRunes;
        //                    shouldIncrement = true;
        //                }
        //                else if (fireRunes != null) // Skill exists but can't use
        //                {
        //                    shouldIncrement = true;
        //                }
        //                break;

        //            case 3: // Teleport
        //                if (teleport != null && skillMonitor.CanUseSkill(teleport))
        //                {
        //                    selectedSkill = teleport;
        //                    shouldIncrement = true;
        //                }
        //                else if (teleport != null) // Skill exists but can't use
        //                {
        //                    shouldIncrement = true;
        //                }
        //                break;
        //        }

        //        if (shouldIncrement)
        //        {
        //            _djinnSkillRotation = (currentRotation + 1) % 4;

        //            if (selectedSkill != null)
        //                return selectedSkill;
        //            else
        //                break; // Skill in rotation but can't use, skip to fallback
        //        }
        //    }

        //    // Fallback: If no skill in rotation was available, try any djinn skill
        //    if (knives != null && skillMonitor.CanUseSkill(knives))
        //        return knives;
        //    if (teleport != null && skillMonitor.CanUseSkill(teleport))
        //        return teleport;
        //    if (fireRunes != null && skillMonitor.CanUseSkill(fireRunes))
        //        return fireRunes;
        //    if (livingBomb != null && skillMonitor.CanUseSkill(livingBomb) && !HasLivingBomb(target.Entity))
        //        return livingBomb;

        //    // ============================================================
        //    // 4) FLAME WALL (not close to target)
        //    // ============================================================
        //    if (flamewall != null && skillMonitor.CanUseSkill(flamewall) &&
        //        !IsFlameWallCloseToTarget(target.Entity, 15))
        //        return flamewall;

        //    // ============================================================
        //    // 5) COMMAND SNIPER
        //    // ============================================================
        //    if (commandSniper != null && skillMonitor.CanUseSkill(commandSniper))
        //        return commandSniper;

        //    // ============================================================
        //    // 6) FLAME WALL (fallback)
        //    // ============================================================
        //    if (flamewall != null && skillMonitor.CanUseSkill(flamewall))
        //        return flamewall;

        //    return null;
        //}

        //private ActiveSkill DetermineNormalMonsterSkill(
        //    EntityInfo target,
        //    List<ActiveSkill> availableSkills,
        //    SkillMonitor skillMonitor)
        //{
        //    var player = _gameController.Player;

        //    var essencedrain = FindSkill(availableSkills, "EssenceDrainPlayer");
        //    var contagion = FindSkill(availableSkills, "ContagionPlayer");
        //    var flamewall = FindSkill(availableSkills, "FlameWallPlayer");
        //    var commandSniper = FindSkill(availableSkills, "CommandSkeletalSniperPlayer");
        //    var knives = FindSkill(availableSkills, "CommandSandDjinnKnifeThrowPlayer");
        //    var teleport = FindSkill(availableSkills, "CommandSandDjinnExplosiveTeleportPlayer");
        //    var fireRunes = FindSkill(availableSkills, "CommandFireDjinnFireRunesPlayer");
        //    var livingBomb = FindSkill(availableSkills, "CommandFireDjinnLivingBombPlayer");

        //    // ============================================================
        //    // 1) CAST CONTAGION if no nearby monster has it and pack size > 1
        //    // ============================================================
        //    if (contagion != null && skillMonitor.CanUseSkill(contagion) &&
        //        !AnyNearbyMonsterHasContagion(target.Entity) &&
        //        CountNearbyMonsters(target.Entity) > 1)
        //        return contagion;

        //    // ============================================================
        //    // 2) CAST ESSENCE DRAIN on normal if it doesn't have essence drain
        //    //    AND no other normal monster nearby has both contagion + essence drain
        //    // ============================================================
        //    if (essencedrain != null && skillMonitor.CanUseSkill(essencedrain))
        //    {
        //        bool targetHasEssenceDrain = HasEssenceDrain(target.Entity);
        //        bool anyOtherNormalHasBoth = AnyOtherNearbyNormalMonsterHasBothBuffs(target.Entity);

        //        if (!targetHasEssenceDrain && !anyOtherNormalHasBoth)
        //            return essencedrain;
        //    }

        //    // ============================================================
        //    // 3) ROTATE BETWEEN DJINN SKILLS: Knives -> Living Bomb -> Fire Runes -> Teleport
        //    // ============================================================
        //    int startRotation = _djinnSkillRotation;

        //    for (int attempt = 0; attempt < 4; attempt++)
        //    {
        //        int currentRotation = (_djinnSkillRotation + attempt) % 4;
        //        ActiveSkill selectedSkill = null;
        //        bool shouldIncrement = false;

        //        switch (currentRotation)
        //        {
        //            case 0: // Knives
        //                if (knives != null && skillMonitor.CanUseSkill(knives))
        //                {
        //                    selectedSkill = knives;
        //                    shouldIncrement = true;
        //                }
        //                else if (knives != null) // Skill exists but can't use (cooldown/charges)
        //                {
        //                    shouldIncrement = true;
        //                }
        //                break;

        //            case 1: // Living Bomb (only if target doesn't have it)
        //                if (livingBomb != null && skillMonitor.CanUseSkill(livingBomb))
        //                {
        //                    if (!HasLivingBomb(target.Entity))
        //                    {
        //                        selectedSkill = livingBomb;
        //                    }
        //                    shouldIncrement = true; // Always increment even if target has bomb
        //                }
        //                else if (livingBomb != null) // Skill exists but can't use
        //                {
        //                    shouldIncrement = true;
        //                }
        //                break;

        //            case 2: // Fire Runes
        //                if (fireRunes != null && skillMonitor.CanUseSkill(fireRunes))
        //                {
        //                    selectedSkill = fireRunes;
        //                    shouldIncrement = true;
        //                }
        //                else if (fireRunes != null) // Skill exists but can't use
        //                {
        //                    shouldIncrement = true;
        //                }
        //                break;

        //            case 3: // Teleport
        //                if (teleport != null && skillMonitor.CanUseSkill(teleport))
        //                {
        //                    selectedSkill = teleport;
        //                    shouldIncrement = true;
        //                }
        //                else if (teleport != null) // Skill exists but can't use
        //                {
        //                    shouldIncrement = true;
        //                }
        //                break;
        //        }

        //        if (shouldIncrement)
        //        {
        //            _djinnSkillRotation = (currentRotation + 1) % 4;

        //            if (selectedSkill != null)
        //                return selectedSkill;
        //            else
        //                break; // Skill in rotation but can't use, skip to fallback
        //        }
        //    }

        //    // Fallback: If no skill in rotation was available, try any djinn skill
        //    if (knives != null && skillMonitor.CanUseSkill(knives))
        //        return knives;
        //    if (teleport != null && skillMonitor.CanUseSkill(teleport))
        //        return teleport;
        //    if (fireRunes != null && skillMonitor.CanUseSkill(fireRunes))
        //        return fireRunes;
        //    if (livingBomb != null && skillMonitor.CanUseSkill(livingBomb) && !HasLivingBomb(target.Entity))
        //        return livingBomb;

        //    // ============================================================
        //    // 4) FLAME WALL (not close to target)
        //    // ============================================================
        //    if (flamewall != null && skillMonitor.CanUseSkill(flamewall) &&
        //        !IsFlameWallCloseToTarget(target.Entity, 15))
        //        return flamewall;

        //    // ============================================================
        //    // 5) COMMAND SNIPER
        //    // ============================================================
        //    if (commandSniper != null && skillMonitor.CanUseSkill(commandSniper))
        //        return commandSniper;

        //    // ============================================================
        //    // 6) FLAME WALL (fallback)
        //    // ============================================================
        //    if (flamewall != null && skillMonitor.CanUseSkill(flamewall))
        //        return flamewall;

        //    return null;
        //}


        private ActiveSkill DetermineEliteMonsterSkill(
    EntityInfo target,
    List<ActiveSkill> availableSkills,
    SkillMonitor skillMonitor)
        {
            var player = _gameController.Player;
            Vector2 interpolatedPosition = Vector2.Lerp(player.GridPos, target.GridPos, 0.5f);

            var iceNova = FindSkill(availableSkills, "IceNovaPlayer");
            var frostBomb = FindSkill(availableSkills, "FrostBombPlayer");
            var eleWeak = FindSkill(availableSkills, "ElementalWeaknessPlayer");
            var navirasFracturing = FindSkill(availableSkills, "CommandWaterDjinnGroundBurstPlayer");
            var kelarisBrutality = FindSkill(availableSkills, "CommandSandDjinnKnifeThrowPlayer");
            var kelarisDeception = FindSkill(availableSkills, "CommandSandDjinnExplosiveTeleportPlayer");
            var kelarisBubble = FindSkill(availableSkills, "CommandWaterDjinnBubblePlayer");
            var commandSniper = FindSkill(availableSkills, "CommandSkeletalSniperPlayer");

            // ============================================================
            // 1) FROST BOMB 
            // ============================================================
            if (frostBomb != null && skillMonitor.CanUseSkill(frostBomb))
                return frostBomb;


            if (NearbyMonsterCount(target.Entity, 40) > 2)
            {
                // ============================================================
                // 0) NAVIRAS FRACTURING - CHILLED GROUND NEARBY Metadata/Effects/Spells/grd_Zones/grd_Chilled01.ao
                // ============================================================
                if (navirasFracturing != null && skillMonitor.CanUseSkill(navirasFracturing) && HasNearbyChilledGround(target.Entity))
                {
                    return navirasFracturing;
                }

                // ============================================================
                // 1) FROST BOMB - Use if we have no cold infusions for remnants
                // ============================================================
                int coldInfusions = ColdInfusions();

                if (coldInfusions == 0 && frostBomb != null && skillMonitor.CanUseSkill(frostBomb))
                {
                    return frostBomb;
                }


                // ============================================================
                // 2) ICE NOVA - Use when we have cold infusions
                // ============================================================
                if (coldInfusions > 0 && iceNova != null && skillMonitor.CanUseSkill(iceNova))
                {
                    return iceNova;
                }


            }


            // ============================================================
            // 2) ELEMENTAL WEAKNESS
            // ============================================================
            bool noWeakness = !HasElementalWeakness(target.Entity);
            const int eleWeakCooldown = 2000; // 2 seconds
            bool canCastEleWeak = _eleWeakFirstCast || (CurrentTime - _lastEleWeakCastTime >= eleWeakCooldown);

            if (eleWeak != null && skillMonitor.CanUseSkill(eleWeak) && canCastEleWeak)
            {

                bool shouldCast = false;



                if (noWeakness)
                    shouldCast = true;


                if (shouldCast)
                {
                    _lastEleWeakCastTime = CurrentTime;    // start cooldown timer
                    _eleWeakFirstCast = false;
                    return eleWeak;
                }
            }


            if (ColdInfusions() > 0 && iceNova != null && skillMonitor.CanUseSkill(iceNova) && !HasNearbyChilledGround(target.Entity))
            {
                return iceNova;
            }



            // ============================================================
            // 3) NAVIRAS FRACTURING - Use when Kelari skills on CD and has 10+ crit weakness
            // ============================================================
            bool kelariSkillsOnCD =
                (kelarisBrutality == null || !skillMonitor.CanUseSkill(kelarisBrutality)) &&
                (kelarisDeception == null || !skillMonitor.CanUseSkill(kelarisDeception));

            int critWeaknessStacks = GetCritWeaknessStacks(target.Entity);

            if (navirasFracturing != null && skillMonitor.CanUseSkill(navirasFracturing) && HasNearbyChilledGround(target.Entity) &&
                //kelariSkillsOnCD && critWeaknessStacks >= 10)
                kelariSkillsOnCD )
            {
                return navirasFracturing;
            }

            // ============================================================
            // 4) SPAM KELARIS BRUTALITY AND DECEPTION (alternating)
            // ============================================================
            bool canUseBrutality = kelarisBrutality != null && skillMonitor.CanUseSkill(kelarisBrutality);
            bool canUseDeception = kelarisDeception != null && skillMonitor.CanUseSkill(kelarisDeception);

            // Alternate between brutality and deception
            if (canUseBrutality && canUseDeception)
            {
                if (_djinnSkillRotation == 0)
                {
                    _djinnSkillRotation = 1;
                    return kelarisBrutality;
                }
                else
                {
                    _djinnSkillRotation = 0;
                    return kelarisDeception;
                }
            }

            // Use whichever is available
            if (canUseBrutality)
                return kelarisBrutality;

            if (canUseDeception)
                return kelarisDeception;

            // ============================================================
            // 5) COMMAND SNIPER
            // ============================================================
            if (commandSniper != null && skillMonitor.CanUseSkill(commandSniper))
                return commandSniper;

            return null;
        }

        private ActiveSkill DetermineNormalMonsterSkill(
            EntityInfo target,
            List<ActiveSkill> availableSkills,
            SkillMonitor skillMonitor)
        {
            var player = _gameController.Player;

            var iceNova = FindSkill(availableSkills, "IceNovaPlayer");
            var frostBomb = FindSkill(availableSkills, "FrostBombPlayer");
            var eleWeak = FindSkill(availableSkills, "ElementalWeaknessPlayer");
            var navirasFracturing = FindSkill(availableSkills, "CommandWaterDjinnGroundBurstPlayer");
            var kelarisBrutality = FindSkill(availableSkills, "CommandSandDjinnKnifeThrowPlayer");
            var kelarisDeception = FindSkill(availableSkills, "CommandSandDjinnExplosiveTeleportPlayer");
            var kelarisBubble = FindSkill(availableSkills, "CommandWaterDjinnBubblePlayer");

            // ============================================================
            // 0) NAVIRAS FRACTURING - CHILLED GROUND NEARBY Metadata/Effects/Spells/grd_Zones/grd_Chilled01.ao
            // ============================================================
            if (navirasFracturing != null && skillMonitor.CanUseSkill(navirasFracturing) && HasNearbyChilledGround(target.Entity))
            {
                return navirasFracturing;
            }

            // ============================================================
            // 1) FROST BOMB - Use if we have no cold infusions for remnants
            // ============================================================
            int coldInfusions = ColdInfusions();

            if (coldInfusions < 3 && frostBomb != null && skillMonitor.CanUseSkill(frostBomb))
            {
                return frostBomb;
            }


            // ============================================================
            // 2) ICE NOVA - Use when we have cold infusions
            // ============================================================
            if (coldInfusions > 0 && iceNova != null && skillMonitor.CanUseSkill(iceNova))
            {
                return iceNova;
            }


            // ============================================================
            // 3) NAVIRAS FRACTURING - Main clear skill
            // ============================================================
            //if (navirasFracturing != null && skillMonitor.CanUseSkill(navirasFracturing))
            //{
            //    return navirasFracturing;
            //}

            // ============================================================
            // 4) KELARIS BRUTALITY + DECEPTION
            // ============================================================

                bool canUseBrutality = kelarisBrutality != null && skillMonitor.CanUseSkill(kelarisBrutality);
                bool canUseDeception = kelarisDeception != null && skillMonitor.CanUseSkill(kelarisDeception);

                // Alternate between brutality and deception
                if (canUseBrutality && canUseDeception)
                {
                    if (_djinnSkillRotation == 0)
                    {
                        _djinnSkillRotation = 1;
                        return kelarisBrutality;
                    }
                    else
                    {
                        _djinnSkillRotation = 0;
                        return kelarisDeception;
                    }
                }

                if (canUseBrutality)
                    return kelarisBrutality;

                if (canUseDeception)
                    return kelarisDeception;
            

            // ============================================================
            // 5) FALLBACK - Frost Bomb
            // ============================================================
            if (frostBomb != null && skillMonitor.CanUseSkill(frostBomb))
                return frostBomb;

            return null;
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================
        private bool HasNearbyChilledGround(Entity target)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Path?.Contains("Metadata/Effects/Spells/ground_effects/VisibleServerGroundEffect") ?? false)
                    .Where(HasChilledGroundAnimation)
                    .Any(x => x.Distance(target) <= CHILLED_GROUND_RADIUS);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool HasChilledGroundAnimation(Entity entity)
        {
            try
            {
                if (!entity.TryGetComponent<Animated>(out var animated))
                    return false;

                return animated?.BaseAnimatedObjectEntity?.Path == "Metadata/Effects/Spells/grd_Zones/grd_Chilled01.ao";
            }
            catch (Exception)
            {
                return false;
            }
        }
        private int ColdInfusions()
        {
            var player = _gameController.Player;
            try
            {
                if (!player.TryGetComponent<Buffs>(out var buffs))
                    return 0;
                var charges = buffs.BuffsList?.FirstOrDefault(buff => buff.Name == "cold_infusion");
                return charges?.BuffCharges ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private int GetCritWeaknessStacks(Entity target)
        {
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                    return 0;

                var critWeaknessBuff = buffs.BuffsList.FirstOrDefault(b => b.Name == "critical_weakness");
                return critWeaknessBuff?.BuffCharges ?? 0;
            }
            catch
            {
                return 0;
            }
        }
        // ============================================================
        // HELPER METHODS (same as before)
        // ============================================================

        private bool HasEssenceDrain(Entity target)
        {
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                    return false;

                return buffs.BuffsList.Any(b => b.Name == "siphon_damage");
            }
            catch
            {
                return false;
            }
        }

        private bool HasLivingBomb(Entity target)
        {
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                    return false;

                return buffs.BuffsList.Any(b => b.Name == "living_bomb_count");
            }
            catch
            {
                return false;
            }
        }

        private bool HasBothContagionAndEssenceDrain(Entity target)
        {
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                    return false;

                bool hasContagion = buffs.BuffsList.Any(b => b.Name == "contagion");
                bool hasEssenceDrain = buffs.BuffsList.Any(b => b.Name == "siphon_damage");

                return hasContagion && hasEssenceDrain;
            }
            catch
            {
                return false;
            }
        }

        private bool AnyNearbyNormalMonsterHasBothBuffs(Entity eliteTarget)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Type == EntityType.Monster)
                    .Where(x => x.IsAlive)
                    .Where(x => x.Rarity != MonsterRarity.Rare && x.Rarity != MonsterRarity.Unique)
                    .Where(x => x.Distance(eliteTarget) <= NEARBY_MONSTER_RADIUS)
                    .Any(monster =>
                    {
                        if (!monster.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                            return false;

                        bool hasContagion = buffs.BuffsList.Any(b => b.Name == "contagion");
                        bool hasEssenceDrain = buffs.BuffsList.Any(b => b.Name == "siphon_damage");

                        return hasContagion && hasEssenceDrain;
                    });
            }
            catch
            {
                return false;
            }
        }

        private bool AnyOtherNearbyNormalMonsterHasBothBuffs(Entity normalTarget)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Type == EntityType.Monster)
                    .Where(x => x.IsAlive)
                    .Where(x => x != normalTarget)
                    .Where(x => x.Rarity != MonsterRarity.Rare && x.Rarity != MonsterRarity.Unique)
                    .Where(x => x.Distance(normalTarget) <= NEARBY_MONSTER_RADIUS)
                    .Any(monster =>
                    {
                        if (!monster.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                            return false;

                        bool hasContagion = buffs.BuffsList.Any(b => b.Name == "contagion");
                        bool hasEssenceDrain = buffs.BuffsList.Any(b => b.Name == "siphon_damage");

                        return hasContagion && hasEssenceDrain;
                    });
            }
            catch
            {
                return false;
            }
        }
        private ActiveSkill FindSkill(List<ActiveSkill> skills, string skillName)
        {
            return skills.FirstOrDefault(x => x.Name == skillName);
        }

       



        private bool HasElementalWeakness(Entity target)
        {
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs))
                    return false;

                return buffs.BuffsList?.Any(buff => buff.Name == "curse_elemental_weakness") ?? false;
            }
            catch
            {
                return false;
            }
        }

        private int NearbyMonsterCount(Entity target, float radius = 40f)
        {
            return _gameController.Entities
                .Count(x =>
                    x.Type == EntityType.Monster && x.IsHostile &&
                    x.IsAlive &&
                    x.Distance(target) <= radius);
        }
        private (bool release, int stage) GetFusilladeReleaseState(ActiveSkill fusillade)
        {
            int stage = fusillade?.Skill.SkillUseStage  ?? 0;

            bool release = false;

            if (stage <= 2)
                release = false;
            else if (stage >= 11)
                release = true;

            return (release, stage);
        }



        private bool HasOOSAnimation(Entity entity)
        {
            try
            {
                if (!entity.TryGetComponent<Animated>(out var animated))
                    return false;

                return animated?.MiscAnimated?.AOFile == "Metadata/Effects/Spells/lightning_orb_of_storms/orb_beam.ao";
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool IsFlameWallBetweenPlayerAndTarget(
        Entity player,
        Entity target,
        float flameWallSize)
        {
            // Get positions
            Vector2 playerPos = player.GridPos;
            Vector2 targetPos = target.GridPos;
            var flameWallEntities = _gameController.Entities
                    .Where(x => x?.Path?.Contains("Metadata/Monsters/Anomalies/Firewall") ?? false);
            // Direction vector from player to target
            Vector2 direction = targetPos - playerPos;
            float distance = direction.Length();

            // Normalize the direction vector
            Vector2 normalizedDirection = Vector2.Normalize(direction);

            foreach (Entity flameWall in flameWallEntities)
            {
                Vector2 flameWallPos = flameWall.GridPos;

                // Vector from player to flame wall
                Vector2 playerToFlameWall = flameWallPos - playerPos;

                // Project flame wall position onto the line between player and target
                float dotProduct = Vector2.Dot(playerToFlameWall, normalizedDirection);

                // If the projection is outside the line segment between player and target, skip
                if (dotProduct < 0 || dotProduct > distance)
                    continue;

                // Find the closest point on the line to the flame wall
                Vector2 closestPoint = playerPos + normalizedDirection * dotProduct;

                // Calculate distance from flame wall to the line
                float perpDistance = Vector2.Distance(closestPoint, flameWallPos);

                // If the distance is less than the flame wall size, we have intersection
                if (perpDistance <= flameWallSize)
                    return true;
            }

            return false;
        }
        public bool IsFlameWallCloseToTarget(Entity target, float closeRadius)
        {
            Vector2 targetPos = target.GridPos;

            return _gameController.Entities
                .Where(x => x?.Path?.Contains("Metadata/Monsters/Anomalies/Firewall") ?? false)
                .Any(flameWall =>
                {
                    float distance = Vector2.Distance(flameWall.GridPos, targetPos);
                    return distance <= closeRadius;
                });
        }
        private bool AnyNearbyMonsterHasContagion(Entity target)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Type.Equals(EntityType.Monster) ?? false)
                    .Where(x => x?.IsAlive ?? false)
                    .Where(x => x.Distance(target) <= NEARBY_MONSTER_RADIUS)
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

        private bool AnyNearbyMonsterHasContagionNoEssenceDrain(Entity target)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Type == EntityType.Monster)
                    .Where(x => x.IsAlive)
                    .Where(x => x.Distance(target) <= NEARBY_MONSTER_RADIUS)
                    .Any(monster =>
                    {
                        if (!monster.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                            return false;

                        bool hasContagion = buffs.BuffsList.Any(b => b.Name == "contagion");
                        bool hasSiphon = buffs.BuffsList.Any(b => b.Name == "siphon_damage");

                        return hasContagion && !hasSiphon;
                    });
            }
            catch
            {
                return false;
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
    }
}