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

namespace ExilePrecision.Routines.Grenades.Strategy
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
            "LightningArrowPlayer",
            "ExplosiveGrenadePlayer",
            "ToxicGrenadePlayer",
            "FlashGrenadePlayer",
            "FrostBombPlayer",
            "ElementalWeaknessPlayer",
            "OilGrenadePlayer",
            "ContagionPlayer",
            "MeleeCrossbowPlayer",
            "MeleeBowPlayer"


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

        private ActiveSkill DetermineEliteMonsterSkill(
            EntityInfo target,
            List<ActiveSkill> availableSkills,
            SkillMonitor skillMonitor)
        {
            var player = _gameController.Player;
            Vector2 interpolatedPosition = Vector2.Lerp(player.GridPos, target.GridPos, 0.5f);


            // Fetch all skills
            var lightningArrow = FindSkill(availableSkills, "LightningArrowPlayer");
            var contagion = FindSkill(availableSkills, "ContagionPlayer");
            var frostBomb = FindSkill(availableSkills, "FrostBombPlayer");
            var flashGrenade = FindSkill(availableSkills, "FlashGrenadePlayer");
            var elementalWeakness = FindSkill(availableSkills, "ElementalWeaknessPlayer");
            var bowShot = FindSkill(availableSkills, "MeleeBowPlayer");
            var xbowShot = FindSkill(availableSkills, "MeleeCrossbowPlayer");

            var explosiveGrenade = FindSkill(availableSkills, "ExplosiveGrenadePlayer");
            var gasGrenade = FindSkill(availableSkills, "ToxicGrenadePlayer");
            var oilGrenade = FindSkill(availableSkills, "OilGrenadePlayer");




            // ============================================================
            // 1) ELEMENTAL WEAKNESS
            // ============================================================
            bool noWeakness = !HasElementalWeakness(target.Entity);
            const int eleWeakCooldown = 2000; // 2 seconds
            bool canCastEleWeak = _eleWeakFirstCast || (CurrentTime - _lastEleWeakCastTime >= eleWeakCooldown);

            if (elementalWeakness != null && skillMonitor.CanUseSkill(elementalWeakness) && canCastEleWeak)
            {

                bool shouldCast = false;



                if (noWeakness)
                    shouldCast = true;


                if (shouldCast)
                {
                    _lastEleWeakCastTime = CurrentTime;    // start cooldown timer
                    _eleWeakFirstCast = false;
                    return elementalWeakness;
                }
            }




            // =========================================================================
            // 2) FROST BOMB
            // =========================================================================
            if (frostBomb != null && skillMonitor.CanUseSkill(frostBomb))
                return frostBomb;


            // =========================================================================
            // 3) FLASH GRENADE
            // =========================================================================
            if (flashGrenade != null && skillMonitor.CanUseSkill(flashGrenade))
                return flashGrenade;


            // =========================================================================
            // 4) ALTERNATE GRENADES (Explosive → Gas → Oil → repeat)
            // =========================================================================
            //var grenadeCandidates = new List<(ActiveSkill skill, int uses, int prio)>();

            //if (explosiveGrenade != null && skillMonitor.CanUseSkill(explosiveGrenade))
            //    grenadeCandidates.Add((explosiveGrenade, explosiveGrenade.Skill.RemainingUses, 1)); // lowest priority

            //if (gasGrenade != null && skillMonitor.CanUseSkill(gasGrenade))
            //    grenadeCandidates.Add((gasGrenade, gasGrenade.Skill.RemainingUses, 2)); // medium priority

            //if (oilGrenade != null && skillMonitor.CanUseSkill(oilGrenade))
            //    grenadeCandidates.Add((oilGrenade, oilGrenade.Skill.RemainingUses, 3)); // highest priority

            //// No grenades usable → continue to next logic
            //if (grenadeCandidates.Count == 0)
            //{
            //    // DO NOT return here — allow bowshots or other skills to be used
            //}
            //else
            //{
            //    // Pick grenade with highest remaining uses; ties resolved by priority
            //    var bestGrenade = grenadeCandidates
            //        .OrderByDescending(g => g.uses)   // highest remaining uses first
            //        .ThenByDescending(g => g.prio)    // tie: Oil > Gas > Explosive
            //        .First();

            //    return bestGrenade.skill;
            //}

            // ============================================================================
            // 4) PICK GRENADE WITH MOST USES (Oil > Gas > Explosive)
            // ============================================================================
            //ActiveSkill nextGrenade = null;

            //var grenadeCandidates = new List<(ActiveSkill skill, int uses, int prio)>();

            //void AddIfUsable(ActiveSkill s, int prio)
            //{
            //    if (s != null)
            //    {
            //        var uses = s.Skill.RemainingUses;
            //        // Only add if not on cooldown
            //        if (skillMonitor.CanUseSkill(s))
            //            grenadeCandidates.Add((s, uses, prio));
            //    }
            //}

            //AddIfUsable(explosiveGrenade, 1);
            //AddIfUsable(gasGrenade, 2);
            //AddIfUsable(oilGrenade, 3);

            //// Only return grenade if a grenade is ACTUALLY usable
            //if (grenadeCandidates.Count > 0)
            //{
            //    nextGrenade = grenadeCandidates
            //        .OrderByDescending(g => g.uses)
            //        .ThenByDescending(g => g.prio)
            //        .First().skill;

            //    return nextGrenade;
            //}

            // If we reach here, grenades exist but are on cooldown — DO NOT BLOCK ROTATION

            // ============================================================================
            // 4) SIMPLE GRENADE PRIORITY: Oil ≥ Gas ≥ Explosive
            // ============================================================================

            int oilUses = oilGrenade?.Skill.RemainingUses ?? 0;
            int gasUses = gasGrenade?.Skill.RemainingUses ?? 0;
            int explosiveUses = explosiveGrenade?.Skill.RemainingUses ?? 0;

            // --- Try Oil Grenade first (highest priority) ---
            if (oilGrenade != null &&
                skillMonitor.CanUseSkill(oilGrenade) &&
                oilUses >= gasUses &&
                oilUses >= explosiveUses)
            {
                return oilGrenade;
            }

            // --- Try Explosive next ---
            if (explosiveGrenade != null && explosiveUses >= gasUses &&
                skillMonitor.CanUseSkill(explosiveGrenade))
            {
                return explosiveGrenade;
            }


            // --- Try Gas Grenade last ---
            if (gasGrenade != null &&
                skillMonitor.CanUseSkill(gasGrenade))
            {
                return gasGrenade;
            }


            // No grenade usable → continue to bowshot or next logic

            // =========================================================================
            // 5) LIGHTNING ARROW
            // =========================================================================

            if (lightningArrow != null &&
            skillMonitor.CanUseSkill(lightningArrow) &&
            NearbyMonsterCount(target.Entity) > 1)
            {
                return lightningArrow;
            }


            // =========================================================================
            // 6) BOW SHOT
            // =========================================================================
            if (xbowShot != null && skillMonitor.CanUseSkill(xbowShot))
                return xbowShot;
            if (bowShot != null && skillMonitor.CanUseSkill(bowShot))
                return bowShot;

            return null;
        }

        private ActiveSkill DetermineNormalMonsterSkill(
    EntityInfo target,
    List<ActiveSkill> availableSkills,
    SkillMonitor skillMonitor)
        {
            var player = _gameController.Player;

            // Fetch all skills
            var lightningArrow = FindSkill(availableSkills, "LightningArrowPlayer");
            var contagion = FindSkill(availableSkills, "ContagionPlayer");
            var frostBomb = FindSkill(availableSkills, "FrostBombPlayer");
            var flashGrenade = FindSkill(availableSkills, "FlashGrenadePlayer");

            var explosiveGrenade = FindSkill(availableSkills, "ExplosiveGrenadePlayer");
            var gasGrenade = FindSkill(availableSkills, "ToxicGrenadePlayer");
            var oilGrenade = FindSkill(availableSkills, "OilGrenadePlayer");
            var bowShot = FindSkill(availableSkills, "MeleeBowPlayer");
            var xbowShot = FindSkill(availableSkills, "MeleeCrossbowPlayer");

            // =========================================================================
            // 1) CONTAGION
            // =========================================================================
            if (contagion != null &&
                skillMonitor.CanUseSkill(contagion) &&
                !AnyNearbyMonsterHasContagion(target.Entity) &&
                NearbyMonsterCount(target.Entity) > 1)
            {
                return contagion;
            }




            // =========================================================================
            // 2) FLASH GRENADE
            // =========================================================================
            if (flashGrenade != null && skillMonitor.CanUseSkill(flashGrenade))
                return flashGrenade;

            //// =========================================================================
            //// 3) FROST BOMB
            //// =========================================================================
            //if (frostBomb != null && skillMonitor.CanUseSkill(frostBomb))
            //    return frostBomb;

            // =========================================================================
            // 4) ALTERNATE GRENADES (Explosive → Gas → Oil → repeat)
            // =========================================================================
            //var grenadeCandidates = new List<(ActiveSkill skill, int uses, int prio)>();

            //if (explosiveGrenade != null && skillMonitor.CanUseSkill(explosiveGrenade))
            //    grenadeCandidates.Add((explosiveGrenade, explosiveGrenade.Skill.RemainingUses, 1)); // lowest priority

            //if (gasGrenade != null && skillMonitor.CanUseSkill(gasGrenade))
            //    grenadeCandidates.Add((gasGrenade, gasGrenade.Skill.RemainingUses, 2)); // medium priority

            //if (oilGrenade != null && skillMonitor.CanUseSkill(oilGrenade))
            //    grenadeCandidates.Add((oilGrenade, oilGrenade.Skill.RemainingUses, 3)); // highest priority

            //// No grenades usable → continue to next logic
            //if (grenadeCandidates.Count == 0)
            //{
            //    // DO NOT return here — allow bowshots or other skills to be used
            //}
            //else
            //{
            //    // Pick grenade with highest remaining uses; ties resolved by priority
            //    var bestGrenade = grenadeCandidates
            //        .OrderByDescending(g => g.uses)   // highest remaining uses first
            //        .ThenByDescending(g => g.prio)    // tie: Oil > Gas > Explosive
            //        .First();

            //    return bestGrenade.skill;
            //}

            // ============================================================================
            // 4) PICK GRENADE WITH MOST USES (Oil > Gas > Explosive)
            // ============================================================================
            //ActiveSkill nextGrenade = null;

            //var grenadeCandidates = new List<(ActiveSkill skill, int uses, int prio)>();

            //void AddIfUsable(ActiveSkill s, int prio)
            //{
            //    if (s != null)
            //    {
            //        var uses = s.Skill.RemainingUses;
            //        // Only add if not on cooldown
            //        if (skillMonitor.CanUseSkill(s))
            //            grenadeCandidates.Add((s, uses, prio));
            //    }
            //}

            //AddIfUsable(explosiveGrenade, 1);
            //AddIfUsable(gasGrenade, 2);
            //AddIfUsable(oilGrenade, 3);

            //// Only return grenade if a grenade is ACTUALLY usable
            //if (grenadeCandidates.Count > 0)
            //{
            //    nextGrenade = grenadeCandidates
            //        .OrderByDescending(g => g.uses)
            //        .ThenByDescending(g => g.prio)
            //        .First().skill;

            //    return nextGrenade;
            //}

            // If we reach here, grenades exist but are on cooldown — DO NOT BLOCK ROTATION
            // ============================================================================
            // 4) SIMPLE GRENADE PRIORITY: Oil ≥ Gas ≥ Explosive
            // ============================================================================

            int oilUses = oilGrenade?.Skill.RemainingUses ?? 0;
            int gasUses = gasGrenade?.Skill.RemainingUses ?? 0;
            int explosiveUses = explosiveGrenade?.Skill.RemainingUses ?? 0;

            // --- Try Oil Grenade first (highest priority) ---
            if (oilGrenade != null &&
                skillMonitor.CanUseSkill(oilGrenade) &&
                oilUses >= gasUses &&
                oilUses >= explosiveUses)
            {
                return oilGrenade;
            }

            // --- Try Explosive next ---
            if (explosiveGrenade != null && explosiveUses  >= gasUses &&
                skillMonitor.CanUseSkill(explosiveGrenade))
            {
                return explosiveGrenade;
            }


            // --- Try Gas Grenade last ---
            if (gasGrenade != null &&
                skillMonitor.CanUseSkill(gasGrenade) )
            {
                return gasGrenade;
            }


            // No grenade usable → continue to bowshot or next logic


            // =========================================================================
            // 5) LIGHTNING ARROW
            // =========================================================================

            if (lightningArrow != null &&
            skillMonitor.CanUseSkill(lightningArrow) &&
            NearbyMonsterCount(target.Entity) > 1)
            {
                return lightningArrow;
            }


            // =========================================================================
            // 6) BOW SHOT
            // =========================================================================
            if (xbowShot != null && skillMonitor.CanUseSkill(xbowShot))
                return xbowShot;
            if (bowShot != null && skillMonitor.CanUseSkill(bowShot))
                return bowShot;

            return null;
        }

        private ActiveSkill FindSkill(List<ActiveSkill> skills, string skillName)
        {
            return skills.FirstOrDefault(x => x.Name == skillName);
        }

        private int GetNearbyOrbOfStormsCount(ActiveSkill orb)
        {
            int stage = orb?.Skill.SkillUseStage ?? 0;

            return stage - 2;

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
                    x.Type == EntityType.Monster &&
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
    }
}