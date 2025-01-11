using ExileCore2;
using ExilePrecision.Core.Combat;
using ExilePrecision.Core.Combat.Skills;
using ExilePrecision.Core.Combat.State;
using ExilePrecision.Features.Targeting.EntityInformation;
using ExilePrecision.Settings;
using ExilePrecision.Features.Targeting;
using ExilePrecision.Features.Targeting.Priority;
using ExilePrecision.Routines.LightningArrow.Strategy;
using ExilePrecision.Utils;
using ExilePrecision.Core.Events;
using ExilePrecision.Core.Events.Events;
using System;
using System.Numerics;

namespace ExilePrecision.Routines.LightningArrow
{
    public class LightningArrowRoutine : OrbWalkingRoutineBase
    {
        private readonly TargetSelector _targetSelector;
        private readonly SkillPriority _skillPriority;
        private readonly LineOfSight _lineOfSight;

        public LightningArrowRoutine(GameController gameController)
            : base("Lightning Arrow", gameController)
        {
            _lineOfSight = new LineOfSight(gameController);

            var entityScanner = new EntityScanner(gameController, _lineOfSight);
            var priorityCalculator = new PriorityCalculator(gameController);

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

        protected override void HandleAreaChange(AreaChangeEvent evt)
        {
            _targetSelector?.Clear();
            StateCoordinator.Reset();
            base.HandleAreaChange(evt);
        }

        protected override void OnTickActive()
        {
            if (!CanExecute)
            {
                Stop();
                return;
            }

            try
            {
                _targetSelector.Update();
                var target = _targetSelector.GetCurrentTarget();

                if (target == null)
                {
                    StateCoordinator.SetState(RoutineState.Idle);
                    SkillHandler.ReleaseAllSkills();
                    return;
                }

                if (CurrentTarget != null && CurrentTarget.Entity.Address != target.Address)
                {
                    SkillHandler.ReleaseAllSkills();
                }

                var entityInfo = new EntityInfo(target, GameController);
                var oldTarget = CurrentTarget;
                CurrentTarget = entityInfo;

                if (!ValidateTarget())
                {
                    CurrentTarget = null;
                    StateCoordinator.SetState(RoutineState.Idle);
                    return;
                }

                EventBus.Instance.Publish(new TargetChangedEvent
                {
                    OldTarget = oldTarget,
                    NewTarget = CurrentTarget
                });

                StateCoordinator.SetState(RoutineState.Active);

                var nextSkill = _skillPriority.GetNextSkill(
                    CurrentTarget,
                    SkillHandler.GetAllSkills(),
                    SkillMonitor);

                if (nextSkill != null)
                {
                    var screenPos = CurrentTarget.ScreenPos;
                    if (screenPos != Vector2.Zero)
                    {
                        ExileCore2.Input.SetCursorPos(screenPos);

                        if (IsCursorOnTarget(CurrentTarget))
                        {
                            SkillMonitor.TrackUse(nextSkill);
                            SkillHandler.UseSkill(nextSkill.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error executing routine: {ex.Message}");
                Stop();
                StateCoordinator.SetError(ex);
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

        protected override bool ValidateTarget()
        {
            if (!base.ValidateTarget()) return false;
            if (!CurrentTarget.IsHostile) return false;
            if (CurrentTarget.Distance > ExilePrecision.Instance.Settings.Targeting.MaxTargetRange) return false;

            var entity = CurrentTarget.Entity;
            return entity != null && entity.IsValid && entity.IsAlive && !entity.IsDead;
        }

        public override void Stop()
        {
            StateCoordinator.SetState(RoutineState.Inactive);
            SkillHandler.ReleaseAllSkills();

            base.Stop();
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