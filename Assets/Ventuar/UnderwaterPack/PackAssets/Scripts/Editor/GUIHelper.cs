using UnityEngine;
using UnityEditor;

namespace LowPolyUnderwaterPack
{
	/// <summary>
	/// Low Poly Underwater Pack class that provides helper functions to custom editors and inspectors.
	/// </summary>
	public static class GUIHelper
	{
#if UNITY_EDITOR
		private static Color foldoutTintColor = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.05f) : new Color(0f, 0f, 0f, 0.05f);

		#region Custom Inspector Displays

		/// <summary>
		/// Creates a foldout menu for fields in a custom inspector.
		/// </summary>
		/// <param name="foldout">The bool that returns true or false when the foldout menu is open or closed respectively.</param>
		/// <param name="content">The text that appears on the header of the foldout menu.</param>
		/// <returns>The state of the foldout menu, true for opened and false for closed.</returns>
		public static bool Foldout(bool foldout, string content)
		{
			GUIStyle HeaderLabel = new GUIStyle(GUI.skin.label)
			{
				fontSize = 13,
				fontStyle = FontStyle.Bold
			};

			EditorGUI.indentLevel--;
			
			Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight * 1.5f, EditorStyles.foldout);

			//if (rect.Contains(Event.current.mousePosition))
				EditorGUI.DrawRect(EditorGUI.IndentedRect(rect), foldoutTintColor);

			EditorGUI.indentLevel++;

			Rect foldoutRect = rect;
			foldoutRect.width = EditorGUIUtility.singleLineHeight;
			foldout = EditorGUI.Foldout(rect, foldout, "", true);

			EditorGUI.LabelField(rect, content, HeaderLabel);

			return foldout;
		}

		/// <summary>
		/// Displays a custom texture field in a custom inspector similar to the default one for shaders.
		/// </summary>
		/// <param name="text">The GUIContent of the texture field.</param>
		/// <param name="tex">The texture field that will be used for this display.</param>
		/// <param name="tiling">The tiling value of the texture.</param>
		/// <param name="offset">The offset value of the texture.</param>
		public static void TextureDisplay(GUIContent text, ref Texture2D tex, ref Vector2 tiling, ref Vector2 offset)
		{
			tex = (Texture2D)EditorGUILayout.ObjectField(text, tex, typeof(Texture2D), false);
			EditorGUILayout.Space(-EditorGUIUtility.singleLineHeight * 2);

			EditorGUI.indentLevel++;

			tiling = EditorGUILayout.Vector2Field("Tiling", tiling);
			offset = EditorGUILayout.Vector2Field("Offset", offset);

			EditorGUI.indentLevel--;
		}

		/// <summary>
		/// Displays two float fields horizontally next to each other in a custom inspector.
		/// </summary>
		/// <param name="f1">The first float field that will be placed on the left.</param>
		/// <param name="text1">The GUIContent of the first float field.</param>
		/// <param name="f2">The second float field that will be placed on the right.</param>
		/// <param name="text2">The GUIContent of the second float field.</param>
		public static void DisplayDualHorizontalFields(ref float f1, GUIContent text1, ref float f2, GUIContent text2)
		{
			Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
			Rect r1 = rect;
			r1.width /= 2;

			Rect r2 = rect;
			r2.width /= 2;
			r2.x += (EditorGUIUtility.currentViewWidth - r2.x) / 2 - 10;

			GUILayout.BeginHorizontal();

			f1 = EditorGUI.FloatField(r1, text1, f1);
			f2 = EditorGUI.FloatField(r2, text2, f2);

			GUILayout.EndHorizontal();
		}

        #endregion
#endif
	}
}
