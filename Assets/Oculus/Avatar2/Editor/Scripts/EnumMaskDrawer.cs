using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Oculus.Avatar2
{
#if !UNITY_2019_3_OR_NEWER
    [CustomPropertyDrawer(typeof(EnumMaskAttribute))]
    public class EnumMaskDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            label = EditorGUI.BeginProperty(position, label, property);

            Enum oldValue = (Enum)Convert.ChangeType(GetValue(property), typeof(Enum));
            Enum newValue = EditorGUI.EnumFlagsField(position, label, oldValue);
            
            if (!oldValue.Equals(newValue))
            {
                SetValue(property, newValue);
                EditorUtility.SetDirty(property.serializedObject.targetObject);
            }

            EditorGUI.EndProperty();
        }
        
        // It's 2020 and Unity still doesn't have a way to get the value as a generic object from a SerializedProperty
        // So we'll do it ourselves
        private object GetValue(SerializedProperty property)
        {
            object valueAsObject = property.serializedObject.targetObject;
            FieldInfo field = null;
            foreach(var path in property.propertyPath.Split('.'))
            {
                var type = valueAsObject.GetType();
                field = type.GetField(path, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                valueAsObject = field.GetValue(valueAsObject);
            }
            return valueAsObject;
        }
        
        private void SetValue(SerializedProperty property, object val)
        {
            object obj = property.serializedObject.targetObject;
            List<KeyValuePair<FieldInfo, object>> list = new List<KeyValuePair<FieldInfo, object>>();
 
            FieldInfo field = null;
            foreach(var path in property.propertyPath.Split('.'))
            {
                var type = obj.GetType();
                field = type.GetField(path, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                list.Add(new KeyValuePair<FieldInfo, object>(field, obj));
                obj = field.GetValue(obj);
            }
 
            // Now set values of all objects, from child to parent
            for(int i = list.Count - 1; i >= 0; --i)
            {
                list[i].Key.SetValue(list[i].Value, val);
                // New 'val' object will be parent of current 'val' object
                val = list[i].Value;
            }
        }
    }
#endif
}
