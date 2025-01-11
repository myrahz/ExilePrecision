using ExilePrecision.Core.Combat.Skills;
using ExilePrecision.Core.Combat.State;
using ExilePrecision.Features.Targeting.EntityInformation;
using ExilePrecision.Features.Input;
using ExileCore2;
using System;
using ExilePrecision.Settings;
using System.Numerics;
using ExilePrecision.Features.Rendering;

namespace ExilePrecision.Core.Combat
{
    public abstract class RoutineBase : IRoutine
    {
        protected readonly GameController GameController;
        protected readonly ExilePrecisionSettings Settings;
        protected readonly SkillMonitor SkillMonitor;
        protected readonly SkillHandler SkillHandler;
        protected readonly KeyHandler KeyHandler;
        protected readonly StateCoordinator StateCoordinator;
        protected readonly ICombatRenderer CombatRenderer;

        protected EntityInfo CurrentTarget;
        protected bool IsInitialized;
        protected bool IsDisposed;

        public string Name { get; }
        public bool CanExecute => ValidateExecutionState();
        public RoutineState State => StateCoordinator.CurrentState;

        protected RoutineBase(string name, GameController gameController, ExilePrecisionSettings settings)
        {
            Name = name;
            GameController = gameController;
            Settings = settings;

            SkillMonitor = new SkillMonitor();
            SkillHandler = new SkillHandler(gameController, settings);
            KeyHandler = new KeyHandler();
            StateCoordinator = new StateCoordinator();
            CombatRenderer = new CombatRenderer(gameController, settings);
        }

        public virtual bool Initialize()
        {
            if (IsDisposed)
                return false;

            try
            {
                if (!ValidateGameState())
                    return false;

                InitializeSkills();
                IsInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize routine: {ex.Message}");
                return false;
            }
        }

        public abstract void Execute();

        public virtual void UpdateTarget(EntityInfo target)
        {
            if (IsDisposed) return;

            try
            {
                var previousTarget = CurrentTarget;
                CurrentTarget = target;

                if (previousTarget?.Id != target?.Id)
                {
                    OnTargetChanged(previousTarget, target);
                }
            }
            catch (Exception ex)
            {
                LogError($"Error updating target: {ex.Message}");
                Stop();
            }
        }

        public virtual void OnAreaChange(AreaInstance area)
        {
            if (IsDisposed) return;

            try
            {
                Stop();
                InitializeSkills();
                StateCoordinator.Reset();
            }
            catch (Exception ex)
            {
                LogError($"Error handling area change: {ex.Message}");
            }
        }

        public virtual void Render(Graphics graphics)
        {
            if (!CanExecute || IsDisposed) return;

            try
            {
                CombatRenderer.Render(graphics, CurrentTarget, StateCoordinator.CurrentState);
                RenderRoutineSpecific(graphics);
            }
            catch (Exception ex)
            {
                LogError($"Error in render: {ex.Message}");
            }
        }

        protected virtual void RenderRoutineSpecific(Graphics graphics)
        {
            // Override in derived classes to render routine-specific content
        }

        public virtual void Stop()
        {
            if (IsDisposed) return;

            try
            {
                KeyHandler.ReleaseAll();
                StateCoordinator.Reset();
                CurrentTarget = null;
            }
            catch (Exception ex)
            {
                LogError($"Error stopping routine: {ex.Message}");
            }
        }

        protected virtual void OnTargetChanged(EntityInfo oldTarget, EntityInfo newTarget)
        {
            // Override in derived classes to handle target changes but will soon convert to EventBus
        }

        protected virtual void InitializeSkills()
        {
            // Override in derived classes to initialize routine-specific skills
        }

        protected bool IsCursorOnTarget(EntityInfo target)
        {
            var cursorPos = ExileCore2.Input.MousePosition;
            var targetPos = GameController.IngameState.Camera.WorldToScreen(target.Pos);

            return Vector2.Distance(cursorPos, targetPos) <= Settings.Combat.CombatRange.Value;
        }

        protected bool ValidateExecutionState()
        {
            if (!IsInitialized || IsDisposed)
                return false;

            return ValidateGameState();
        }

        protected virtual bool ValidateGameState()
        {
            return GameController != null &&
                   GameController.Game.IngameState?.Data != null &&
                   GameController.Game.IngameState.IngameUi != null &&
                   GameController.Player != null;
        }

        protected virtual bool ValidateTarget()
        {
            return CurrentTarget != null &&
                   CurrentTarget.IsValid &&
                   CurrentTarget.IsAlive &&
                   !CurrentTarget.IsHidden;
        }

        protected void LogError(string message)
        {
            DebugWindow.LogError($"[{Name}] {message}");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    try
                    {
                        Stop();
                        KeyHandler.Dispose();
                        SkillMonitor.Reset();
                        CombatRenderer.Clear();
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error disposing routine: {ex.Message}");
                    }
                }
                IsDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~RoutineBase()
        {
            Dispose(false);
        }
    }
}