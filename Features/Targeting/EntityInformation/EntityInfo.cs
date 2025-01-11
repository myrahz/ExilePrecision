using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using System.Numerics;

namespace ExilePrecision.Features.Targeting.EntityInformation
{
    public class EntityInfo
    {
        private readonly Entity _entity;
        private readonly Life _life;
        private readonly GameController _gameController;

        public EntityInfo(Entity entity, GameController gameController)
        {
            _entity = entity;
            _gameController = gameController;
            _life = entity?.GetComponent<Life>();
        }

        public uint Id => _entity?.Id ?? 0;
        public string Path => _entity?.Path;
        public Vector3 Pos => _entity?.Pos ?? Vector3.Zero;
        public Vector2 GridPos => _entity?.GridPos ?? Vector2.Zero;
        public float Distance => _entity?.DistancePlayer ?? float.MaxValue;

        public Vector2 ScreenPos => _entity != null ?
            _gameController.IngameState.Camera.WorldToScreen(_entity.Pos) :
            Vector2.Zero;

        public bool IsValid => _entity?.IsValid ?? false;
        public bool IsAlive => _entity?.IsAlive ?? false;
        public bool IsTargetable => _entity?.IsTargetable ?? false;
        public bool IsHidden => _entity?.IsHidden ?? true;
        public bool IsHostile => _entity?.IsHostile ?? false;

        public float HPPercentage => _life?.HPPercentage ?? 0;
        public float ESPercentage => _life?.ESPercentage ?? 0;
        public MonsterRarity Rarity => _entity?.Rarity ?? MonsterRarity.White;

        public Entity Entity => _entity;
    }
}