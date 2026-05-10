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

namespace ExilePrecision.Routines.InfusedSorc.Strategy
{
    public class SkillPriority
    {
        private long _lastArcCastTime = 0;
        private long _lastEleWeakCastTime = 0;
        private bool _arcFirstCast = true;
        private bool _eleWeakFirstCast = true;
        private bool _useFirestormNext = true;
        private long _lastStormCastTime = 0;

        // CONFIGS HERE
        private bool castFusiladeOnNormalMonsters = false;
        private int orbLimit = 5;
        private int ORB_OF_STORM_RADIUS = 30;
        private int MAX_DISTANCE_FOR_NON_ARC = 70;

        private bool SPELLSLINGER_REQUIRES_LIGHTNING_INFUSION = true;
        private int MIN_ENERGY_FOR_SPELLSLINGER = 23; // arc is 1.1 sec so 110 / 500 = 0.22 so 23 will make it cast asap 
        private int MIN_ENERGY_FOR_ELE_INVOCATION = 13; // LIVING BOMB IS  0.6 sec so 110 / 500 = 0.12 so 13 will make it cast asap

        private long CurrentTime => Environment.TickCount64;
        private readonly GameController _gameController;
        private readonly HashSet<string> _trackedSkills = new()
        {
            "ArcPlayer",
            "OrbOfStormsPlayer",
            "FrostBombPlayer",
            "ElementalWeaknessPlayer",
            "EmberFusilladePlayer",
            "FirestormPlayer",
            "FlameWallPlayer",
            "ThunderstormPlayer",
            "MetaSpellslingerPlayer",
            "MetaElementalInvocationPlayer"



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


            var arc = FindSkill(availableSkills, "ArcPlayer");
            var orb = FindSkill(availableSkills, "OrbOfStormsPlayer");
            var eleWeak = FindSkill(availableSkills, "ElementalWeaknessPlayer");
            var fusillade = FindSkill(availableSkills, "EmberFusilladePlayer");
            var frostbomb = FindSkill(availableSkills, "FrostBombPlayer");
            var firestorm = FindSkill(availableSkills, "FirestormPlayer");            
            var thunderstorm = FindSkill(availableSkills, "ThunderstormPlayer");
            var metaSpellslingerPlayer = FindSkill(availableSkills, "MetaSpellslingerPlayer");
            var (release, stage) = GetFusilladeReleaseState(fusillade);
            var metaElementalInvocationPlayer = FindSkill(availableSkills, "MetaElementalInvocationPlayer");


            //if (metaElementalInvocationPlayer != null && skillMonitor.CanUseSkill(metaElementalInvocationPlayer) && ElementalInvocationCharges() > 0)
            if (metaElementalInvocationPlayer != null && skillMonitor.CanUseSkill(metaElementalInvocationPlayer) && ElementalInvocationPercent() > MIN_ENERGY_FOR_ELE_INVOCATION && FireInfusions() < 3)
                return metaElementalInvocationPlayer;


            //if (metaSpellslingerPlayer != null && skillMonitor.CanUseSkill(metaSpellslingerPlayer) && SpellSlingerCharges() > 0)
            if (metaSpellslingerPlayer != null && skillMonitor.CanUseSkill(metaSpellslingerPlayer)
                && (SPELLSLINGER_REQUIRES_LIGHTNING_INFUSION || LightningInfusions() > 0) && SpellSlingerBuffPercent() > MIN_ENERGY_FOR_SPELLSLINGER)
                return metaSpellslingerPlayer;









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

            // LOADED WITH INFUSIONS, UNLEASH FIRESTORM TO PROC FIRESTORM
            if (thunderstorm != null && skillMonitor.CanUseSkill(thunderstorm) && (LightningInfusions() + FireInfusions() + ColdInfusions()) > 2)
            {
                return thunderstorm;
            }

            // ============================================================
            // 1) CAST FUSILADE IF PREVIOUSLY CHARGIN
            // ============================================================

            if (!release && fusillade != null && skillMonitor.CanUseSkill(fusillade) && stage > 2)
                return fusillade;


            // ============================================================
            // 1) ORB OF STORMS
            // ============================================================
            int orbCount = GetNearbyOrbOfStormsCount(orb);
            //int orbCount = GetNearbyOrbOfStormsCountNew(target.Entity);



            if (orb != null && skillMonitor.CanUseSkill(orb) && orbCount < orbLimit)
                return orb;


            if (frostbomb != null && skillMonitor.CanUseSkill(frostbomb))
                return frostbomb;

            // ============================================================
            // 3) FUSILLADE RELEASE LOGIC
            // ============================================================



            // ============================================================
            // 4) CAST FUSILLADE IF RELEASE = FALSE
            // ============================================================
            if (!release && fusillade != null && skillMonitor.CanUseSkill(fusillade))
                return fusillade;




            // ============================================================
            // 5) CAST ARC IF RELEASE = TRUE
            // ============================================================
            if (release && arc != null && skillMonitor.CanUseSkill(arc))
                return arc;


            if (orb != null && skillMonitor.CanUseSkill(orb) && orbCount < orbLimit)
                return orb;

            if (thunderstorm != null && skillMonitor.CanUseSkill(thunderstorm) && skillMonitor.CanUseSkill(firestorm)) // can use thunderstorm but not firestorm
            {
                return thunderstorm;
            }


            // ============================================================
            // 6-7) ALTERNATE BETWEEN FIRESTORM AND THUNDERSTORM
            // ============================================================
            if (skillMonitor.CanUseSkill(firestorm) && skillMonitor.CanUseSkill(thunderstorm))
            {
                if (_useFirestormNext)
                {
                    if (firestorm != null && skillMonitor.CanUseSkill(firestorm))
                    {
                        _useFirestormNext = false;
                        _lastStormCastTime = CurrentTime;
                        return firestorm;
                    }
                    if (thunderstorm != null && skillMonitor.CanUseSkill(thunderstorm))
                    {
                        _useFirestormNext = true;
                        _lastStormCastTime = CurrentTime;
                        return thunderstorm;
                    }
                }
                else
                {
                    if (thunderstorm != null && skillMonitor.CanUseSkill(thunderstorm))
                    {
                        _useFirestormNext = true;
                        _lastStormCastTime = CurrentTime;
                        return thunderstorm;
                    }
                    if (firestorm != null && skillMonitor.CanUseSkill(firestorm))
                    {
                        _useFirestormNext = false;
                        _lastStormCastTime = CurrentTime;
                        return firestorm;
                    }
                }
            }


            if (firestorm != null && skillMonitor.CanUseSkill(firestorm))
            {
                return firestorm;
            }


            if (thunderstorm != null && skillMonitor.CanUseSkill(thunderstorm))
            {
                return thunderstorm;
            }

            return null;
        }

        private ActiveSkill DetermineNormalMonsterSkill(
    EntityInfo target,
    List<ActiveSkill> availableSkills,
    SkillMonitor skillMonitor)
        {
            var player = _gameController.Player;

            var arc = FindSkill(availableSkills, "ArcPlayer");
            var orb = FindSkill(availableSkills, "OrbOfStormsPlayer");
            var eleWeak = FindSkill(availableSkills, "ElementalWeaknessPlayer");
            var fusillade = FindSkill(availableSkills, "EmberFusilladePlayer");
            var frostbomb = FindSkill(availableSkills, "FrostBombPlayer");
            var firestorm = FindSkill(availableSkills, "FirestormPlayer");
            var thunderstorm = FindSkill(availableSkills, "ThunderstormPlayer");

            var metaSpellslingerPlayer = FindSkill(availableSkills, "MetaSpellslingerPlayer"); 
            var metaElementalInvocationPlayer = FindSkill(availableSkills, "MetaElementalInvocationPlayer");

            var (release, stage) = GetFusilladeReleaseState(fusillade);



            //if (metaElementalInvocationPlayer != null && skillMonitor.CanUseSkill(metaElementalInvocationPlayer) && ElementalInvocationCharges() > 0)
            if (metaElementalInvocationPlayer != null && skillMonitor.CanUseSkill(metaElementalInvocationPlayer) && ElementalInvocationPercent() > MIN_ENERGY_FOR_ELE_INVOCATION && FireInfusions() < 3) 
                return metaElementalInvocationPlayer;


            //if (metaSpellslingerPlayer != null && skillMonitor.CanUseSkill(metaSpellslingerPlayer) && SpellSlingerCharges() > 0)
            if (metaSpellslingerPlayer != null && skillMonitor.CanUseSkill(metaSpellslingerPlayer)
                && (SPELLSLINGER_REQUIRES_LIGHTNING_INFUSION || LightningInfusions() > 0)  && SpellSlingerBuffPercent() > MIN_ENERGY_FOR_SPELLSLINGER )
                return metaSpellslingerPlayer;


            // LOADED WITH INFUSIONS, UNLEASH FIRESTORM TO PROC FIRESTORM
            if (thunderstorm != null && skillMonitor.CanUseSkill(thunderstorm) && (LightningInfusions() + FireInfusions() + ColdInfusions())>2 )
            {
                return thunderstorm;
            }


            // ============================================================
            // 1) ORB OF STORMS
            // ============================================================
            int orbCount = GetNearbyOrbOfStormsCount(orb);
            //int orbCount = GetNearbyOrbOfStormsCountNew(target.Entity);

            if (orb != null && skillMonitor.CanUseSkill(orb) && orbCount <= orbLimit/2 && LightningInfusions() ==0)
                return orb;




            if (frostbomb != null && skillMonitor.CanUseSkill(frostbomb))
                return frostbomb;



            // ============================================================
            // 2) CAST FUSILADE IF PREVIOUSLY CHARGIN
            // ============================================================

            if (!release && fusillade != null && skillMonitor.CanUseSkill(fusillade) && stage > 2)
                return fusillade;



            // ============================================================
            // 3) ELEMENTAL WEAKNESS CONDITIONAL ON MAGIC PACK SIZE
            // ============================================================
            bool noWeakness = !HasElementalWeakness(target.Entity);
            const int eleWeakCooldown = 2000; // 2 seconds
            bool canCastEleWeak = _eleWeakFirstCast || (CurrentTime - _lastEleWeakCastTime >= eleWeakCooldown);

            if (eleWeak != null && skillMonitor.CanUseSkill(eleWeak) && canCastEleWeak)
            {

                bool shouldCast = false;

                if (target.Rarity == MonsterRarity.Magic)
                {
                    int nearby = NearbyMonsterCount(target.Entity);

                    if (noWeakness && nearby > 2)
                        shouldCast = true;
                }

                if (shouldCast)
                {
                    _lastEleWeakCastTime = CurrentTime;    // start cooldown timer
                    _eleWeakFirstCast = false;
                    return eleWeak;
                }
            }




            // ============================================================
            // 4) CAST ARC IF UNLEASH CHARGES = 2, cant detect so Ill just cast before charging FUSILLADE
            // ============================================================

            if (arc != null && skillMonitor.CanUseSkill(arc))
            {

            
            double arcCastMs = arc.CastTime.TotalMilliseconds;
            double arcCooldownMs = arcCastMs * 4.0;

            bool canCastArcBeforeFusillade =   _arcFirstCast ||    (CurrentTime - _lastArcCastTime >= arcCooldownMs);

            // Cast ARC first if allowed
            if (arc != null
                && skillMonitor.CanUseSkill(arc)
                && canCastArcBeforeFusillade)
            {
                _lastArcCastTime = CurrentTime;
                _arcFirstCast = false;      // disable free-first-cast
                return arc;
            }
            }

            // ============================================================
            // 5) CAST FUSILLADE IF RELEASE = FALSE
            // ============================================================
            if (!release && fusillade != null && skillMonitor.CanUseSkill(fusillade) && castFusiladeOnNormalMonsters)
                return fusillade;


            // ============================================================
            // 6) CAST ARC IF RELEASE = TRUE
            // ============================================================
            if (release && arc != null && skillMonitor.CanUseSkill(arc))
                return arc;


            // ============================================================
            // 7) CAST ARC default
            // ============================================================
            if (arc != null && skillMonitor.CanUseSkill(arc))
                return arc;

            // ============================================================
            // 8-10) ALTERNATE BETWEEN FIRESTORM AND THUNDERSTORM
            // ============================================================



            if (orb != null && skillMonitor.CanUseSkill(orb) && orbCount < orbLimit)
                return orb;

            if (thunderstorm != null && skillMonitor.CanUseSkill(thunderstorm) && skillMonitor.CanUseSkill(firestorm)) // can use thunderstorm but not firestorm
            {
                return thunderstorm;
            }


            if (skillMonitor.CanUseSkill(firestorm) && skillMonitor.CanUseSkill(thunderstorm))
            {
                if (_useFirestormNext)
                {
                    if (firestorm != null && skillMonitor.CanUseSkill(firestorm))
                    {
                        _useFirestormNext = false;
                        _lastStormCastTime = CurrentTime;
                        return firestorm;
                    }
                    if (thunderstorm != null && skillMonitor.CanUseSkill(thunderstorm))
                    {
                        _useFirestormNext = true;
                        _lastStormCastTime = CurrentTime;
                        return thunderstorm;
                    }
                }
                else
                {
                    if (thunderstorm != null && skillMonitor.CanUseSkill(thunderstorm))
                    {
                        _useFirestormNext = true;
                        _lastStormCastTime = CurrentTime;
                        return thunderstorm;
                    }
                    if (firestorm != null && skillMonitor.CanUseSkill(firestorm))
                    {
                        _useFirestormNext = false;
                        _lastStormCastTime = CurrentTime;
                        return firestorm;
                    }
                }
            }




            if (firestorm != null && skillMonitor.CanUseSkill(firestorm))
            {
                return firestorm;
            }

            if (thunderstorm != null && skillMonitor.CanUseSkill(thunderstorm))
            {
                return thunderstorm;
            }

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

        private int GetNearbyOrbOfStormsCountNew(Entity target)
        {

            try
            {
                return _gameController.Entities
                    .Where(x => x?.Path?.Contains("Metadata/Effects/SleepableEffect") ?? false)
                    .Where(HasOOSAnimation)
                    .Count(x => x.Distance(target) <= ORB_OF_STORM_RADIUS);
            }
            catch (Exception)
            {
                return 0;
            }


        }

        //private int SpellSlingerCharges()
        //{

        //    // support_static_charge
        //    var player = _gameController.Player;
        //    try
        //    {
        //        if (!player.TryGetComponent<Buffs>(out var buffs))
        //            return 0;
        //        var charges = buffs.BuffsList?.FirstOrDefault(buff => buff.Name == "invocation_skill_ready" && buff.SourceSkillId == 46451);

        //        return charges?.BuffCharges ?? 0;
        //    }
        //    catch (Exception)
        //    {
        //        return 0;
        //    }


        //}      
        
        private int SpellSlingerBuffPercent()
        {

            // support_static_charge
            var player = _gameController.Player;
            try
            {
                if (!player.TryGetComponent<Buffs>(out var buffs))
                    return 0;
                var charges = buffs.BuffsList?.FirstOrDefault(buff => buff.Name == "spellslinger_invocation_reserve");

                return charges?.BuffCharges ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }


        }

        private int ElementalInvocationPercent()
        {

            // support_static_charge
            var player = _gameController.Player;
            try
            {
                if (!player.TryGetComponent<Buffs>(out var buffs))
                    return 0;
                var charges = buffs.BuffsList?.FirstOrDefault(buff => buff.Name == "elemental_invocation_reserve");

                return charges?.BuffCharges ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }


        }


        private int LightningInfusions()
        {

            // support_static_charge
            var player = _gameController.Player;
            try
            {
                if (!player.TryGetComponent<Buffs>(out var buffs))
                    return 0;
                var charges = buffs.BuffsList?.FirstOrDefault(buff => buff.Name == "lightning_infusion");

                return charges?.BuffCharges ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }


        }
        private int FireInfusions()
        {

            // support_static_charge
            var player = _gameController.Player;
            try
            {
                if (!player.TryGetComponent<Buffs>(out var buffs))
                    return 0;
                var charges = buffs.BuffsList?.FirstOrDefault(buff => buff.Name == "fire_infusion");

                return charges?.BuffCharges ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }


        }
        private int ColdInfusions()
        {

            // support_static_charge
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

        //private int ElementalInvocationCharges()
        //{

        //    // support_static_charge
        //    var player = _gameController.Player;
        //    try
        //    {
        //        if (!player.TryGetComponent<Buffs>(out var buffs))
        //            return 0;
        //        var charges = buffs.BuffsList?.FirstOrDefault(buff => buff.Name == "invocation_skill_ready" && buff.SourceSkillId == 21502);

        //        return charges?.BuffCharges ?? 0;
        //    }
        //    catch (Exception)
        //    {
        //        return 0;
        //    }


        //}



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
    }
}