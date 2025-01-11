using System;
using System.Numerics;
using ExileCore2;
using ExilePrecision.Core.Combat;
using ExilePrecision.Core.Combat.Skills;
using ExilePrecision.Features.Targeting.EntityInformation;
using ExilePrecision.Settings;

namespace ExilePrecision.Core.Combat
{
    public abstract class OrbWalkingRoutineBase : RoutineBase
    {
        private Vector2 _originalMousePosition;
        private Vector2 _lastAttackMousePosition;
        private bool _hasStoredPosition;
        private DateTime _lastAttackTime;
        private DateTime _lastMoveTime;
        private const string MOVE_SKILL_NAME = "Move";
        private const float ATTACK_MOVE_DELAY = 0.1f;
        private const float MOVE_DURATION = 0.05f;

        protected OrbWalkingRoutineBase(string name, GameController gameController, ExilePrecisionSettings settings)
            : base(name, gameController, settings)
        {
            ResetState();
        }

        protected void BeginAttacking(EntityInfo target)
        {
            if (!_hasStoredPosition)
            {
                _originalMousePosition = ExileCore2.Input.MousePosition;
                _hasStoredPosition = true;
            }
            _lastAttackMousePosition = ExileCore2.Input.MousePosition;
        }

        protected void FinishAttack()
        {
            _lastAttackTime = DateTime.Now;
            _lastAttackMousePosition = ExileCore2.Input.MousePosition;
        }

        protected void StopAttacking()
        {
            if (_hasStoredPosition)
            {
                ExileCore2.Input.SetCursorPos(_originalMousePosition);
                ResetState();
            }
        }

        protected bool ShouldWeaveMove()
        {
            if (!_hasStoredPosition) return false;

            var now = DateTime.Now;
            var timeSinceAttack = (now - _lastAttackTime).TotalSeconds;
            var timeSinceMove = (now - _lastMoveTime).TotalSeconds;

            return timeSinceAttack >= ATTACK_MOVE_DELAY && timeSinceMove >= MOVE_DURATION;
        }

        protected void WeaveMove()
        {
            if (ShouldWeaveMove())
            {
                ExileCore2.Input.SetCursorPos(_lastAttackMousePosition);

                var moveSkill = SkillHandler.GetSkill(MOVE_SKILL_NAME);
                if (moveSkill != null)
                {
                    SkillHandler.UseSkill(MOVE_SKILL_NAME, true);
                    _lastMoveTime = DateTime.Now;
                }
            }
        }

        protected void FollowMouseWithMove()
        {
            var moveSkill = SkillHandler.GetSkill(MOVE_SKILL_NAME);
            if (moveSkill != null)
            {
                SkillHandler.UseSkill(MOVE_SKILL_NAME, true);
            }
        }

        private void ResetState()
        {
            _originalMousePosition = Vector2.Zero;
            _lastAttackMousePosition = Vector2.Zero;
            _hasStoredPosition = false;
            _lastAttackTime = DateTime.MinValue;
            _lastMoveTime = DateTime.MinValue;

            SkillHandler.ReleaseAllSkills();
        }

        protected override void OnTargetChanged(EntityInfo oldTarget, EntityInfo newTarget)
        {
            base.OnTargetChanged(oldTarget, newTarget);

            if (newTarget == null)
            {
                StopAttacking();
            }
        }

        public override void Stop()
        {
            StopAttacking();
            base.Stop();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopAttacking();
            }
            base.Dispose(disposing);
        }
    }
}