using ExilePrecision.Core.Combat.Skills;
using ExilePrecision.Core.Combat.State;
using ExilePrecision.Features.Targeting.EntityInformation;
using ExileCore2;
using System;

namespace ExilePrecision.Core.Combat
{
    public interface IRoutine : IDisposable
    {
        string Name { get; }
        bool CanExecute { get; }
        RoutineState State { get; }
        bool Initialize();
        void Execute();
        void UpdateTarget(EntityInfo target);
        void OnAreaChange(AreaInstance area);
        void Render(Graphics graphics);
        void Stop();
    }
}