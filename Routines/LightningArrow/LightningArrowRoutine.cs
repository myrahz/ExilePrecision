using ExileCore2;
using ExilePrecision.Core.Combat;
using ExilePrecision.Core.Combat.Skills;
using ExilePrecision.Core.Combat.State;
using ExilePrecision.Features.Targeting.EntityInformation;
using ExilePrecision.Settings;
using ExilePrecision.Features.Targeting;
using ExilePrecision.Features.Targeting.Priority;
using ExilePrecision.Routines.LightningArrow.Strategy;
using System;
using System.Numerics;
using ExilePrecision.Utils;

namespace ExilePrecision.Routines.LightningArrow
{
    public class LightningArrowRoutine : OrbWalkingRoutineBase
    {
        private readonly TargetSelector _targetSelector;
        private readonly SkillPriority _skillPriority;
        private readonly LineOfSight _lineOfSight;

        public LightningArrowRoutine(GameController gameController, ExilePrecisionSettings settings)
            : base("Lightning Arrow", gameController, settings)
        {
            _lineOfSight = new LineOfSight(gameController);

            var entityScanner = new EntityScanner(gameController);
            var priorityCalculator = new PriorityCalculator(gameController);

            _targetSelector = new TargetSelector(
                gameController,
                entityScanner,
                priorityCalculator,
                settings,
                _lineOfSight
            );

            _targetSelector.Configure();
            _skillPriority = new SkillPriority(gameController);
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

        public override void Execute()
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

                UpdateTarget(new EntityInfo(target, GameController));
                if (CurrentTarget == null)
                {
                    StateCoordinator.SetState(RoutineState.Idle);
                    return;
                }

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

        protected override bool ValidateTarget()
        {
            if (CurrentTarget == null) return false;
            if (!CurrentTarget.IsValid) return false;
            if (!CurrentTarget.IsAlive) return false;
            if (CurrentTarget.IsHidden) return false;
            if (!CurrentTarget.IsHostile) return false;
            if (CurrentTarget.Distance > Settings.Targeting.MaxTargetRange) return false;

            var entity = CurrentTarget.Entity;
            if (entity == null || !entity.IsValid || !entity.IsAlive || entity.IsDead)
                return false;

            return true;
        }

        public override void Stop()
        {
            StateCoordinator.SetState(RoutineState.Inactive);
            SkillHandler.ReleaseAllSkills();

            base.Stop();
        }

        protected override void RenderRoutineSpecific(Graphics graphics)
        {
            if (Settings.Render.ShowTerrainDebug)
                _lineOfSight.Render(graphics);
        }

        public override void OnAreaChange(AreaInstance area)
        {
            _targetSelector?.Clear();
            StateCoordinator.Reset();

            _targetSelector?.OnAreaChange();
            base.OnAreaChange(area);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _targetSelector?.Clear();
            }
            base.Dispose(disposing);
        }
    }
}