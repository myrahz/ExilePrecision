using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore2;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.MemoryObjects;
using ExilePrecision.Core.Combat;
using ExilePrecision.Core.Events;
using ExilePrecision.Core.Events.Events;
using ExilePrecision.Settings;

namespace ExilePrecision
{
    public class ExilePrecision : BaseSettingsPlugin<ExilePrecisionSettings>
    {
        private IRoutine _activeRoutine;
        private bool _isToggled;

        public ExilePrecision()
        {
            Name = "Exile Precision";
        }

        public override bool Initialise()
        {
            try
            {
                Input.RegisterKey(Settings.PrecisionKey);
                Input.RegisterKey(Settings.PrecisionToggleKey);

                Settings.PrecisionKey.OnValueChanged += () => Input.RegisterKey(Settings.PrecisionKey);
                Settings.PrecisionToggleKey.OnValueChanged += () =>
                {
                    Input.RegisterKey(Settings.PrecisionToggleKey);
                    _isToggled = false;
                };

                var routineSelector = new CombatRoutineSelector(GameController, Settings);

                var availableRoutines = routineSelector.GetAvailableRoutines();
                Settings.Combat.AvailableStrategies.SetListValues(availableRoutines);

                if (Settings.Combat.AvailableStrategies.Values.Count == 0)
                {
                    DebugWindow.LogError($"[{Name}] No combat routines available");
                    return false;
                }

                try
                {
                    if (!string.IsNullOrEmpty(Settings.Combat.AvailableStrategies.Value))
                    {
                        _activeRoutine = routineSelector.GetRoutine();
                        if (_activeRoutine == null)
                        {
                            DebugWindow.LogError($"[{Name}] Failed to create combat routine");
                            return false;
                        }
                        if (!_activeRoutine.Initialize())
                        {
                            DebugWindow.LogError($"[{Name}] Failed to initialize combat routine");
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugWindow.LogError($"[{Name}] Failed to initialize combat routine: {ex.Message}");
                    return false;
                }

                Settings.Combat.AvailableStrategies.OnValueSelected += (strategy) =>
                {
                    DebugWindow.LogMsg($"[{Name}] Selected strategy: {strategy}");
                    _activeRoutine?.Dispose();
                    _activeRoutine = routineSelector.GetRoutine();
                    _activeRoutine?.Initialize();
                };

                return true;
            }
            catch (Exception ex)
            {
                DebugWindow.LogError($"[{Name}] Failed to initialize: {ex.Message}");
                return false;
            }
        }

        public override void AreaChange(AreaInstance area)
        {
            _isToggled = false;
            if (_activeRoutine != null)
            {
                DebugWindow.LogMsg($"[Core] AreaChange() -> Calling OnAreaChange");
                _activeRoutine.OnAreaChange(area);
            }

            EventBus.Instance.Publish(new AreaChangeEvent { NewArea = area });
        }

        public override void EntityAdded(Entity entity)
        {
            if (entity != null && entity.IsValid)
            {
                EventBus.Instance.Publish(new EntityDiscoveredEvent
                {
                    Entity = entity,
                    Distance = entity.DistancePlayer
                });
            }
        }

        public override void Tick()
        {
            if (!ShouldProcess()) return;

            try
            {
                if (Settings.PrecisionToggleKey.PressedOnce())
                {
                    _isToggled = !_isToggled;
                }

                var isActive = _isToggled || Input.GetKeyState(Settings.PrecisionKey);
                if (!isActive)
                {
                    _activeRoutine?.Stop();
                    return;
                }

                if (isActive && _activeRoutine != null)
                {
                    _activeRoutine.Execute();
                }

                // TODO: Implement EventBus for LightningArrowRoutine
                if (Settings.Combat.EnableCombatMode)
                {
                    EventBus.Instance.Publish(new CombatStateChangedEvent
                    {
                        IsInCombat = true,
                        CurrentTarget = null  // Let routines handle their own targeting
                    });
                }
            }
            catch (Exception ex)
            {
                DebugWindow.LogError($"[{Name}] Error in Tick: {ex.Message}");
            }
        }

        public override void Render()
        {
            if (!Settings.Render.EnableRendering) return;

            try
            {
                if (_activeRoutine != null)
                {
                    _activeRoutine.Render(Graphics);
                }
            }
            catch (Exception ex)
            {
                DebugWindow.LogError($"[{Name}] Error in Render: {ex.Message}");
            }
        }

        private bool ShouldProcess()
        {
            if (!Settings.Enable) return false;
            if (GameController?.InGame != true) return false;
            if (GameController.Player == null) return false;
            if (GameController.Settings.CoreSettings.Enable) return false;

            return ValidateUIState();
        }

        private bool ValidateUIState()
        {
            var ingameUI = GameController?.IngameState?.IngameUi;
            if (ingameUI == null) return false;

            if (!Settings.Render.Interface.EnableWithFullscreenUI &&
                ingameUI.FullscreenPanels.Any(x => x.IsVisible))
                return false;

            if (!Settings.Render.Interface.EnableWithLeftPanel &&
                ingameUI.OpenLeftPanel.IsVisible)
                return false;

            if (!Settings.Render.Interface.EnableWithRightPanel &&
                ingameUI.OpenRightPanel.IsVisible)
                return false;

            return true;
        }
    }
}