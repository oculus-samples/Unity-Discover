using UnityEditor;
using UnityEngine;

namespace Oculus.Avatar2
{
    [CustomPropertyDrawer(typeof(OvrAvatarMaterialExtensionConfig))]
    public class OvrAvatarMaterialExtensionConfigDrawer : PropertyDrawer
    {
        private const float PIXEL_HEIGHT_BETWEEN_FIELDS = 2.0f;

        private const string ADD_BUTTON_TEXT = "+";
        private const float ADD_BUTTON_PIXEL_WIDTH = 30.0f;

        private const string REMOVE_BUTTON_TEXT = "-";
        private const float REMOVE_BUTTON_PIXEL_WIDTH = 30.0f;

        private const string DEFAULT_EXTENSION_NAME = "Material Extension";
        private const string DEFAULT_ENTRY_NAME = "Extension Entry";
        private const string DEFAULT_REPLACEMENT_NAME = "Replacement Name";
        private const string ENTRIES_LABEL_TEXT = "Entries:";

        private static readonly float ENTRY_NAME_WIDTH = EditorGUIUtility.labelWidth;

        private static readonly GUIContent ADD_EXTENSION_BUTTON_TOOLTIP = new GUIContent(ADD_BUTTON_TEXT, "Add a new extension");
        private static readonly GUIContent ADD_ENTRY_BUTTON_TOOLTIP = new GUIContent(ADD_BUTTON_TEXT, "Add a new entry mapping for the extension");
        private static readonly GUIContent REMOVE_ENTRY_BUTTON_TOOLTIP = new GUIContent(REMOVE_BUTTON_TEXT, "Removes the last entry mapping for the extension");

        private static readonly GUIContent EXTENSION_ENTRY_FOLDOUT_CONTENT =
            new GUIContent("Extension Name:", "Edit the extension name here.");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float totalHeight = EditorGUIUtility.singleLineHeight; // Always at least one single line for the property label

            if(property.isExpanded)
            {
                // Add height needed for the "add new extension" button and the padding between then
                totalHeight += EditorGUIUtility.singleLineHeight + PIXEL_HEIGHT_BETWEEN_FIELDS;

                // Add in height for each extension
                var extNamesProp = property.FindPropertyRelative(OvrAvatarMaterialExtensionConfig.ExtensionNamesPropertyName);
                var numEntriesPerExtension =
                    property.FindPropertyRelative(OvrAvatarMaterialExtensionConfig.EntryNamesPropertyName);

                Debug.Assert(extNamesProp != null);
                Debug.Assert(numEntriesPerExtension != null);
                Debug.Assert(extNamesProp.arraySize == numEntriesPerExtension.arraySize);

                var numExtensions = extNamesProp.arraySize;
                for (int i = 0; i < numExtensions; i++)
                {
                    // Add in padding between the property label and the extension
                    totalHeight += GetSingleExtensionPixelHeight(extNamesProp, numEntriesPerExtension, i) + PIXEL_HEIGHT_BETWEEN_FIELDS;
                }
            }

            return totalHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            label = EditorGUI.BeginProperty(position, label, property);

            var foldoutRect = new Rect(position)
            {
                height = EditorGUIUtility.singleLineHeight,
            };

            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label);

            if (property.isExpanded)
            {
                DrawExpanded(foldoutRect, property, label);
            }

            EditorGUI.EndProperty();
        }

        private void DrawExpanded(in Rect position, SerializedProperty property, GUIContent label)
        {
            var extensionNamesProp =
                property.FindPropertyRelative(OvrAvatarMaterialExtensionConfig.ExtensionNamesPropertyName);
            var entryNamesProp = property.FindPropertyRelative(OvrAvatarMaterialExtensionConfig.EntryNamesPropertyName);
            var replacementNamesProp =
                property.FindPropertyRelative(OvrAvatarMaterialExtensionConfig.ReplacementNamesPropertyName);

            Debug.Assert(extensionNamesProp != null);
            Debug.Assert(entryNamesProp != null);
            Debug.Assert(replacementNamesProp != null);

            Debug.Assert(extensionNamesProp.arraySize == entryNamesProp.arraySize);
            Debug.Assert(entryNamesProp.arraySize == replacementNamesProp.arraySize);

            int lvl = EditorGUI.indentLevel;
            EditorGUI.indentLevel = lvl + 1;

            Rect r = position;
            for (int i = 0; i < extensionNamesProp.arraySize; i++)
            {
                // Draw the extension name at one indent level in, it has its own foldout
                r = DrawSingleExtension(
                    r,
                    extensionNamesProp,
                    entryNamesProp,
                    replacementNamesProp,
                    i);
            }

            EditorGUI.indentLevel = lvl;

            // Draw a + and - button for adding new extension
            DrawAddExtensionButton(r, extensionNamesProp, entryNamesProp, replacementNamesProp);
        }

        private static Rect DrawAddExtensionButton(
            in Rect position,
            SerializedProperty extensionNamesProp,
            SerializedProperty entryNamesProp,
            SerializedProperty replacementNamesProp)
        {
            var r = GetNextRect(position);
            var pRect = new Rect(r.xMin, r.yMin, ADD_BUTTON_PIXEL_WIDTH, EditorGUIUtility.singleLineHeight);

            if(GUI.Button(pRect, ADD_EXTENSION_BUTTON_TOOLTIP))
            {
                AddNewExtension(extensionNamesProp, entryNamesProp, replacementNamesProp);
            }

            return r;
        }

        private static Rect DrawAddAndRemoveEntryButtons(
            in Rect position,
            SerializedProperty extensionNamesProp,
            SerializedProperty entryNamesPerExtensionProp,
            SerializedProperty replacementNamesPerExtensionProp,
            SerializedProperty entryNamesForThisExtension,
            SerializedProperty replacementNamesForThisExtension,
            int extensionIndex)
        {
            // Calculate placement rectangles (right aligned)
            var r = GetNextRect(position);
            var pRect = new Rect(r.xMax - ADD_BUTTON_PIXEL_WIDTH - REMOVE_BUTTON_PIXEL_WIDTH, r.yMin, ADD_BUTTON_PIXEL_WIDTH, EditorGUIUtility.singleLineHeight);
            var mRect = new Rect(r.xMax - REMOVE_BUTTON_PIXEL_WIDTH, r.yMin, REMOVE_BUTTON_PIXEL_WIDTH, EditorGUIUtility.singleLineHeight);

            if(GUI.Button(pRect, ADD_ENTRY_BUTTON_TOOLTIP))
            {
                // If clicked, add new entry
                AddNewEntryForExtension(entryNamesForThisExtension, replacementNamesForThisExtension);
            }

            if (GUI.Button(mRect, REMOVE_ENTRY_BUTTON_TOOLTIP))
            {
                RemoveLastEntryForExtension(entryNamesForThisExtension, replacementNamesForThisExtension);

                if (entryNamesForThisExtension.arraySize == 0)
                {
                    // Empty extension, remove it
                    RemoveExtension(extensionNamesProp, entryNamesPerExtensionProp, replacementNamesPerExtensionProp, extensionIndex);
                }
            }

            return r;
        }

        // Returns last used rect
        private static Rect DrawSingleExtension(
            in Rect position,
            SerializedProperty extensionNamesProp,
            SerializedProperty entryNamesPerExtensionProp,
            SerializedProperty replacementNamesPerExtensionProp,
            int extensionIndex)
        {
            // Draw the extension name at one indent level in, it has it's own foldout
            // Draw foldout to left of property label
            var r = GetNextRect(position);
            var prop = extensionNamesProp.GetArrayElementAtIndex(extensionIndex);
            DrawExtensionFoldout(r, prop);

            if (prop.isExpanded)
            {
                // Add all entries for this extension
                var keysProp = GetNestedSerializedArrayProperty(entryNamesPerExtensionProp, extensionIndex);
                var valuesProp = GetNestedSerializedArrayProperty(replacementNamesPerExtensionProp, extensionIndex);

                // Make a label that states that the following are entries
                r = GetNextRect(r);
                EditorGUI.LabelField(r,  ENTRIES_LABEL_TEXT);

                EditorGUI.indentLevel++;

                for (int i = 0; i < keysProp.arraySize; i++)
                {
                    r = GetNextRect(r);
                    var valueWidth = r.width - ENTRY_NAME_WIDTH;
                    var keyRect = new Rect(r.xMin, r.yMin, ENTRY_NAME_WIDTH, r.height);
                    var valueRect = new Rect(keyRect.xMax, r.yMin, valueWidth, r.height);

                    var keyProp = keysProp.GetArrayElementAtIndex(i);
                    var valueProp = valuesProp.GetArrayElementAtIndex(i);

                    EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none, false);
                    EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none, false);
                }

                EditorGUI.indentLevel--;

                // Draw +- buttons for adding and removing entries
                r = DrawAddAndRemoveEntryButtons(
                    r,
                    extensionNamesProp,
                    entryNamesPerExtensionProp,
                    replacementNamesPerExtensionProp,
                    keysProp,
                    valuesProp,
                    extensionIndex);
            }

            return r;
        }

        private static float GetSingleExtensionPixelHeight(
            SerializedProperty extensionNamesProp,
            SerializedProperty entriesProp,
            int extensionIndex)
        {
            // Add in height for each extension

            // Extension has single line at least extension name label
            float extensionHeight = EditorGUIUtility.singleLineHeight;

            var prop = extensionNamesProp.GetArrayElementAtIndex(extensionIndex);
            if (prop.isExpanded)
            {
                // Add a line (and vertical padding) for the "Entries" label
                // Add a line (and vertical padding) for the +- buttons
                extensionHeight += 2.0f * (EditorGUIUtility.singleLineHeight + PIXEL_HEIGHT_BETWEEN_FIELDS);

                // Pull num entries out
                prop = GetNestedSerializedArrayProperty(entriesProp, extensionIndex);
                var numEntries = prop.arraySize;
                if (numEntries > 0)
                {
                    extensionHeight += (EditorGUIUtility.singleLineHeight * numEntries) +
                           (numEntries * PIXEL_HEIGHT_BETWEEN_FIELDS);
                }
            }

            return extensionHeight;
        }

        private static Rect GetNextRect(in Rect position)
        {
            var heightBetweenRects = EditorGUIUtility.singleLineHeight + PIXEL_HEIGHT_BETWEEN_FIELDS;
            var heightOfRect = EditorGUIUtility.singleLineHeight;
            return new Rect(position.xMin, position.yMin + heightBetweenRects, position.width, heightOfRect);
        }

        private static void AddNewExtension(
            SerializedProperty extNameProp,
            SerializedProperty entryNamesProp,
            SerializedProperty replacementNamesProp)
        {
            // Insert empty string as new extension name
            var extensionIndex = extNameProp.arraySize;
            extNameProp.InsertArrayElementAtIndex(extensionIndex);

            var prop = extNameProp.GetArrayElementAtIndex(extensionIndex);
            prop.stringValue = DEFAULT_EXTENSION_NAME;

            // TODO*: Check for key uniqueness

            // Insert new list/array of entries
            entryNamesProp.InsertArrayElementAtIndex(extensionIndex); // new list of entries
            prop = GetNestedSerializedArrayProperty(entryNamesProp, extensionIndex);
            prop.arraySize = 0; // Unity docs say Insert inserts an undefined value, so, explicitly set here

            var entryIndex = 0; // Should be new list of entries, so can assume index 0
            prop.InsertArrayElementAtIndex(entryIndex);
            prop = prop.GetArrayElementAtIndex(entryIndex);
            prop.stringValue = DEFAULT_ENTRY_NAME;

            // Insert a new list/array of replacement names
            replacementNamesProp.InsertArrayElementAtIndex(extensionIndex);
            prop = GetNestedSerializedArrayProperty(replacementNamesProp, extensionIndex);
            prop.arraySize = 0; // Unity docs say Insert inserts an undefined value, so, explicitly set here

            // Add new replacement name
            prop.InsertArrayElementAtIndex(entryIndex);
            prop = prop.GetArrayElementAtIndex(entryIndex);
            prop.stringValue = DEFAULT_REPLACEMENT_NAME;
        }

        private static void AddNewEntryForExtension(
            SerializedProperty entryNamesForThisExtension,
            SerializedProperty replacementNamesProp)
        {
            int entryIndex = entryNamesForThisExtension.arraySize;

            // Insert a new default entry
            entryNamesForThisExtension.InsertArrayElementAtIndex(entryIndex);
            var prop = entryNamesForThisExtension.GetArrayElementAtIndex(entryIndex);
            prop.stringValue = DEFAULT_ENTRY_NAME;

            // TODO* Enforce name uniqueness?

            // Insert a new default mapping (empty string)
            replacementNamesProp.InsertArrayElementAtIndex(entryIndex);
            prop = replacementNamesProp.GetArrayElementAtIndex(entryIndex);
            prop.stringValue = DEFAULT_REPLACEMENT_NAME;
        }

        private static void RemoveLastEntryForExtension(
            SerializedProperty entryNamesForThisExtension,
            SerializedProperty replacementNamesForThisExtension)
        {
            // Seemingly have to set data to be null first, then delete....
            // Unity docs don't say this but forums do
            int lastIndex = entryNamesForThisExtension.arraySize - 1;
            if (lastIndex >= 0)
            {
                entryNamesForThisExtension.DeleteArrayElementAtIndex(lastIndex);
                replacementNamesForThisExtension.DeleteArrayElementAtIndex(lastIndex);
            }
        }

        private static void RemoveExtension(
            SerializedProperty extensionNames,
            SerializedProperty entryNameLists,
            SerializedProperty replacementNameLists,
            int extensionIndex)
        {
            extensionNames.DeleteArrayElementAtIndex(extensionIndex);
            entryNameLists.DeleteArrayElementAtIndex(extensionIndex);
            replacementNameLists.DeleteArrayElementAtIndex(extensionIndex);
        }

        private static SerializedProperty GetNestedSerializedArrayProperty(SerializedProperty property, int index)
        {
            var prop = property.GetArrayElementAtIndex(index);
            return prop.FindPropertyRelative(OvrAvatarMaterialExtensionConfig.InnerListProperyName);
        }

        private static void DrawExtensionFoldout(in Rect position, SerializedProperty prop)
        {
            EditorGUI.PropertyField(position, prop, EXTENSION_ENTRY_FOLDOUT_CONTENT, false);
            prop.isExpanded = EditorGUI.Foldout(position, prop.isExpanded, GUIContent.none, false);
        }
    }
}
