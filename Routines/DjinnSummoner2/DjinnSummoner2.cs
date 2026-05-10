using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExilePrecision.Core.Combat;
using ExilePrecision.Core.Combat.Skills;
using ExilePrecision.Core.Combat.State;
using ExilePrecision.Core.Events;
using ExilePrecision.Core.Events.Events;
using ExilePrecision.Features.Targeting;
using ExilePrecision.Features.Targeting.EntityInformation;
using ExilePrecision.Features.Targeting.Priority;
using ExilePrecision.Routines.DjinnSummoner2.Strategy;
using ExilePrecision.Settings;
using ExilePrecision.Utils;
using System;
using System.Linq;
using System.Numerics;

namespace ExilePrecision.Routines.DjinnSummoner2
{
    public class DjinnSummoner2 : OrbWalkingRoutineBase
    {
        private readonly TargetSelector _targetSelector;
        private readonly SkillPriority _skillPriority;
        private readonly LineOfSight _lineOfSight;
        private readonly PriorityCalculator _priorityCalculator; // ← stored as field

        public DjinnSummoner2(GameController gameController)
            : base("DjinnSummoner2", gameController)
        {
            _lineOfSight = new LineOfSight(gameController);

            var entityScanner = new EntityScanner(gameController, _lineOfSight);
            var priorityCalculator = new PriorityCalculator(gameController);
            _priorityCalculator = new PriorityCalculator(gameController); // ← assign to field
            _targetSelector = new TargetSelector(
                gameController,
                entityScanner,
                priorityCalculator,
                _lineOfSight
            );

            _targetSelector.Configure();
            _skillPriority = new SkillPriority(gameController);

            var eventBus = EventBus.Instance;
            eventBus.Subscribe<RenderEvent>(HandleRender);
        }

        protected override void InitializeSkills()
        {
            try
            {
                SkillHandler.Initialize();
                StateCoordinator.SetState(RoutineState.Idle);
            }
            catch (Exception ex)
            {
                LogError($"Error initializing skills: {ex.Message}");
                StateCoordinator.SetError(ex);
            }
        }

        protected override EntityInfo GetTarget()
        {
            // Example:
            var allSkills = SkillHandler.GetAllSkills();
            _priorityCalculator.SetEssenceDrainAvailable(allSkills.Any(s => s.Name == "EssenceDrainPlayer"));
            _priorityCalculator.SetContagionAvailable(allSkills.Any(s => s.Name == "ContagionPlayer"));
            _targetSelector.Update();
            var target = _targetSelector.GetCurrentTarget();
            return target != null ? new EntityInfo(target, GameController) : null;
        }

        protected override void ExecuteCombatTick()
        {
            if (CurrentTarget == null) return;

            var nextSkill = _skillPriority.GetNextSkill(
                CurrentTarget,
                SkillHandler.GetAllSkills(),
                SkillMonitor);

            if (nextSkill != null)
            {
                var screenPos = CurrentTarget.ScreenPos;
                if (screenPos != Vector2.Zero)
                {

                    var posToUseSkill = screenPos;

             
                    ExileCore2.Input.SetCursorPos(posToUseSkill);

                    if (IsCursorOnTarget(CurrentTarget))
                    {
                        SkillMonitor.TrackUse(nextSkill);
                        SkillHandler.UseSkill(nextSkill.Name);
                    }
                }
            }
        }

        private void HandleRender(RenderEvent evt)
        {
            if (!ExilePrecision.Instance.Settings.Render.EnableRendering) return;

            try
            {
                CombatRenderer.Render(evt.Graphics, CurrentTarget, StateCoordinator.CurrentState);
            }
            catch (Exception ex)
            {
                LogError($"Error in render: {ex.Message}");
            }
        }

        protected override void HandleAreaChange(AreaChangeEvent evt)
        {
            _targetSelector?.Clear();
            StateCoordinator.Reset();
            base.HandleAreaChange(evt);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var eventBus = EventBus.Instance;
                eventBus.Unsubscribe<RenderEvent>(HandleRender);
                _targetSelector?.Clear();
            }
            base.Dispose(disposing);
        }
    }
}