// --------------------------------------------------------------------------------
// <copyright file="TestTone.cs" company="Exit Games GmbH">
//   Part of: Photon Voice Utilities for Unity - Copyright (C) 2018 Exit Games GmbH
// </copyright>
// <summary>
// This MonoBehaviour is a sample demo of how to use AudioSource.Factory
// by implementing IAudioReader.
// </summary>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------

using System;
using UnityEngine;

namespace Photon.Voice.Unity.UtilityScripts
{
    [RequireComponent(typeof(Recorder))]
    public class TestTone : MonoBehaviour
    {
        private void Start()
        {
            Recorder rec = this.gameObject.GetComponent<Recorder>();
            rec.SourceType = Recorder.InputSourceType.Factory;
            rec.InputFactory = () =>
            {
                return new AudioUtil.ToneAudioReader<float>(null, 440, 24000, 1);
            };
        }
    }
}

