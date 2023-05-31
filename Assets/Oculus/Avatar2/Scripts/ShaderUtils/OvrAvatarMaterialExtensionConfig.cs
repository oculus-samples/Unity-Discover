using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oculus.Avatar2
{
    [Serializable]
    public class OvrAvatarMaterialExtensionConfig : ISerializationCallbackReceiver
    {
        private const string LOG_SCOPE = nameof(OvrAvatarMaterialExtensionConfig);

        private Dictionary<string, Dictionary<string, string>> _entryNameRemapping =
            new Dictionary<string, Dictionary<string, string>>();

        // Yes, it sucks to serialize out the keys and values as separate lists
        // just to read them into a dictionary, but Unity doesn't seem to handle serializing
        // dictionaries well *shrug*

        // Unity also can't serialize list of lists without wrapping because...Unity
        [Serializable]
        private class StringListWrapper
        {
            [SerializeField] public List<string> list = new List<string>(0);
        }

        [SerializeField] private List<string> _extensionNames = new List<string>(0);
        [SerializeField] private List<StringListWrapper> _entryNamesPerExtension = new List<StringListWrapper>(0);
        [SerializeField] private List<StringListWrapper> _replacementNamesPerExtension = new List<StringListWrapper>(0);

        public static string ExtensionNamesPropertyName => nameof(_extensionNames);
        public static string EntryNamesPropertyName => nameof(_entryNamesPerExtension);
        public static string ReplacementNamesPropertyName => nameof(_replacementNamesPerExtension);

        public static string InnerListProperyName = nameof(StringListWrapper.list);

        public bool TryGetNameInShader(string extensionName, string entryName, out string nameInShader)
        {
            if (_entryNameRemapping.TryGetValue(extensionName, out Dictionary<string, string> extensionEntries))
            {
                return extensionEntries.TryGetValue(entryName, out nameInShader);
            }

            nameInShader = string.Empty;
            return false;
        }

        public void OnBeforeSerialize()
        {
            _extensionNames.Clear();
            _entryNamesPerExtension.Clear();
            _replacementNamesPerExtension.Clear();

            foreach (var kvp in _entryNameRemapping)
            {
                _extensionNames.Add(kvp.Key);

                var entries = new StringListWrapper();
                var replacements = new StringListWrapper();

                foreach (var evp in _entryNameRemapping[kvp.Key])
                {
                    entries.list.Add(evp.Key);
                    replacements.list.Add(evp.Value);
                }

                _entryNamesPerExtension.Add(entries);
                _replacementNamesPerExtension.Add(replacements);
            }
        }

        public void OnAfterDeserialize()
        {
            _entryNameRemapping = new Dictionary<string, Dictionary<string, string>>();

            // There are some constraints in naming
            // 1. Extension names must be unique
            // 2. Combination of extension name + entry name must be unique
            // 3. Replacement names must be unique

            // NOTE* It is possible that these constraints may change in the future

            // TODO*: Have the property drawer for this validate input then
            // for better feedback to user

            HashSet<string> existingExtensionNames = new HashSet<string>();
            HashSet<Tuple<string, string>> existingCombos = new HashSet<Tuple<string, string>>();
            HashSet<string> existingReplacementNames = new HashSet<string>();

            Debug.Assert(_extensionNames.Count == _entryNamesPerExtension.Count);
            Debug.Assert(_entryNamesPerExtension.Count == _replacementNamesPerExtension.Count);

            for (int extensionIndex = 0; extensionIndex < _extensionNames.Count; extensionIndex++)
            {
                var extensionName = _extensionNames[extensionIndex];

                // See if extension name is a duplicate and find a new name if it is a dupe
                if (existingExtensionNames.Contains(extensionName))
                {
                    extensionName = FindNonDuplicateName(extensionName, existingExtensionNames);
                }

                var entryNames = _entryNamesPerExtension[extensionIndex].list;
                var replacementNames = _replacementNamesPerExtension[extensionIndex].list;
                Debug.Assert(entryNames.Count == replacementNames.Count);

                // Loop over all extensions and replacement names for that extension
                var extensionDict = new Dictionary<string, string>();
                for (int entryIndex = 0; entryIndex < entryNames.Count; entryIndex++)
                {
                    var entryName = entryNames[entryIndex];

                    // Check for validity of extension name + entry name combo and change
                    // entry name if a dupe
                    var combo = new Tuple<string, string>(extensionName, entryName);
                    if (existingCombos.Contains(combo))
                    {
                        entryName = FindNonDuplicateNameTuple(extensionName, entryName, existingCombos);
                    }

                    var replacementName = replacementNames[entryIndex];
                    if (existingReplacementNames.Contains(replacementName))
                    {
                        replacementName = FindNonDuplicateName(replacementName, existingReplacementNames);
                    }

                    // Add to validity bookkeeping
                    existingCombos.Add(combo);
                    existingReplacementNames.Add(replacementName);

                    // Add to dictionary
                    extensionDict.Add(entryName, replacementName);
                }

                existingExtensionNames.Add(extensionName);
                _entryNameRemapping[extensionName] = extensionDict;
            }
        }

        private static string FindNonDuplicateName(string original, HashSet<string> existingExtensionNames)
        {
            var count = 1;
            var newName = string.Concat(original, $"({count})");
            while (existingExtensionNames.Contains(newName) && count < int.MaxValue)
            {
                count++;
                newName = string.Concat(original, $"({count})");
            }

            return newName;
        }

        private static string FindNonDuplicateNameTuple(
            string firstString,
            string original,
            HashSet<Tuple<string, string>> existingCombos)
        {
            var count = 1;
            var newSecondString = string.Concat(original, $"({count})");
            while (existingCombos.Contains(new Tuple<string, string>(firstString, newSecondString)) &&
                   count < int.MaxValue)
            {
                count++;
                newSecondString = string.Concat(original, $"({count})");
            }

            return newSecondString;
        }
    }
}
