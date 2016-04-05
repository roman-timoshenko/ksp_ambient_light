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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AmbientLightAdjustment {
	internal class AdjustmentDrawable : IDrawable {
		internal event Action OnLevelChanged;

		internal float Level {
			get {
				return level_;
			}
			set {
				if (value != level_) {
					level_ = value;
					fireLevelChanged();
				}
			}
		}
		private float level_;

		private int id = new System.Random().Next(int.MaxValue);
		private Rect rect = new Rect(0, 0, 0, 0);

		public void Update() {
			// nothing to do
		}

        public Vector2 Draw(Vector2 position, bool allowDrag)
        {
            rect.x = position.x;
            rect.y = position.y;

            return Draw(allowDrag);
        }

        public Vector2 GetPosition()
        {
            return new Vector2(rect.x, rect.y);
        }

        public Vector2 Draw(Vector2 position) {
            return Draw(position, false);
        }

        private Vector2 Draw(bool allowDrag)
        {
            rect = GUILayout.Window(id, rect, (windowId) => drawContents(allowDrag), (string) null, GUI.skin.box);
			return new Vector2(rect.width, rect.height);
		}

		private void drawContents(bool allowDrag) {
            if (allowDrag)
            {
                GUILayout.BeginHorizontal();
            }
            Level = GUILayout.HorizontalSlider(Level, 0f, 1f, GUILayout.Width(200));            

            if (allowDrag)
            {
                GUILayout.Space(10);
                GUILayout.EndHorizontal();
                GUI.DragWindow();
            }
        }

		private void fireLevelChanged() {
			if (OnLevelChanged != null) {
				OnLevelChanged();
			}
		}
	}
}
