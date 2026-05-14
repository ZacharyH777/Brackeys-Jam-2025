#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Collections;
using RunstarSystems.IKSystem.Data;

namespace RunstarSystems.IKSystem.Editor
{
    // Binding
    [CustomPropertyDrawer(typeof(BoneStaticData))]
    [CustomPropertyDrawer(typeof(JointStaticData))]
    public class IkStaticDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty nameProp = property.FindPropertyRelative("Name");
            if (nameProp != null)
            {
                if (nameProp.boxedValue is FixedString64Bytes fixedString)
                {
                    string actualName = fixedString.ToString();
                    if (!string.IsNullOrEmpty(actualName))
                    {
                        label.text = actualName;
                    }
                }
            }

            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                
                SerializedProperty iterator = property.Copy();
                SerializedProperty endProperty = property.GetEndProperty();
                
                bool enterChildren = true;
                float currentY = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false; // Stay on the surface properties of this struct
                    if (SerializedProperty.EqualContents(iterator, endProperty)) break;

                    float childHeight = EditorGUI.GetPropertyHeight(iterator, true);
                    Rect childRect = new Rect(position.x, currentY, position.width, childHeight);
                    
                    EditorGUI.PropertyField(childRect, iterator, true);
                    currentY += childHeight + EditorGUIUtility.standardVerticalSpacing;
                }
                
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;

            // If the foldout is open, we need to calculate the height of all children
            if (property.isExpanded)
            {
                SerializedProperty iterator = property.Copy();
                SerializedProperty endProperty = property.GetEndProperty();
                
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (SerializedProperty.EqualContents(iterator, endProperty)) break;

                    height += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            return height;
        }
    }
}
#endif
