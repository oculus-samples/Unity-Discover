using System;
using System.Collections.Generic;
using UnityEngine;

/*
 * @file OvrAvatarMaterial.cs
 */

namespace Oculus.Avatar2
{
    /**
     * @class OvrAvatarMaterial
     * Collects shader and material properties to apply to an avatar.
     *
     * OvrAvatarMaterial is like the Unity MaterialPropertyBlock.
     * It has getters and setters for the various types of material properties.
     * Unlike MaterialPropertyBlock, this class remembers and can serialize
     * the names and values of the properties that have been changed.
     *
     * Each OvrAvatarEntity can have an instance of this class to maintain
     * the current shader, its keywords and material properties.
     * You can access it using OvrAvatarEntity.material and then
     * set individual properties. OvrAvatarEntity.ApplyMaterial()
     * will apply the new shader / material to all the renderables
     * in the avatar.
     *
     * @code
     *  OvrAvatarEntity avatar1;
     *  avatar1.Material.SetColor("_EmissionColor", Color.blue);
     *  avatar1.Material.SetKeyword("_EMISSION", true);
     *  avatar1.ApplyMaterial();
     * @endcode
     *
     * This material state is also applied to new renderables that
     * are added to the avatar entity.
     *
     * You can also set the material properties based on the ID
     * provided by Shader.PropertyToId(). Even if you don't provide
     * an ID, OvrAvatarMaterial maintains one internally so you can
     * add a property using the name but retrieve it from the ID.
     *
     * @code
     *  OvrAvatarEntity avatar2;
     *  int propID = Shader.PropertyToId("_EmissionColor");
     *  Shader shader2 = Shader.Find("Avatar/Standard");
     *  avatar2.Material.Shader = shader2;
     *  avatar2.Material.SetColor(propID, Color.blue);
     *  avatar2.ApplyMaterial();
     * @endcode
     *
     *  Avatars can share materials as well. If a material is provided
     *  before the avatar is loaded, the material will be applied to all the
     *  future renderables as well.
     *
     * @code
     *  OvrAvatarMaterial desat = new OvrAvatarMaterial();
     *  desat.SetKeyword("DESAT", true);
     *  desat.SetFloat("_DesatAmount", 0.7f);
     *  desat.SetColor("_DesatTint", new Color(0.13f, 0.2f, 0.4f);
     *  desat.SetFloat("_DesatLerp", 0.4f);
     *  avatar1.Material = desat;
     *  avatar2.Material = desat;
     *  avatar1.ApplyMaterial();
     *  avatar2.ApplyMaterial();
     * @endcode
     *
     * As an optimization, you can set and get material properties
     * based on their integer ID instead of a string name. This ID
     * is obtained by calling Shader.PropertyToId() with the  material property name.
     * If you first set the property using the name, OvrAvatarMaterial will
     * be able to retrieve it by name or by ID. If you first set the property
     * using the ID, you will not be able to retrieve it by name.
     *
     * @see OvrAvatarEntity.Material
     */
    public sealed class OvrAvatarMaterial
    {
        /*
         * type tokens for material properties
         */
        private enum PropertyType
        {
            NONE = 0,
            INTEGER = 1,
            FLOAT = 2,
            COLOR = 3,
            VEC4 = 4,
            MATRIX = 5,
            FLOAT_ARRAY = 6,
            VEC_ARRAY = 7,
            TEXTURE = 8
        };

        private static Dictionary<string, int> g_shaderIdLookup = default;

        private readonly Dictionary<int, ValueTuple<object, PropertyType>> properties_
            = new Dictionary<int, ValueTuple<object, PropertyType>>();

        private readonly Dictionary<string, bool> ShaderKeywords = new Dictionary<string, bool>();

        private MaterialPropertyBlock propertyBlock_;
        private Shader Shader = null;

        public OvrAvatarMaterial()
        {
            propertyBlock_ = new MaterialPropertyBlock();

            // Check for static initialization in instance ctor to avoid implicit static ctor
            if (g_shaderIdLookup == null)
            {
                g_shaderIdLookup = new Dictionary<string, int>();
            }
        }


        /**
         * Clears the contents of this instance, removing shader,
         * shader keywords and material properties.
         *
         * This does not change the appearance of avatars
         * using this instance until OvrAvatarEntity.ApplyMaterial
         * is explicitly called on each avatar using it.
         *
         * @see OvrAvatarEntity.ApplyMaterial()
         * @see OvrAvatarEntity.Material
         */
        public void Clear()
        {
            properties_.Clear();
            ShaderKeywords.Clear();

            propertyBlock_ = null;
            Shader = null;
        }

        public void SetShader(Shader shader) => Shader = shader;


        /**
         * Gets the value of the specified shader keyword.
         * @param keyword  name of the shader keyword to check.
         * @returns true if the keyword has been enabled by this material, else false.
         *          false if the keyword is disabled or has never been set.
         * @see HasKeyword(string)
         * @see SetKeyword(string, boolean)
         * @see RemoveKeyword(string)
         */
        public bool GetKeyword(string keyword)
        {
            return ShaderKeywords.TryGetValue(keyword, out bool val) && val;
        }

        /**
         * Indicates whether a specific shader keyword has been set in this material.
         * @param keyword  name of the shader keyword to check.
         * @returns true if the keyword has been enabled or disabled,
         *          false if it has never been set.
         * @see GetKeyword(string)
         * @see SetKeyword(string, boolean)
         * @see RemoveKeyword(string)
         */
        public bool HasKeyword(string keyword)
        {
            return ShaderKeywords.ContainsKey(keyword);
        }

        /**
         * Enables or disables a specific shader keyword.
         * @param keyword  name of the shader keyword to change.
         * @boolean val    true to enable the keyword, false to disable it.
         *
         * This change will not change the avatars which use
         * this material unless you call OvrAvatarEntity.ApplyMaterial()
         * for all avatars that share this instance. It will affect
         * future renderables added to these avatars.
         *
         * After this call, HasKeyword() will return true.
         *
         * @see GetKeyword(string)
         * @see HasKeyword(string)
         * @see RemoveKeyword(string)
         */
        public void SetKeyword(string keyword, bool enable)
        {
            ShaderKeywords[keyword] = enable;
        }

        /**
         * Removes a shader keyword from this material.
         * @param keyword name of shader keyword to remove.
         *
         * After this call, HasKeyword() will return false.
         * @see GetKeyword(string)
         * @see HasKeyword(string)
         * @see SetKeyword(string, boolean)
         */
        public void RemoveKeyword(string keyword)
        {
            ShaderKeywords.Remove(keyword);
        }

        /**
         * Gets the value of a Color material property with the given name.
         * @param name    name of the material property to retrieve.
         * @returns Color value of property.
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no color property with that name.
         * @see SetColor(string, Color)
         * @see GetColor(int)
         */
        public Color GetColor(string name)
        {
            int nameID = GetShaderIDForProperty(name);
            return GetColor(nameID);
        }

        /**
         * Gets the value of a Color material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @returns Color value of property.
         * @throws ArgumentOutOfRangeException if there is no color property with that name.
         * @see SetColor(int, Color)
         * @see GetColor(string)
         */
        public Color GetColor(int nameID)
        {
            return (Color)properties_[nameID].Item1;
        }

        /**
         * Gets the value of a float material property with the given name.
         * @param name    name of the material property to retrieve.
         * @returns float value of property.
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no float property with that name.
         * @see SetFloat(string, float)
         */
        public float GetFloat(string name)
        {
            int nameID = GetShaderIDForProperty(name);
            return GetFloat(nameID);
        }

        /**
         * Gets the value of a float material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @returns float value of property.
         * @throws ArgumentOutOfRangeException if there is no float property with that name.
         * @see SetFloat(int, float)
         * @see GetFloat(string)
         */
        public float GetFloat(int nameID)
        {
            return (float)properties_[nameID].Item1;
        }

        /**
         * Gets the value of a float array material property with the given name.
         * @param name    name of the material property to retrieve.
         * @returns float[] value of property.
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no float array property with that name.
         * @see SetFloatArray(string, float[])
         */
        public float[] GetFloatArray(string name)
        {
            int nameID = GetShaderIDForProperty(name);
            return GetFloatArray(nameID);
        }

        /**
         * Gets the value of a float array material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @returns float[] value of property.
         * @throws ArgumentOutOfRangeException if there is no float array property with that name.
         * @see SetFloatArray(int, float[])
         * @see GetFloatArray(string)
         */
        public float[] GetFloatArray(int nameID)
        {
            return (float[])properties_[nameID].Item1;
        }

        /**
         * Gets the value of an integer material property with the given name.
         * @param name    name of the material property to retrieve.
         * @returns integer value of property.
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no integer property with that name.
         * @see SetInt(string, int)
         */
        public int GetInt(string name)
        {
            int nameID = GetShaderIDForProperty(name);
            return GetInt(nameID);
        }

        /**
         * Gets the value of a integer material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @returns integer value of property.
         * @throws ArgumentOutOfRangeException if there is no integer property with that name.
         * @see SetInt(int, int)
         * @see GetInt(string)
         */
        public int GetInt(int nameID)
        {
            return (int)properties_[nameID].Item1;
        }

        /**
         * Gets the value of a Matrix4x4 material property with the given name.
         * @param name    name of the material property to retrieve.
         * @returns Matrix4x4 value of property.
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no matrix property with that name.
         * @see SetMatrix(string, Matrix4x4)
         */
        public Matrix4x4 GetMatrix(string name)
        {
            int nameID = GetShaderIDForProperty(name);
            return GetMatrix(nameID);
        }

        /**
         * Gets the value of a matrix array material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @returns Matrix4x4[] value of property.
         * @throws ArgumentOutOfRangeException if there is no matrix array property with that name.
         * @see SetMatrixArray(int, Matrix4x4[])
         * @see GetMatrixArray(string)
         */
        public Matrix4x4 GetMatrix(int nameID)
        {
            return (Matrix4x4)properties_[nameID].Item1;
        }

        /**
         * Gets the value of a Texture material property with the given name.
         * @param name    name of the material property to retrieve.
         * @returns Texture value of property.
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no Texture property with that name.
         * @see SetTexture(string, Texture)
         */
        public Texture GetTexture(string name)
        {
            int nameID = GetShaderIDForProperty(name);
            return GetTexture(nameID);
        }

        /**
         * Gets the value of a Texture material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @returns Texture value of property.
         * @throws ArgumentOutOfRangeException if there is no Texture property with that name.
         * @see SetTexture(int, CoTexturelor)
         * @see GetTexture(string)
         */
        public Texture GetTexture(int nameID)
        {
            return (Texture)properties_[nameID].Item1;
        }

        /**
         * Gets the value of a Vector4 material property with the given name.
         * @param name    name of the material property to retrieve.
         * @returns Vector4 value of property.
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no vector property with that name.
         * @see SetVector(string, Vector4)
         */
        public Vector4 GetVector(string name)
        {
            int nameID = GetShaderIDForProperty(name);
            return GetVector(nameID);
        }

        /**
         * Gets the value of a Vector4 material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @returns Vector4 value of property.
         * @throws ArgumentOutOfRangeException if there is no color property with that name.
         * @see SetVector(int, Vector4)
         * @see GetVector(string)
         */
        public Vector4 GetVector(int nameID)
        {
            return (Vector4)properties_[nameID].Item1;
        }

        /**
         * Gets the value of a vector array material property with the given name.
         * @param name    name of the material property to retrieve.
         * @returns Vector4[] value of property.
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no vector array property with that name.
         * @see SetVectorArray(string, Vector4[])
         */
        public Vector4[] GetVectorArray(string name)
        {
            int nameID = GetShaderIDForProperty(name);
            return GetVectorArray(nameID);
        }

        /**
         * Gets the value of a vector array material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @returns Vector4[] value of property.
         * @throws ArgumentOutOfRangeException if there is no vector array property with that name.
         * @see SetVecArray(int, Vector4[])
         * @see GetVecArray(string)
         */
        public Vector4[] GetVectorArray(int nameID)
        {
            return properties_[nameID].Item1 as Vector4[];
        }

        /**
         * Sets the value of a Color material property with the given name.
         * @param name    name of the material property to set.
         * @returns ShaderID of name
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no color property with that name.
         * @see GetColor(string)
         * @see SetColor(int, Color)
         */
        public int SetColor(string name, Color value)
        {
            int nameID = GetShaderIDForProperty(name);
            SetColor(nameID, value);
            return nameID;
        }

        /**
         * Sets the value of a Color material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no color property with that name.
         * @see GetColor(int)
         * @see SetColor(string, Color)
         */
        public void SetColor(int nameID, Color colorValue)
            => SetProperty(nameID, in colorValue, PropertyType.COLOR);

        /**
         * Sets the value of a float material property with the given name.
         * @param name    name of the material property to set.
         * @returns ShaderID of name
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no float property with that name.
         * @see GetFloat(string)
         * @see SetFloat(int, float)
         */
        public int SetFloat(string name, float value)
        {
            int nameID = GetShaderIDForProperty(name);
            SetFloat(nameID, value);
            return nameID;
        }

        /**
         * Sets the value of a float material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @returns float new value of property.
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no float property with that name.
         * @see GetFloat(int)
         * @see SetFloat(string, float)
         */
        public void SetFloat(int nameID, float floatValue)
            => SetProperty(nameID, floatValue, PropertyType.FLOAT);

        /**
         * Sets the value of a float array material property with the given name.
         * @param name    name of the material property to set.
         * @returns ShaderID of name
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no float array property with that name.
         * @see GetFloatArray(string)
         * @see SetFloatArray(int, float[])
         */
        public int SetFloatArray(string name, float[] values)
        {
            int nameID = GetShaderIDForProperty(name);
            SetFloatArray(nameID, values);
            return nameID;
        }

        /**
         * Sets the value of a float array material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no float array property with that name.
         * @see GetFloatArray(int)
         * @see SetFloatArray(string, float[])
         */
        public void SetFloatArray(int nameID, float[] floatArrayValues)
            => SetProperty(nameID, in floatArrayValues, PropertyType.FLOAT_ARRAY);

        /**
         * Sets the value of an integer material property with the given name.
         * @param name    name of the material property to set.
         * @returns ShaderID of name
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no integer property with that name.
         * @see GetInt(string)
         * @see SetInt(int, int)
         */
        public int SetInt(string name, int value)
        {
            int nameID = GetShaderIDForProperty(name);
            SetInt(nameID, value);
            return nameID;
        }

        /**
         * Sets the value of an integer material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no integer property with that name.
         * @see GetInt(int)
         * @see SetInt(string, int)
         */
        public void SetInt(int nameID, int intValue)
            => SetProperty(nameID, in intValue, PropertyType.INTEGER);

        /**
         * Sets the value of a Matrix4x4 material property with the given name.
         * @param name    name of the material property to set.
         * @returns ShaderID of name
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no matrix property with that name.
         * @see GetMatrix(string)
         * @see SetMatrix(int, Matrix4x4)
         */
        public int SetMatrix(string name, Matrix4x4 value)
        {
            int nameID = GetShaderIDForProperty(name);
            SetMatrix(nameID, value);
            return nameID;
        }

        /**
         * Sets the value of a Matrix4x4 material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no matrix property with that name.
         * @see GetMatrix(int)
         * @see SetMatrix(string, Matrix4x4)
         */
        public void SetMatrix(int nameID, Matrix4x4 matValue)
            => SetProperty(nameID, in matValue, PropertyType.MATRIX);

        /**
         * Sets the value of a Texture material property with the given name.
         * @param name    name of the material property to set.
         * @returns ShaderID of name
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no Texture property with that name.
         * @see GetTexture(string)
         * @see SetTexture(int, Texture)
         */
        public int SetTexture(string name, Texture value)
        {
            int nameID = GetShaderIDForProperty(name);
            SetTexture(nameID, value);
            return nameID;
        }

        /**
         * Sets the value of a Texture material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no Texture property with that name.
         * @see GetTexture(int)
         * @see SetTexture(string, Texture)
         */
        public void SetTexture(int nameID, Texture textureValue)
            => SetProperty(nameID, in textureValue, PropertyType.TEXTURE);

        /**
         * Sets the value of a Vector4 material property with the given name.
         * @param name    name of the material property to set.
         * @returns ShaderID of name
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no vector property with that name.
         * @see GetVector(string)
         * @see SetVector(int, Vector4)
         */
        public int SetVector(string name, Vector4 value)
        {
            int nameID = GetShaderIDForProperty(name);
            SetVector(nameID, value);
            return nameID;
        }

        /**
         * Sets the value of a Vector4 material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no vector property with that name.
         * @see GetVector(int)
         * @see SetVector(string, Vector4)
         */
        public void SetVector(int nameID, Vector4 vec4Value)
            => SetProperty(nameID, in vec4Value, PropertyType.VEC4);

        /**
         * Sets the value of a vector array material property with the given name.
         * @param name    name of the material property to set.
         * @returns ShaderID of name
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no vector array property with that name.
         * @see GetVectorArray(string)
         * @see SetVectorArray(int, Vector4[])
         */
        public int SetVectorArray(string name, Vector4[] values)
        {
            int nameID = GetShaderIDForProperty(name);
            SetVectorArray(nameID, values);
            return nameID;
        }

        /**
         * Sets the value of a vector array material property with the given ID.
         * @param nameID  an ID returned by Shader.PropertyToId()
         * @throws ArgumentNullException if the name is null.
         * @throws KeyNotFoundException if there is no vector array property with that name.
         * @see GetVectorArray(int)
         * @see SetVectorArray(string, Color)
         */
        public void SetVectorArray(int nameID, Vector4[] vec4Values)
            => SetProperty(nameID, in vec4Values, PropertyType.VEC_ARRAY);

        /**
         * Applies the current material state (shader, keywords, properties)
         * to a specific renderable.
         * @param renderable OvrAvatarRenderable to apply this material to.
         * Updates the Unity MaterialPropertyBlock associated with the renderable.
         */
        public void Apply(OvrAvatarRenderable renderable)
        {
            Renderer rend = renderable.rendererComponent;
            rend.GetPropertyBlock(propertyBlock_);
            if (Shader != null)
            {
                renderable.SetShader(Shader);
            }

            foreach (var entry in ShaderKeywords)
            {
                renderable.SetMaterialKeyword((string)entry.Key, (Boolean)entry.Value);
            }

            foreach (var pair in properties_)
            {
                object val = pair.Value.Item1;

                switch (pair.Value.Item2)
                {
                    case PropertyType.INTEGER:
                        propertyBlock_.SetInt(pair.Key, (int)val);
                        break;

                    case PropertyType.FLOAT:
                        propertyBlock_.SetFloat(pair.Key, (float)val);
                        break;

                    case PropertyType.COLOR:
                        propertyBlock_.SetColor(pair.Key, (Color)val);
                        break;

                    case PropertyType.TEXTURE:
                        propertyBlock_.SetTexture(pair.Key, (Texture)val);
                        break;

                    case PropertyType.VEC4:
                        propertyBlock_.SetVector(pair.Key, (Vector4)val);
                        break;

                    case PropertyType.MATRIX:
                        propertyBlock_.SetMatrix(pair.Key, (Matrix4x4)val);
                        break;

                    case PropertyType.FLOAT_ARRAY:
                        propertyBlock_.SetFloatArray(pair.Key, (float[])val);
                        break;

                    case PropertyType.VEC_ARRAY:
                        propertyBlock_.SetVectorArray(pair.Key, (Vector4[])val);
                        break;

                    case PropertyType.NONE:
                        break;
                }
            }

            rend.SetPropertyBlock(propertyBlock_);
        }

        private void SetProperty<T>(int nameID, in T val, PropertyType type)
        {
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            properties_[nameID] = new ValueTuple<object, PropertyType>(val, type);
        }

        private static int GetShaderIDForProperty(string propertyName)
        {
            System.Diagnostics.Debug.Assert(g_shaderIdLookup != null);
            if (!g_shaderIdLookup.TryGetValue(propertyName, out var shaderId))
            {
                shaderId = Shader.PropertyToID(propertyName);
                g_shaderIdLookup.Add(propertyName, shaderId);
            }

            return shaderId;
        }
    }
}
