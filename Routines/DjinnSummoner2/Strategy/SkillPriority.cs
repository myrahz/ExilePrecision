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

namespace ExilePrecision.Routines.DjinnSummoner2.Strategy
{
    public class SkillPriority
    {
        // Non-elite Kelari alternation: 0=Brutality, 1=Deception
        private int _djinnSkillRotation = 0;

        // Elite rotation steps:
        //   0 = Thunderstorm
        //   1 = Brutality #1
        //   2 = Brutality #2
        //   3 = Deception #1
        //   4 = Deception #2
        //   5 = Deception #3  (then wraps back to 0)
        private int _eliteRotationStep = 0;
        private long _lastThunderstormCastTime = 0;
        private bool _thunderstormFirstCast = true;
        private const int THUNDERSTORM_COOLDOWN = 2000; // 2 seconds


        // CONFIGS
        private int orbLimit = 2;
        private const float NEARBY_MONSTER_RADIUS = 30.0f;

        private long CurrentTime => Environment.TickCount64;
        private readonly GameController _gameController;

        private readonly HashSet<string> _trackedSkills = new()
        {
            // --- Kelari (mid/high level) ---
            "CommandSandDjinnKnifeThrowPlayer",        // Kelari's Brutality
            "CommandSandDjinnExplosiveTeleportPlayer", // Kelari's Deception

            // --- Elite-only skills ---
            "PouncePlayer",       // Pounce (applies Mark for Death)
            "ThunderstormPlayer", // Thunderstorm

            // --- Mid-level fallback (Frost Bomb / OoS / Fusillade tier) ---
            "FrostBombPlayer",
            "OrbOfStormsPlayer",
            "EmberFusilladePlayer",

            // --- Lower-level fallback (Essence Drain / Chaos Bolt tier) ---
            "EssenceDrainPlayer",
            "WeaponGrantedChaosboltPlayer",

            // --- Lowest-level fallback ---
            "SparkPlayer",
            "FireboltPlayer",
            "LightningBoltPlayer",
            "FreezingShardsPlayer",
            "ContagionPlayer",
        };

        public SkillPriority(GameController gameController)
        {
            _gameController = gameController;
        }

        // ============================================================
        // ENTRY POINT
        // ============================================================
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

        // ============================================================
        // ELITE ROTATION
        // ============================================================
        private ActiveSkill DetermineEliteMonsterSkill(
     EntityInfo target,
     List<ActiveSkill> availableSkills,
     SkillMonitor skillMonitor)
        {
            var kelarisBrutality = FindSkill(availableSkills, "CommandSandDjinnKnifeThrowPlayer");
            var kelarisDeception = FindSkill(availableSkills, "CommandSandDjinnExplosiveTeleportPlayer");

            bool hasKelariSkills = kelarisBrutality != null || kelarisDeception != null;

            if (hasKelariSkills)
            {
                var pounce = FindSkill(availableSkills, "PouncePlayer");
                var thunderstorm = FindSkill(availableSkills, "ThunderstormPlayer");

                // --------------------------------------------------------
                // 1) POUNCE – if target is missing Mark for Death
                // --------------------------------------------------------
                if (pounce != null && skillMonitor.CanUseSkill(pounce) && !HasMarkForDeath(target.Entity))
                    return pounce;

                // --------------------------------------------------------
                // 2) THUNDERSTORM – if target doesn't have "wet" debuff
                //    and at least 2 seconds since last cast
                // --------------------------------------------------------
                bool canCastThunderstorm = _thunderstormFirstCast ||
                    (CurrentTime - _lastThunderstormCastTime >= THUNDERSTORM_COOLDOWN);

                if (thunderstorm != null && skillMonitor.CanUseSkill(thunderstorm)
                    && !HasWetDebuff(target.Entity) && canCastThunderstorm)
                {
                    _lastThunderstormCastTime = CurrentTime;
                    _thunderstormFirstCast = false;
                    return thunderstorm;
                }
                bool canUseBrutality = kelarisBrutality != null && skillMonitor.CanUseSkill(kelarisBrutality);
                bool canUseDeception = kelarisDeception != null && skillMonitor.CanUseSkill(kelarisDeception);
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

                if (canUseBrutality) return kelarisBrutality;
                if (canUseDeception) return kelarisDeception;

                return null; // everything on cooldown
            }

            // No Kelari skills → lower-level fallback
            return DetermineLowLevelSkill(target, availableSkills, skillMonitor);
        }


        // ============================================================
        // NORMAL MONSTER ROTATION
        // ============================================================
        private ActiveSkill DetermineNormalMonsterSkill(
            EntityInfo target,
            List<ActiveSkill> availableSkills,
            SkillMonitor skillMonitor)
        {
            var kelarisBrutality = FindSkill(availableSkills, "CommandSandDjinnKnifeThrowPlayer");
            var kelarisDeception = FindSkill(availableSkills, "CommandSandDjinnExplosiveTeleportPlayer");

            bool hasKelariSkills = kelarisBrutality != null || kelarisDeception != null;

            if (hasKelariSkills)
            {
                bool canUseBrutality = kelarisBrutality != null && skillMonitor.CanUseSkill(kelarisBrutality);
                bool canUseDeception = kelarisDeception != null && skillMonitor.CanUseSkill(kelarisDeception);

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

                if (canUseBrutality) return kelarisBrutality;
                if (canUseDeception) return kelarisDeception;

                return null; // both on cooldown
            }

            // No Kelari skills → lower-level fallback
            return DetermineLowLevelSkill(target, availableSkills, skillMonitor);
        }

        // ============================================================
        // SHARED LOW-LEVEL FALLBACK
        //   Pack (>4 nearby)  → always ED/Contagion rotation
        //   Rare/Unique only  → Fusillade/OoS rotation
        //   Tier 2-ED         → Contagion → Essence Drain → Chaos Bolt filler
        //   Tier 3            → Frost Bomb, Contagion (dying), Spark, Firebolt, etc.
        // ============================================================
        private ActiveSkill DetermineLowLevelSkill(
            EntityInfo target,
            List<ActiveSkill> availableSkills,
            SkillMonitor skillMonitor)
        {
            var frostBomb = FindSkill(availableSkills, "FrostBombPlayer");
            var orbOfStorms = FindSkill(availableSkills, "OrbOfStormsPlayer");
            var fusillade = FindSkill(availableSkills, "EmberFusilladePlayer");
            var essenceDrain = FindSkill(availableSkills, "EssenceDrainPlayer");
            var chaosBolt = FindSkill(availableSkills, "WeaponGrantedChaosboltPlayer");
            var contagion = FindSkill(availableSkills, "ContagionPlayer");
            var spark = FindSkill(availableSkills, "SparkPlayer");
            var firebolt = FindSkill(availableSkills, "FireboltPlayer");
            var lightningBolt = FindSkill(availableSkills, "LightningBoltPlayer");
            var freezeShards = FindSkill(availableSkills, "FreezingShardsPlayer");

            bool isEliteTarget = target.Rarity is MonsterRarity.Rare or MonsterRarity.Unique;
            int nearbyCount = NearbyMonsterCount(target.Entity, 40);
            bool isPack = nearbyCount > 4;
            // If the pack already has at least one monster with both ED+Contagion,
            // the spread is handled — switch to Fusillade/OoS to exploit it.
            bool packAlreadyCovered = isPack && PackHasActiveEDContagionCombo(target.Entity);

            // ----------------------------------------------------------
            // TIER 1: Fusillade / Orb of Storms
            //   Enter when: target is Rare/Unique, OR pack is already covered
            //   Skip when: large pack with no combo running yet (go apply debuffs first)
            // ----------------------------------------------------------
            if ((fusillade != null || orbOfStorms != null) && (isEliteTarget || packAlreadyCovered))
            {
                int fusilladeStacks = GetPlayerBuffCharges("ember_fusilade_projectile_count");

                // Already building stacks — finish reaching 7 before anything else
                if (fusilladeStacks > 0 && fusilladeStacks < 7
                    && fusillade != null && skillMonitor.CanUseSkill(fusillade))
                    return fusillade;

                // No stacks or at 7 — do maintenance first
                if (frostBomb != null && skillMonitor.CanUseSkill(frostBomb))
                    return frostBomb;

                if (orbOfStorms != null && skillMonitor.CanUseSkill(orbOfStorms)
                    && CountOrbsOfStorms(orbOfStorms) < orbLimit)
                    return orbOfStorms;

                // Start (or restart) building Fusillade stacks
                if (fusillade != null && skillMonitor.CanUseSkill(fusillade) && fusilladeStacks < 7)
                    return fusillade;
            }

            // ----------------------------------------------------------
            // TIER 2-ED: Essence Drain rotation
            //   Enter when: ED is available, OR it's a large pack (>4 nearby)
            //   In a pack we always prefer Contagion → ED spread over fusillade
            // ----------------------------------------------------------
            if (essenceDrain != null || isPack)
            {
                if (essenceDrain != null)
                {
                    // Contagion first — ED will do the killing, no dying condition needed
                    if (contagion != null && skillMonitor.CanUseSkill(contagion)
                        && ShouldCastContagionForEssenceDrain(target.Entity))
                        return contagion;

                    if (skillMonitor.CanUseSkill(essenceDrain) && !HasEssenceDrain(target.Entity))
                        return essenceDrain;

                    if (chaosBolt != null && skillMonitor.CanUseSkill(chaosBolt))
                        return chaosBolt;

                    // fall through to Tier 3
                }
            }

            // ----------------------------------------------------------
            // TIER 3: Absolute lowest level – individual spell priority
            // ----------------------------------------------------------

            // 1. Frost Bomb
            if (frostBomb != null && skillMonitor.CanUseSkill(frostBomb))
                return frostBomb;

            // 2. Contagion – dying/weakest target in a pack (no ED, natural death needed)
            if (contagion != null && skillMonitor.CanUseSkill(contagion)
                && ShouldCastContagionOnTarget(target.Entity))
                return contagion;

            // 3. Spark when cold infused
            if (spark != null && skillMonitor.CanUseSkill(spark) && ColdInfusions() > 0)
                return spark;

            // 4. Spark when multiple monsters nearby
            if (spark != null && skillMonitor.CanUseSkill(spark) && nearbyCount > 2)
                return spark;

            // 5. Firebolt
            if (firebolt != null && skillMonitor.CanUseSkill(firebolt))
                return firebolt;

            // 6. Lightning Bolt
            if (lightningBolt != null && skillMonitor.CanUseSkill(lightningBolt))
                return lightningBolt;

            // 7. Freezing Shards
            if (freezeShards != null && skillMonitor.CanUseSkill(freezeShards))
                return freezeShards;

            // 8. Spark unconditional fallback
            if (spark != null && skillMonitor.CanUseSkill(spark))
                return spark;

            return null;
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================

        private ActiveSkill FindSkill(List<ActiveSkill> skills, string skillName)
            => skills.FirstOrDefault(x => x.Name == skillName);

        private bool HasMarkForDeath(Entity target)
        {
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                    return false;
                return buffs.BuffsList.Any(b =>
                    b.Name == "mark_for_death" || b.Name == "marked_for_death");
            }
            catch { return false; }
        }

        private bool HasEssenceDrain(Entity target)
        {
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                    return false;
                return buffs.BuffsList.Any(b => b.Name == "siphon_damage");
            }
            catch { return false; }
        }
        private bool HasWetDebuff(Entity target)
        {
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                    return false;
                return buffs.BuffsList.Any(b => b.Name == "wet");
            }
            catch { return false; }
        }

        private int ColdInfusions()
        {
            try
            {
                var player = _gameController.Player;
                if (!player.TryGetComponent<Buffs>(out var buffs))
                    return 0;
                var charge = buffs.BuffsList?.FirstOrDefault(b => b.Name == "cold_infusion");
                return charge?.BuffCharges ?? 0;
            }
            catch { return 0; }
        }

        private int GetPlayerBuffCharges(string buffName)
        {
            try
            {
                var player = _gameController.Player;
                if (!player.TryGetComponent<Buffs>(out var buffs))
                    return 0;
                var buff = buffs.BuffsList?.FirstOrDefault(b => b.Name == buffName);
                return buff?.BuffCharges ?? 0;
            }
            catch { return 0; }
        }

        private int CountOrbsOfStorms(ActiveSkill orb)
        {
            try
            {
                int stage = orb?.Skill.SkillUseStage ?? 0;

                return stage - 2;
            }
            catch { return 0; }
        }
        private bool HasOOSAnimation(Entity entity)
        {
            try
            {
                if (!entity.TryGetComponent<Animated>(out var animated))
                    return false;
                return animated?.MiscAnimated?.AOFile ==
                    "Metadata/Effects/Spells/lightning_orb_of_storms/orb_beam.ao";
            }
            catch { return false; }
        }

        private (bool release, int stage) GetFusilladeReleaseState(ActiveSkill fusillade)
        {
            int stage = fusillade?.Skill.SkillUseStage ?? 0;
            bool release = stage >= 11;
            return (release, stage);
        }

        private int NearbyMonsterCount(Entity target, float radius = 40f)
        {
            return _gameController.Entities
                .Count(x =>
                    x.Type == EntityType.Monster && x.IsHostile &&
                    x.IsAlive &&
                    x.Distance(target) <= radius);
        }

        private bool HasElementalWeakness(Entity target)
        {
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs))
                    return false;
                return buffs.BuffsList?.Any(b => b.Name == "curse_elemental_weakness") ?? false;
            }
            catch { return false; }
        }

        private int GetCritWeaknessStacks(Entity target)
        {
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                    return 0;
                var buff = buffs.BuffsList.FirstOrDefault(b => b.Name == "critical_weakness");
                return buff?.BuffCharges ?? 0;
            }
            catch { return 0; }
        }

        private bool HasNearbyChilledGround(Entity target)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Path?.Contains(
                        "Metadata/Effects/Spells/ground_effects/VisibleServerGroundEffect") ?? false)
                    .Where(HasChilledGroundAnimation)
                    .Any(x => x.Distance(target) <= 30);
            }
            catch { return false; }
        }

        private bool HasChilledGroundAnimation(Entity entity)
        {
            try
            {
                if (!entity.TryGetComponent<Animated>(out var animated))
                    return false;
                return animated?.BaseAnimatedObjectEntity?.Path ==
                    "Metadata/Effects/Spells/grd_Zones/grd_Chilled01.ao";
            }
            catch { return false; }
        }

        private bool HasLivingBomb(Entity target)
        {
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                    return false;
                return buffs.BuffsList.Any(b => b.Name == "living_bomb_count");
            }
            catch { return false; }
        }

        public bool IsFlameWallCloseToTarget(Entity target, float closeRadius)
        {
            Vector2 targetPos = target.GridPos;
            return _gameController.Entities
                .Where(x => x?.Path?.Contains("Metadata/Monsters/Anomalies/Firewall") ?? false)
                .Any(fw => Vector2.Distance(fw.GridPos, targetPos) <= closeRadius);
        }

        // True if any monster near the target already carries both ED and Contagion.
        // When this is the case the spread is already running and we can switch
        // to Fusillade/OoS to deal damage while the debuffs do their work.
        private bool PackHasActiveEDContagionCombo(Entity target)
        {
            try
            {
                return _gameController.Entities
                    .Any(x =>
                        x != target &&
                        (x?.Type == EntityType.Monster) &&
                        (x?.IsAlive ?? false) &&
                        x.Distance(target) <= NEARBY_MONSTER_RADIUS &&
                        x.TryGetComponent<Buffs>(out var buffs) &&
                        buffs.BuffsList != null &&
                        buffs.BuffsList.Any(b => b.Name == "siphon_damage") &&
                        buffs.BuffsList.Any(b => b.Name == "contagion"));
            }
            catch { return false; }
        }

        // ED mode: cast Contagion regardless of target HP — ED kills fast anyway.
        // Conditions: target has no contagion yet, at least one neighbour nearby,
        // and nothing nearby is already spreading.
        private bool ShouldCastContagionForEssenceDrain(Entity target)
        {
            try
            {
                // Skip if target already has contagion
                if (target.TryGetComponent<Buffs>(out var targetBuffs) && targetBuffs.BuffsList != null)
                    if (targetBuffs.BuffsList.Any(b => b.Name == "contagion"))
                        return false;

                var nearby = _gameController.Entities
                    .Where(x =>
                        x != target &&
                        (x?.Type == EntityType.Monster) &&
                        (x?.IsAlive ?? false) &&
                        (x?.IsTargetable ?? false) &&
                        x.Distance(target) <= NEARBY_MONSTER_RADIUS)
                    .ToList();

                // Isolated target — contagion won't chain to anyone
                if (!nearby.Any())
                    return false;

                // Skip if something nearby already carries contagion
                bool alreadySpreading = nearby.Any(m =>
                {
                    if (!m.TryGetComponent<Buffs>(out var b) || b.BuffsList == null) return false;
                    return b.BuffsList.Any(buff => buff.Name == "contagion");
                });

                return !alreadySpreading;
            }
            catch { return false; }
        }

        // Returns true when it makes sense to cast Contagion on this target (no ED):
        //   - At least one neighbour within NEARBY_MONSTER_RADIUS
        //   - No nearby monster already has Contagion active
        //   - Target is about to die: Normal always, Magic <40%, Rare <20%, Unique never
        private bool ShouldCastContagionOnTarget(Entity target)
        {
            try
            {
                var nearby = _gameController.Entities
                    .Where(x =>
                        x != target &&
                        (x?.Type == EntityType.Monster) &&
                        (x?.IsAlive ?? false) &&
                        (x?.IsTargetable ?? false) &&
                        x.Distance(target) <= NEARBY_MONSTER_RADIUS)
                    .ToList();

                if (!nearby.Any())
                    return false;

                bool alreadySpreading = nearby.Any(m =>
                {
                    if (!m.TryGetComponent<Buffs>(out var b) || b.BuffsList == null) return false;
                    return b.BuffsList.Any(buff => buff.Name == "contagion");
                });

                if (alreadySpreading)
                    return false;

                // Also check the target itself
                if (target.TryGetComponent<Buffs>(out var targetBuffs) && targetBuffs.BuffsList != null)
                    if (targetBuffs.BuffsList.Any(b => b.Name == "contagion"))
                        return false;

                var rarity = target.GetComponent<ObjectMagicProperties>()?.Rarity ?? MonsterRarity.White;
                var life = target.GetComponent<Life>();
                float hp = life?.HPPercentage ?? 1f;

                return rarity switch
                {
                    MonsterRarity.White => true,
                    MonsterRarity.Magic => hp <= 0.40f,
                    MonsterRarity.Rare => hp <= 0.20f,
                    _ => false,
                };
            }
            catch { return false; }
        }

        private int CountNearbyMonsters(Entity target)
        {
            try
            {
                return _gameController.Entities
                    .Where(x =>
                        (x?.Type == EntityType.Monster) &&
                        (x?.IsAlive ?? false) &&
                        (x?.IsTargetable ?? false))
                    .Count(x => x.Distance(target) <= NEARBY_MONSTER_RADIUS) - 1;
            }
            catch { return 0; }
        }
    }
}