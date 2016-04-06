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
        void Save(ConfigNode node);
        void Load(ConfigNode node);

        void Destroy();

        bool IsLevelUIVisible();
        void ShowLevelUI(Action levelChangedCallback, Action toggleSettingCallback, Action resetSettingsCallback);
        void HideLevelUI();
        bool OnGUI();

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

        public void Save(ConfigNode node)
        {
        }

        public void Load(ConfigNode node)
        {

        }

        public void Destroy()
        {
            wrapped.Destroy();
        }

        public bool IsLevelUIVisible()
        {
            return (wrapped.Drawable != null);
        }

        public void ShowLevelUI(Action levelChangedCallback, Action toggleSettingCallback, Action resetSettingsCallback)
        {
            AdjustmentDrawable adjustment = new AdjustmentDrawable(toggleSettingCallback, resetSettingsCallback);
            adjustment.OnLevelChanged += levelChangedCallback;

            wrapped.Drawable = adjustment;
        }

        public void HideLevelUI()
        {
            wrapped.Drawable = null;
        }

        public bool OnGUI()
        {
            // blizzy's toolbar itself handles this, which is nice
            return false;
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
        private Callback toggleAdjustmentUI;
        private bool addedToAppLauncher = false;
        private ApplicationLauncherButton wrapped;
        private AdjustmentDrawable levelUI;
        private Vector2 levelUIPosition = new Vector2(200f, 10f);

        internal StockButton(Callback toggleAdjustmentUI)
        {
            this.toggleAdjustmentUI = toggleAdjustmentUI;
        }

        public void Save(ConfigNode node)
        {
            //Log.warn("Saving position to config node[" + node + "] position[" + levelUIPosition + "]");
            node.overwrite("x", levelUIPosition.x.ToString());
            node.overwrite("y", levelUIPosition.y.ToString());
        }

        public void Load(ConfigNode node)
        {
            //Log.warn("Loading position from config node [" + node + "]");

            if (!node.HasValue("x"))
            {
                //Log.warn("Node has no 'x' attribute");
            }
            else
            { 
                string xString = node.GetValue("x");
                levelUIPosition.x = float.Parse(xString);
            }

            if (!node.HasValue("y"))
            {
                //Log.warn("Node has no 'y' attribute");
            }
            else
            {
                string yString = node.GetValue("y");
                levelUIPosition.y = float.Parse(yString);
            }

            //Log.warn("Position loaded x[" + levelUIPosition.x + "] y[" + levelUIPosition.y + "]");
        }

        public void Destroy()
        {
            //ApplicationLauncher.Instance.DisableMutuallyExclusive(wrapped);
            if (wrapped != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(wrapped);
                wrapped = null;
                addedToAppLauncher = false;
            }

            levelUI = null;
        }

        public bool IsLevelUIVisible()
        {
            return (levelUI != null);
        }

        public void ShowLevelUI(Action levelChangedCallback, Action toggleSettingCallback, Action resetSettingsCallback)
        {
            levelUI = new AdjustmentDrawable(toggleSettingCallback, resetSettingsCallback);
            levelUI.OnLevelChanged += levelChangedCallback;
        }

        public void HideLevelUI()
        {
            levelUI = null;
        }

        public bool OnGUI()
        {
            if (!addedToAppLauncher)
            {
                if (ApplicationLauncher.Ready)
                {
                    ApplicationLauncher.AppScenes scenes = ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.TRACKSTATION | ApplicationLauncher.AppScenes.SPACECENTER;

                    Texture toolbarButtonTexture = (Texture)GameDatabase.Instance.GetTexture("AmbientLightAdjustment/contrast", false);
                    wrapped = ApplicationLauncher.Instance.AddModApplication(toggleAdjustmentUI,          // Callback onTrue, 
                                                                             toggleAdjustmentUI,          // Callback onFalse, 
                                                                             null,                        // Callback onHover, 
                                                                             null,                        // Callback onHoverOut, 
                                                                             null,                        // Callback onEnable, 
                                                                             null,                        // Callback onDisable, 
                                                                             scenes,                      // AppScenes visibleInScenes, 
                                                                             toolbarButtonTexture);       // Texture texture
                    addedToAppLauncher = true;
                }
            }

            // stock applauncher does not handle this, which is a pity
            if (levelUI != null)
            {
                levelUI.Draw(levelUIPosition, true);

                Vector2 newPosition = levelUI.GetPosition();
                if (newPosition == levelUIPosition)
                {
                    return false;
                }

                levelUIPosition = levelUI.GetPosition();

                return true;
            }

            return false;
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
        private bool defaultAmbienceRetrieved = false;
		private bool listenToSliderChange = true;

		public void Start() {
			if (button == null) {
                //Log.warn("Awake - button == null");
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
            else
            {
                //Log.warn("Awake - button != null");
            }
		}
        

        #region gui

        public void OnGUI()
        {
            if (button != null)
            {
                if (button.OnGUI())
                {
                    saveSettings();
                }
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
            //Log.warn("setting up stock toolbar button");
            
            return new StockButton(toggleAdjustmentUI);
        }
        

        public void OnDestroy() {
            //Log.warn("Destroy called");
			if (button != null) {
				button.Destroy();
				button = null;
			}
            stopAutoHide();
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
                if (button == null)
                {
                    //Log.warn("Unable to load position settings as button is null");
                }
                else
                {
                    if (!settings.HasNode("position"))
                    {
                        //Log.warn("Button is not null, but settings node[" + settings + "] has no position node");
                    }
                    else
                    {
                        ConfigNode positionNode = settings.GetNode("position");

                        //Log.warn("Button is not null and settings has position node[" + positionNode + "]");
                        button.Load(positionNode);
                    }
                }
			}
		}

		private void saveSettings() {
			ConfigNode root = new ConfigNode();
			ConfigNode ambienceNode = root.AddNode("ambience");
			setting.save(ambienceNode.AddNode("setting"));
			secondSetting.save(ambienceNode.AddNode("setting"));
            if (button == null)
            {
                //Log.warn("Button is null, unable to save position");
            }
            else
            {
                //Log.warn("Button is not null, saving position");
                button.Save(root.AddNode("position"));
            }
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
            Action levelChangedCallback = () => {
                if (listenToSliderChange)
                {
                    float newLevel = button.getLevel();
                    if (newLevel != setting.Level)
                    {
                        setting.Level = button.getLevel();
                        setting.UseDefaultAmbience = false; //Log.warn("Not using default ambience");
                        saveSettings();
                        startAutoHide("level changed");
                    }
                }
            };

            Action toggleSettingCallback = () =>
            {
                switchToSecondSetting();
            };

            Action resetSettingsCallback = () =>
            {
                resetToDefaultAmbience();
            };

            button.ShowLevelUI(levelChangedCallback, toggleSettingCallback, resetSettingsCallback);

            updateSliderFromSetting();

            startAutoHide("showAdjustmentUI");
		}

		private void hideAdjustmentUI() {
			stopAutoHide();
            if (button != null)
            {
                button.HideLevelUI();
            }
		}

		private void startAutoHide(string reason) {
            //Log.warn("startAutoHide - " + reason);

			stopAutoHide();
			StartCoroutine("doAutoHide");
		}

		private void stopAutoHide() {
            //Log.warn("stopAutoHide");

            StopCoroutine("doAutoHide");
		}

		private IEnumerator doAutoHide() {
			yield return new WaitForSeconds(AUTO_HIDE_DELAY);
			hideAdjustmentUI();
		}

		private void resetToDefaultAmbience() {
            //Log.warn("resetToDefaultAmbience");

			setting.Level = defaultAmbience.grayscale;
			setting.UseDefaultAmbience = true; //Log.warn("Using default ambience");
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
                if (!defaultAmbienceRetrieved)
                {
                    defaultAmbience = RenderSettings.ambientLight;
                    defaultAmbienceRetrieved = true;
                }

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

                if (setting.UseDefaultAmbience)
                {
                    //Log.warn("Making use of default ambience");
                    RenderSettings.ambientLight = defaultAmbience;
                }
                else
                {
                    //Log.warn("Overriding default ambience");
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
