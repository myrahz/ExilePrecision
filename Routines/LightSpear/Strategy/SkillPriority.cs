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

namespace ExilePrecision.Routines.LightSpear.Strategy
{
    public class SkillPriority
    {
        private readonly GameController _gameController;
        private readonly HashSet<string> _trackedSkills = new()
        {
            "LightningSpearPlayer",
            "StormLancePlayer",
            "SnipersMarkPlayer"

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

            var barrage = FindSkill(availableSkills, "BarragePlayer");
            if (barrage != null && skillMonitor.CanUseSkill(barrage) )
            //if (barrage != null && skillMonitor.CanUseSkill(barrage) && barrage.Skill.SkillUseStage != 3 )
                    return barrage;

            if (!HasSnipersMark(target.Entity))
            {
                var snipers = FindSkill(availableSkills, "SnipersMarkPlayer");
                if (snipers != null && skillMonitor.CanUseSkill(snipers))
                    return snipers;
            }

            if (VoltaicCharges() >= 30)
            {
                var lightspear = FindSkill(availableSkills, "LightningSpearPlayer");
                if (lightspear != null && skillMonitor.CanUseSkill(lightspear))
                    return lightspear;
            }
            else if (player.GetComponent<Actor>().Animation != AnimationE.FixedRunLayerBase)
            {
                var stormlance = FindSkill(availableSkills, "StormLancePlayer");
                if (stormlance != null && skillMonitor.CanUseSkill(stormlance))
                    return stormlance;
            }

         
            return null;
        
    }

        private ActiveSkill DetermineNormalMonsterSkill(
            EntityInfo target,
            List<ActiveSkill> availableSkills,
            SkillMonitor skillMonitor)
        {
            var player = _gameController.Player;
            Vector2 interpolatedPosition = Vector2.Lerp(player.GridPos, target.GridPos, 0.5f);
            if (!HasSnipersMark(target.Entity) && FrenzyCharges() == 0)
            {
                var snipers = FindSkill(availableSkills, "SnipersMarkPlayer");
                if (snipers != null && skillMonitor.CanUseSkill(snipers))
                    return snipers;
            }

            if (VoltaicCharges() >= 12)
            {
                var barrage = FindSkill(availableSkills, "BarragePlayer");
                //if (barrage != null && skillMonitor.CanUseSkill(barrage) && barrage.Skill.SkillUseStage != 3)
                if (barrage != null && skillMonitor.CanUseSkill(barrage) )
                    return barrage;

                var lightspear = FindSkill(availableSkills, "LightningSpearPlayer");
                if (lightspear != null && skillMonitor.CanUseSkill(lightspear))
                    return lightspear;
            }
            else if (player.GetComponent<Actor>().Animation != AnimationE.FixedRunLayerBase)
            {
                var stormlance = FindSkill(availableSkills, "StormLancePlayer");
                if (stormlance != null && skillMonitor.CanUseSkill(stormlance))
                    return stormlance;
            }


            return null;
        }

        private ActiveSkill FindSkill(List<ActiveSkill> skills, string skillName)
        {
            return skills.FirstOrDefault(x => x.Name == skillName);
        }

        private bool HasSnipersMark(Entity target)
        {
            
            try
            {
                if (!target.TryGetComponent<Buffs>(out var buffs))
                    return false;

                return buffs.BuffsList?.Any(buff => buff.Name == "snipers_mark") ?? false;
            }
            catch (Exception)
            {
                return false;
            }
        }        
        
        private int VoltaicCharges()
        {

            // support_static_charge
            var player = _gameController.Player;
            try
            {
                if (!player.TryGetComponent<Buffs>(out var buffs))
                    return 0;
                var VoltaicBuff = buffs.BuffsList?.FirstOrDefault(buff => buff.Name == "support_static_charge");

                return VoltaicBuff?.BuffCharges ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private int FrenzyCharges()
        {

            // support_static_charge
            var player = _gameController.Player;
            try
            {
                if (!player.TryGetComponent<Buffs>(out var buffs))
                    return 0;
                var frenzybuff = buffs.BuffsList?.FirstOrDefault(buff => buff.Name == "frenzy_charge");

                return frenzybuff?.BuffCharges ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        //private bool HasNearbyStormCloud(Entity target)
        //{
        //    try
        //    {
        //        return _gameController.Entities
        //            .Where(x => x?.Path?.Contains("Metadata/Effects/SleepableEffect") ?? false)
        //            .Where(HasStormCloudAnimation)
        //            .Any(x => x.Distance(target) <= STORM_CLOUD_RADIUS);
        //    }
        //    catch (Exception)
        //    {
        //        return false;
        //    }
        //}





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