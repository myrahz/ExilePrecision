using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ExilePrecision.Core.Events;
using ExilePrecision.Core.Events.Events;

namespace ExilePrecision.Features.Targeting.Priority
{
    public class PriorityCalculator
    {
        private readonly GameController _gameController;
        private readonly Dictionary<Entity, float> _currentWeights;
        private readonly Dictionary<Entity, Life> _lifeCache;
        private readonly Dictionary<Entity, MonsterRarity> _rarityCache;
        private readonly object _lock = new();

        private float _distanceWeight = 2.0f;
        private float _healthWeight = 1.0f;
        private float _rarityWeight = 1.0f;
        private float _maxTargetDistance = 100f;
        private bool _preferHigherHealth;
        private bool _essenceDrainAvailable = false;
        private bool _contagionAvailable = false;

        private const int CACHE_CLEANUP_INTERVAL = 120;
        private int _frameCounter;

        public PriorityCalculator(GameController gameController)
        {
            _gameController = gameController;
            _currentWeights = new Dictionary<Entity, float>();
            _lifeCache = new Dictionary<Entity, Life>();
            _rarityCache = new Dictionary<Entity, MonsterRarity>();
        }

        public void Configure(
            float distanceWeight,
            float healthWeight,
            float rarityWeight,
            float maxTargetDistance,
            bool preferHigherHealth)
        {
            _distanceWeight = distanceWeight;
            _healthWeight = healthWeight;
            _rarityWeight = rarityWeight;
            _maxTargetDistance = maxTargetDistance;
            _preferHigherHealth = preferHigherHealth;
        }

        // Call this from DjinnSummoner2.GetTarget() before _targetSelector.Update()
        // so the weight calculation uses the correct targeting mode.
        // Example: _priorityCalculator.SetEssenceDrainAvailable(
        //              SkillHandler.GetAllSkills().Any(s => s.Name == "EssenceDrainPlayer"));
        public void SetEssenceDrainAvailable(bool available)
        {
            _essenceDrainAvailable = available;
        }

        public void SetContagionAvailable(bool available)
        {
            _contagionAvailable = available;
        }

        public void UpdatePriorities(IEnumerable<Entity> entities)
        {
            if (_gameController?.Player == null) return;

            var playerPos = _gameController.Player.GridPos;
            _frameCounter++;

            foreach (var entity in entities)
            {
                if (!IsEntityValid(entity)) continue;

                var distance = Vector2.Distance(playerPos, entity.GridPos);
                var newWeight = CalculateWeight(entity, playerPos, distance); // pass distance in

                float oldWeight;
                bool changed;

                lock (_lock)
                {
                    _currentWeights.TryGetValue(entity, out oldWeight);
                    changed = Math.Abs(oldWeight - newWeight) > 0.1f;
                    if (changed)
                        _currentWeights[entity] = newWeight;
                }

                if (changed)
                {
                    EventBus.Instance.Publish(new TargetPriorityChangedEvent(
                        entity,
                        oldWeight,
                        newWeight,
                        distance)); // reuse, no second sqrt
                }
            }

            if (_frameCounter >= CACHE_CLEANUP_INTERVAL)
            {
                CleanupCaches();
                _frameCounter = 0;
            }
        }
        private float CalculateWeight(Entity entity, Vector2 playerPos, float distance)
        {
            if (distance > _maxTargetDistance) return 0f;

            var distanceNormalized = 1f - (distance / _maxTargetDistance);

            if (_essenceDrainAvailable)
            {
                bool hasED = HasEssenceDrainDebuff(entity);
                bool hasContagion = HasContagionDebuff(entity);
                bool hasBoth = hasED && hasContagion;
                var rarity = GetMonsterRarity(entity);
                bool isElite = rarity == MonsterRarity.Rare || rarity == MonsterRarity.Unique;

                float baseDistanceScore = distanceNormalized * distanceNormalized * _distanceWeight;

                // Has both — already handled, very low priority regardless of rarity
                if (hasBoth)
                    return baseDistanceScore * 0.1f;

                // Near a monster with both debuffs — ED will chain to it, low priority
                // Elites bypass this penalty because they're always worth targeting directly
                if (!isElite && IsNearMonsterWithBothDebuffs(entity))
                    return baseDistanceScore * 0.1f;

                // Has exactly one debuff — highest urgency, apply the missing half
                if (hasED || hasContagion)
                    return baseDistanceScore + 5f;

                // Has neither — high priority, start the Contagion → ED cycle
                return baseDistanceScore + 3f;
            }


            var weight = 0f;
            weight += distanceNormalized * distanceNormalized * _distanceWeight;
            weight += CalculateHealthPriority(entity, distanceNormalized);
            weight += CalculateRarityPriority(entity, distanceNormalized);
            weight += CalculateContagionPriority(entity);
            return weight;
        }
        

        private float CalculateHealthPriority(Entity entity, float distanceFactor)
        {
            var life = GetLifeComponent(entity);
            if (life == null) return 0f;

            var totalHealthPercent = life.HPPercentage + life.ESPercentage;
            var healthPriority = _preferHigherHealth ? totalHealthPercent : (1f - totalHealthPercent);

            return healthPriority * _healthWeight * distanceFactor;
        }

        private float CalculateRarityPriority(Entity entity, float distanceFactor)
        {
            var rarity = GetMonsterRarity(entity);

            var rarityMultiplier = rarity switch
            {
                MonsterRarity.Unique => 4.0f,
                MonsterRarity.Rare => 3.0f,
                MonsterRarity.Magic => 2.0f,
                _ => 1.0f
            };

            return rarityMultiplier * _rarityWeight * distanceFactor;
        }

        // Contagion priority: reward dying monsters that have neighbours to spread to.
        //   Normal  → always gets the bonus (dies fast, great spread vector)
        //   Magic   → only below 40 % HP
        //   Rare    → only below 20 % HP
        //   Unique  → no bonus (lives too long to be a useful vector)
        //   Isolated (no other monster within 30 units) → no bonus regardless
        private const float CONTAGION_PRIORITY_BONUS = 5.0f;
        private const float CONTAGION_FINISH_PRIORITY_BONUS = 10.0f;
        private const float CONTAGION_SPREAD_RADIUS = 30f;
        private float CalculateContagionPriority(Entity entity)
        {
            if (!_contagionAvailable) return 0f;

            try
            {
                bool hasContagion = HasContagionDebuff(entity);
                bool hasED = HasEssenceDrainDebuff(entity);

                // IMPORTANT:
                // If monster already has Contagion but not ED,
                // we want to STICK to it and kill/apply ED ASAP.
                if (hasContagion && !hasED)
                    return CONTAGION_FINISH_PRIORITY_BONUS;

                // If it already has both, no need to focus it.
                if (hasContagion && hasED)
                    return 0f;

                // No point if isolated — contagion won't spread
                bool hasNeighbour = _gameController.EntityListWrapper
                    .ValidEntitiesByType[EntityType.Monster]
                    .Any(x =>
                        x != entity &&
                        x.IsAlive &&
                        x.IsTargetable &&
                        Vector2.Distance(x.GridPos, entity.GridPos) <= CONTAGION_SPREAD_RADIUS);

                if (!hasNeighbour)
                    return 0f;

                var rarity = entity.GetComponent<ObjectMagicProperties>()?.Rarity
                             ?? MonsterRarity.White;

                var life = entity.GetComponent<Life>();
                float hp = life?.HPPercentage ?? 1f;

                return rarity switch
                {
                    MonsterRarity.White => CONTAGION_PRIORITY_BONUS,
                    MonsterRarity.Magic => hp <= 0.40f ? CONTAGION_PRIORITY_BONUS : 0f,
                    MonsterRarity.Rare => hp <= 0.20f ? CONTAGION_PRIORITY_BONUS : 0f,
                    _ => 0f,
                };
            }
            catch
            {
                return 0f;
            }
        }

        private bool HasEssenceDrainDebuff(Entity entity)
        {
            try
            {
                if (!entity.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                    return false;
                return buffs.BuffsList.Any(b => b.Name == "siphon_damage");
            }
            catch { return false; }
        }

        private bool HasContagionDebuff(Entity entity)
        {
            try
            {
                if (!entity.TryGetComponent<Buffs>(out var buffs) || buffs.BuffsList == null)
                    return false;
                return buffs.BuffsList.Any(b => b.Name == "contagion");
            }
            catch { return false; }
        }

        // True if any monster within ED chain range already carries both debuffs.
        // Those monsters will die soon and chain ED + spread Contagion to this entity,
        // so there is no need to target it directly.
        private const float ED_CHAIN_RADIUS = 35f;

        private bool IsNearMonsterWithBothDebuffs(Entity entity)
        {
            try
            {
                return _gameController.EntityListWrapper
                    .ValidEntitiesByType[EntityType.Monster]
                    .Any(x =>
                        x != entity &&
                        x.IsAlive &&
                        x.IsTargetable &&
                        Vector2.Distance(x.GridPos, entity.GridPos) <= ED_CHAIN_RADIUS &&
                        HasEssenceDrainDebuff(x) &&
                        HasContagionDebuff(x));
            }
            catch { return false; }
        }

        private int CountNearbyEntities(Entity entity, float radius)
        {
            try
            {
                return _gameController.EntityListWrapper
                    .ValidEntitiesByType[EntityType.Monster]
                    .Count(x =>
                        x != entity &&
                        x.IsAlive &&
                        x.IsTargetable &&
                        Vector2.Distance(x.GridPos, entity.GridPos) <= radius);
            }
            catch { return 0; }
        }

        private Life GetLifeComponent(Entity entity)
        {
            lock (_lock)
            {
                if (!_lifeCache.TryGetValue(entity, out var life))
                {
                    life = entity.GetComponent<Life>();
                    if (life != null)
                    {
                        _lifeCache[entity] = life;
                    }
                }
                return life;
            }
        }

        private MonsterRarity GetMonsterRarity(Entity entity)
        {
            lock (_lock)
            {
                if (!_rarityCache.TryGetValue(entity, out var rarity))
                {
                    rarity = entity.GetComponent<ObjectMagicProperties>()?.Rarity ?? MonsterRarity.White;
                    _rarityCache[entity] = rarity;
                }
                return rarity;
            }
        }

        private float GetCurrentWeight(Entity entity)
        {
            lock (_lock)
            {
                return _currentWeights.TryGetValue(entity, out var weight) ? weight : 0f;
            }
        }

        private bool IsEntityValid(Entity entity)
        {
            if (entity == null) return false;
            if (!entity.IsValid) return false;
            if (entity.Address == 0) return false;

            try
            {
                var pos = entity.GridPos;
                var isAlive = entity.IsAlive;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void CleanupCaches()
        {
            lock (_lock)
            {
                var invalidEntities = new List<Entity>();

                foreach (var entity in _currentWeights.Keys)
                {
                    if (!IsEntityValid(entity))
                    {
                        invalidEntities.Add(entity);
                    }
                }

                foreach (var entity in invalidEntities)
                {
                    _currentWeights.Remove(entity);
                    _lifeCache.Remove(entity);
                    _rarityCache.Remove(entity);
                }
            }
        }

        public float? GetEntityWeight(Entity entity)
        {
            lock (_lock)
            {
                return _currentWeights.TryGetValue(entity, out float weight) ? weight : null;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _currentWeights.Clear();
                _lifeCache.Clear();
                _rarityCache.Clear();
                _frameCounter = 0;
            }
        }
    }
}