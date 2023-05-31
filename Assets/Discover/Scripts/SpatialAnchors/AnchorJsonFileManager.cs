// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Discover.SpatialAnchors
{
    public class AnchorJsonFileManager<TData> : ISpatialAnchorFileManager<TData>
        where TData : SpatialAnchorSaveData
    {
        private string m_path;

        public AnchorJsonFileManager(string fileName, string path = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Application.persistentDataPath;
            }

            m_path = Path.Combine(path, fileName);
        }

        public void WriteDataToFile(List<TData> dataList)
        {
            Debug.Log($"[JSON] Writing to json {dataList.Count} items");
            var jsonData = JsonConvert.SerializeObject(
                dataList,
                new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }
                );
            try
            {
                var writer = new StreamWriter(m_path);
                writer.Write(jsonData);
                writer.Flush();
                writer.Close();
            }
            catch (Exception e)
            {
                Debug.LogError($"[JSON] {e.Message}");
            }
        }

        public List<TData> ReadDataFromFile()
        {
            var data = ReadFile();
            if (string.IsNullOrWhiteSpace(data))
            {
                return new List<TData>();
            }

            var jsonData = JsonConvert.DeserializeObject<List<TData>>(data);

            Debug.Log($"[JSON] Reading from json {jsonData.Count} items");
            return jsonData;
        }

        private string ReadFile()
        {
            var data = "";
            try
            {
                var reader = new StreamReader(m_path);
                data = reader.ReadToEnd();
                reader.Close();
                Debug.Log($"[JSON] json data is found: {data}");
            }
            catch (Exception e)
            {
                Debug.Log($"[JSON] no json data is found : {e.Message}");
            }

            return data;
        }
    }
}