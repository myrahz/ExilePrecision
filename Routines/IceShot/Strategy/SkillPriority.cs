using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ExilePrecision.Core.Combat.Skills;
using ExilePrecision.Features.Targeting.EntityInformation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExilePrecision.Routines.IceShot.Strategy
{
    public class SkillPriority
    {
        private readonly GameController _gameController;
        private readonly HashSet<string> _trackedSkills = new()
        {
            "IceTippedArrowsPlayer",
            "TornadoShotPlayer",
            "ThunderstormPlayer",
            "FreezingMarkPlayer",
            "IceShotPlayer",
            "MeleeBowPlayer"
        };

        private long _lastStormCastTime = 0;
        private const float NEARBY_MONSTER_RADIUS = 20.0f;
        private long CurrentTime => Environment.TickCount64;


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

            


            const int thunderstormCooldown = 2000; // 2 seconds
            bool canCastStorm =  (CurrentTime - _lastStormCastTime >= thunderstormCooldown);

            if (canCastStorm)
            {
                var thunderstorm = FindSkill(availableSkills, "ThunderstormPlayer");
                if (thunderstorm != null && skillMonitor.CanUseSkill(thunderstorm))
                {
                    _lastStormCastTime = CurrentTime;
                    return thunderstorm;
                }
                    
            }

            if (!HasFreezingMark(target.Entity))
            {
                var freezingMark = FindSkill(availableSkills, "FreezingMarkPlayer");
                if (freezingMark != null && skillMonitor.CanUseSkill(freezingMark))
                    return freezingMark;
            }

            if (!HasNearbyTornado(target.Entity))
            {
                var tornadoShot = FindSkill(availableSkills, "TornadoShotPlayer");
                if (tornadoShot != null && skillMonitor.CanUseSkill(tornadoShot))
                    return tornadoShot;
            }



            var iceShot = FindSkill(availableSkills, "IceShotPlayer");
            if (iceShot != null && skillMonitor.CanUseSkill(iceShot))
                return iceShot;


            var BowShot = FindSkill(availableSkills, "MeleeBowPlayer");
            if (BowShot != null && skillMonitor.CanUseSkill(BowShot))
                return BowShot;


            return null;
        }

        private ActiveSkill DetermineNormalMonsterSkill(
            EntityInfo target,
            List<ActiveSkill> availableSkills,
            SkillMonitor skillMonitor)
        {





            var iceShot = FindSkill(availableSkills, "IceShotPlayer");
            if (iceShot != null && skillMonitor.CanUseSkill(iceShot))
                return iceShot;


            var BowShot = FindSkill(availableSkills, "MeleeBowPlayer");
            if (BowShot != null && skillMonitor.CanUseSkill(BowShot))
                return BowShot;



            return null;
        }

        private ActiveSkill FindSkill(List<ActiveSkill> skills, string skillName)
        {
            return skills.FirstOrDefault(x => x.Name == skillName);
        }

        private bool HasFreezingMark(Entity target)
        {
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs))
                    return false;

                return buffs.BuffsList?.Any(buff => buff.Name == "freezing_mark") ?? false;
            }
            catch (Exception)
            {
                return false;
            }
        }
        private bool HasNearbyTornado(Entity target)
        {
            try
            {
                return _gameController.Entities
                    .Where(x => x?.Path?.Contains("Metadata/MiscellaneousObjects/TornadoShotTornado") ?? false)
                       .Any(x => x.Distance(target) <= NEARBY_MONSTER_RADIUS);
            }
            catch (Exception)
            {
                return false;
            }
        }




    }
}