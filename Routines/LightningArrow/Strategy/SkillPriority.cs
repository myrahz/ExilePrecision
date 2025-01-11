using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ExilePrecision.Core.Combat.Skills;
using ExilePrecision.Features.Targeting.EntityInformation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExilePrecision.Routines.LightningArrow.Strategy
{
    public class SkillPriority
    {
        private readonly GameController _gameController;
        private readonly HashSet<string> _trackedSkills = new()
        {
            "LightningArrowPlayer",
            "LightningRodPlayer",
            "OrbOfStormsPlayer",
            "VoltaicMarkPlayer"
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
            if (!HasVoltaicMark(target.Entity))
            {
                var voltaic = FindSkill(availableSkills, "VoltaicMarkPlayer");
                if (voltaic != null && skillMonitor.CanUseSkill(voltaic))
                    return voltaic;
            }

            if (target.Rarity == MonsterRarity.Unique)
            {
                if (HasNearbyStormCloud(target.Entity))
                {
                    var lightningRod = FindSkill(availableSkills, "LightningRodPlayer");
                    if (lightningRod != null && skillMonitor.CanUseSkill(lightningRod))
                        return lightningRod;
                }

                if (target.Distance <= EFFECTIVE_ORB_RANGE)
                {
                    var orbOfStorms = FindSkill(availableSkills, "OrbOfStormsPlayer");
                    if (orbOfStorms != null && skillMonitor.CanUseSkill(orbOfStorms))
                        return orbOfStorms;
                }
            }
            else
            {
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

            return null;
        }

        private ActiveSkill DetermineNormalMonsterSkill(
            EntityInfo target,
            List<ActiveSkill> availableSkills,
            SkillMonitor skillMonitor)
        {
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

        private bool ShouldUseLightningRod(Entity target)
        {
            try
            {
                if (target?.Rarity is MonsterRarity.White or MonsterRarity.Magic)
                    return false;

                return !HasNearbyLightningRod(target);
            }
            catch (Exception)
            {
                return false;
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
    }
}