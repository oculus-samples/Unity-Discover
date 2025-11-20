// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Meta.Utilities;
using Meta.XR.Samples;
using Oculus.Platform;
using UnityEngine;

namespace Discover
{
    /// <summary>
    /// Handles the Abuse reporting flow. This needs to be enabled at the startup of the app.
    /// https://developer.oculus.com/resources/reporting-plugin/?intern_source=devblog&intern_content=user-reporting-requirements-developer-tools-updates
    /// </summary>
    [MetaCodeSample("Discover")]
    public class AbuseReportingHandler : Singleton<AbuseReportingHandler>
    {
        [SerializeField] private ReportRequestResponse m_reportHandlingType = ReportRequestResponse.Unhandled;

        public Action<Message<string>> OnReportButtonClicked;

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            AbuseReport.SetReportButtonPressedNotificationCallback(OnReportButtonPressed);
        }

        private void OnReportButtonPressed(Message<string> message)
        {
            if (!message.IsError)
            {
                _ = AbuseReport.ReportRequestHandled(m_reportHandlingType);
                // This action can start your own reporting flow
                OnReportButtonClicked?.Invoke(message);
            }
        }
    }
}
