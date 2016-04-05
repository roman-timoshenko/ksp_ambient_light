/*
Ambient Light Adjustment - Modify ambient lighting in Kerbal Space Program.
Copyright (C) 2014 Maik Schreiber

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using KSP.UI.Screens;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AmbientLightAdjustment {
    internal interface IToolbarButton
    {
        void Destroy();

        bool IsLevelUIVisible();
        void ShowLevelUI(Action levelChangedCallback);
        void HideLevelUI();
        void DrawLevelUI();

        float getLevel();
        void setLevel(float value);
    }

    internal class BlizzyButton : IToolbarButton
    {
        private IButton wrapped;

        internal BlizzyButton(IButton wrapped)
        {
            this.wrapped = wrapped;
        }

        public void Destroy()
        {
            wrapped.Destroy();
        }

        public bool IsLevelUIVisible()
        {
            return (wrapped.Drawable != null);
        }

        public void ShowLevelUI(Action levelChangedCallback)
        {
            AdjustmentDrawable adjustment = new AdjustmentDrawable();
            adjustment.OnLevelChanged += levelChangedCallback;

            wrapped.Drawable = adjustment;
        }

        public void HideLevelUI()
        {
            wrapped.Drawable = null;
        }

        public void DrawLevelUI()
        {
            // blizzy's toolbar itself handles this, which is nice
        }

        public float getLevel()
        {
            return ((AdjustmentDrawable)wrapped.Drawable).Level;
        }

        public void setLevel(float value)
        {
            ((AdjustmentDrawable)wrapped.Drawable).Level = value;
        }
    }

    internal class StockButton : IToolbarButton
    {
        private ApplicationLauncherButton wrapped;
        private AdjustmentDrawable levelUI;
        private Vector2 levelUIPosition = new Vector2();

        internal StockButton(ApplicationLauncherButton wrapped)
        {
            this.wrapped = wrapped;
        }

        public void Destroy()
        {
            //ApplicationLauncher.Instance.DisableMutuallyExclusive(wrapped);
            ApplicationLauncher.Instance.RemoveModApplication(wrapped);
        }

        public bool IsLevelUIVisible()
        {
            return (levelUI != null);
        }

        public void ShowLevelUI(Action levelChangedCallback)
        {
            levelUI = new AdjustmentDrawable();
            levelUI.OnLevelChanged += levelChangedCallback;
        }

        public void HideLevelUI()
        {
            levelUI = null;
        }

        public void DrawLevelUI()
        {
            // stock applauncher does not handle this, which is a pity
            if (levelUI != null)
            {
                levelUI.Draw(levelUIPosition, true);
                levelUIPosition = levelUI.GetPosition();
            }
        }

        public float getLevel()
        {
            return levelUI.Level;
        }

        public void setLevel(float value)
        {
            levelUI.Level = value;
        }
    }


	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	internal class AmbientLightAdjustment : MonoBehaviour {
		internal static int VERSION = 2;

		private static readonly string SETTINGS_FILE = KSPUtil.ApplicationRootPath + "GameData/AmbientLightAdjustment/settings.dat";
		private const int AUTO_HIDE_DELAY = 5;

		private IToolbarButton button;
		private AmbienceSetting setting;
		private AmbienceSetting secondSetting;
		private Color defaultAmbience;
		private bool listenToSliderChange = true;

		public void Start() {
			if (isRelevantScene()) {
                if (ToolbarManager.ToolbarAvailable)
                {
                    button = setupBlizzyToolbarButton();
                }
                else
                {
                    button = setupStockToolbarButton();
                }

				loadSettings();
			}
		}

        #region gui

        public void OnGUI()
        {
            if (button != null && button.IsLevelUIVisible())
            {
                button.DrawLevelUI();
            }
        }

        #endregion

        private IToolbarButton setupBlizzyToolbarButton()
        {
            IButton button = ToolbarManager.Instance.add("AmbientLightAdjustment", "adjustLevels");
            button.TexturePath = "AmbientLightAdjustment/contrast";
            button.ToolTip = "Ambient Light Adjustment";
            button.Visibility = new GameScenesVisibility(GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER);
            button.OnClick += (e) =>
            {
                switch (e.MouseButton)
                {
                    case 1:
                        resetToDefaultAmbience();
                        break;
                    case 2:
                        switchToSecondSetting();
                        break;
                    default:
                        toggleAdjustmentUI();
                        break;
                }
            };

            return new BlizzyButton(button);
        }

        private IToolbarButton setupStockToolbarButton()
        {
            ApplicationLauncher.AppScenes scenes = ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.TRACKSTATION | ApplicationLauncher.AppScenes.SPACECENTER;

            Texture toolbarButtonTexture = (Texture)GameDatabase.Instance.GetTexture("AmbientLightAdjustment/contrast", false);
            ApplicationLauncherButton button  = ApplicationLauncher.Instance.AddModApplication(toggleAdjustmentUI,          // Callback onTrue, 
                                                                                               toggleAdjustmentUI,          // Callback onFalse, 
                                                                                               null,                        // Callback onHover, 
                                                                                               null,                        // Callback onHoverOut, 
                                                                                               null,                        // Callback onEnable, 
                                                                                               null,                        // Callback onDisable, 
                                                                                               scenes,                      // AppScenes visibleInScenes, 
                                                                                               toolbarButtonTexture);       // Texture texture
            
            return new StockButton(button);
        }
        

        public void Destroy() {
			if (button != null) {
				button.Destroy();
				button = null;
			}
		}

		private void loadSettings() {
			ConfigNode settings = ConfigNode.Load(SETTINGS_FILE) ?? new ConfigNode();
			if (settings.HasNode("ambience")) {
				ConfigNode ambienceNode = settings.GetNode("ambience");
				ConfigNode[] settingNodes = ambienceNode.GetNodes("setting");
				if (settingNodes.Length >= 1) {
					setting = AmbienceSetting.create(settingNodes[0]);
				}
				if (settingNodes.Length >= 2) {
					secondSetting = AmbienceSetting.create(settingNodes[1]);
				}
			}
		}

		private void saveSettings() {
			ConfigNode root = new ConfigNode();
			ConfigNode ambienceNode = root.AddNode("ambience");
			setting.save(ambienceNode.AddNode("setting"));
			secondSetting.save(ambienceNode.AddNode("setting"));
			root.Save(SETTINGS_FILE);
		}

		private bool isRelevantScene() {
			return HighLogic.LoadedSceneIsFlight || 
                   HighLogic.LoadedScene == GameScenes.TRACKSTATION ||
                   HighLogic.LoadedScene == GameScenes.SPACECENTER;
		}

		private void toggleAdjustmentUI() {
			if (!button.IsLevelUIVisible()) {
				showAdjustmentUI();
			} else {
				hideAdjustmentUI();
			}
		}

		private void showAdjustmentUI() {
            Action callback = () => {
                if (listenToSliderChange)
                {
                    setting.Level = button.getLevel();
                    setting.UseDefaultAmbience = false;
                    saveSettings();
                    startAutoHide();
                }
            };

            button.ShowLevelUI(callback);

            updateSliderFromSetting();

            startAutoHide();
		}

		private void hideAdjustmentUI() {
			stopAutoHide();
			button.HideLevelUI();
		}

		private void startAutoHide() {
			stopAutoHide();
			StartCoroutine("doAutoHide");
		}

		private void stopAutoHide() {
			StopCoroutine("doAutoHide");
		}

		private IEnumerator doAutoHide() {
			yield return new WaitForSeconds(AUTO_HIDE_DELAY);
			hideAdjustmentUI();
		}

		private void resetToDefaultAmbience() {
			setting.Level = defaultAmbience.grayscale;
			setting.UseDefaultAmbience = true;
			updateSliderFromSetting();
			saveSettings();
		}

		private void switchToSecondSetting() {
			AmbienceSetting temp = setting;
			setting = secondSetting;
			secondSetting = temp;
			updateSliderFromSetting();
			saveSettings();
		}

		private void updateSliderFromSetting() {
			if (button.IsLevelUIVisible()) {
				listenToSliderChange = false;
				button.setLevel(setting.Level);
				listenToSliderChange = true;
			}
		}

		public void LateUpdate() {
			if (isRelevantScene()) {
				defaultAmbience = RenderSettings.ambientLight;

				if (setting == null) {
					setting = new AmbienceSetting() {
						UseDefaultAmbience = true,
						Level = defaultAmbience.grayscale
					};
				}
				if (secondSetting == null) {
					secondSetting = new AmbienceSetting() {
						UseDefaultAmbience = true,
						Level = defaultAmbience.grayscale
					};
				}

				if (!setting.UseDefaultAmbience) {
					Color ambience = defaultAmbience;
					ambience.r = setting.Level;
					ambience.g = setting.Level;
					ambience.b = setting.Level;
					RenderSettings.ambientLight = ambience;
				}
			}
		}
	}
}
