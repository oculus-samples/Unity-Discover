// ----------------------------------------------------------------------------
// <copyright file="VoiceComponent.cs" company="Exit Games GmbH">
//   Photon Voice for Unity - Copyright (C) 2018 Exit Games GmbH
// </copyright>
// <summary>
// Base class for voice components.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------


namespace Photon.Voice.Unity
{
    using ExitGames.Client.Photon;
    using UnityEngine;

    // All Voice components should inherit this class. If this is not possible, reimplenet it directly in the component.
    [HelpURL("https://doc.photonengine.com/en-us/voice/v2")]
    public abstract class VoiceComponent : MonoBehaviour
    {
        VoiceComponentImpl impl = new VoiceComponentImpl();

        protected virtual void Awake()
        {
            impl.Awake(this);
        }

        protected Voice.ILogger Logger => impl.Logger;

        // to set logging level from code
        public VoiceLogger VoiceLogger => impl.VoiceLogger;

        public string Name
        {
            set
            {
                name = value;
                impl.Name = value;
            }
        }
    }

    // Voice.ILogger implementation logging via static UnityLogger or VoiceLogger instance if the latter is set by VoiceComponent in Awake()
    public class VoiceComponentImpl
    {
        class LoggerImpl : Voice.ILogger
        {
            VoiceLogger voiceLogger;
            Object obj;
            // name cache required because obj.name is available only on the main thread
            string objName;
            string tag = "INIT";

            public void SetVoiceLogger(VoiceLogger voiceLogger, Object obj, string tag)
            {
                this.voiceLogger = voiceLogger;
                this.obj = obj;
                this.tag = tag;
            }

            public void SetObjName(string n)
            {
                objName = n;
            }

            private void Log(DebugLevel level, string fmt, params object[] args)
            {
                if (voiceLogger != null)
                {
                    if (voiceLogger.LogLevel >= level)
                    {
                        UnityLogger.Log(level, obj, tag, objName, fmt, args);
                    }
                }
                else
                {
                    UnityLogger.Log(level, obj, tag, objName, fmt, args);
                }
            }

            public void LogError(string fmt, params object[] args)
            {
                Log(DebugLevel.ERROR, fmt, args);
            }

            public void LogWarning(string fmt, params object[] args)
            {
                Log(DebugLevel.WARNING, fmt, args);
            }

            public void LogInfo(string fmt, params object[] args)
            {
                Log(DebugLevel.INFO, fmt, args);
            }

            public void LogDebug(string fmt, params object[] args)
            {
                Log(DebugLevel.ALL, fmt, args);
            }
        }

        private VoiceLogger voiceLogger;

        private LoggerImpl logger = new LoggerImpl();

        public Voice.ILogger Logger => logger;

        public VoiceLogger VoiceLogger => voiceLogger;

        public string Name
        {
            set
            {
                logger.SetObjName(value);
            }
        }

        public void Awake(MonoBehaviour mb)
        {
            voiceLogger = VoiceLogger.FindLogger(mb.gameObject);
            if (voiceLogger == null)
            {
                // logging this message with just created voiceLogger produces confusing items relevant to mb only
                logger.LogWarning("VoiceLogger object is not found in the scene. Creating one.");
                voiceLogger = VoiceLogger.CreateRootLogger();
            }

            logger.SetVoiceLogger(voiceLogger, mb, mb.GetType().Name);
            logger.SetObjName(mb.name);
        }
    }
}