// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Threading.Tasks;
using Meta.XR.Samples;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;

namespace Discover
{
    /// <summary>
    /// Utility functions to interact with the Oculus Platform SDK.
    /// Make sure to add "OVR_PLATFORM_ASYNC_MESSAGES" to Script Defined Symbols in Player Settings,
    /// for the async Gen functions.
    /// </summary>
    [MetaCodeSample("Discover")]
    public static class OculusPlatformUtils
    {
        private const string INITIALIZED_ERROR_MSG = "Failed to initialize Oculus Platform SDK";
        private const string ENTITLEMENT_ERROR_MSG = "You are not entitled to use this app";
        private const string LOGGED_IN_USER_ERROR_MSG = "Failed to load logged in user";

        private static User s_loggedInUser = null;
        private static ulong? s_userDeviceGeneratedUid;

        public static async Task<bool> InitializeAndValidate(Action<string> onError = null)
        {
            Debug.Log("[OculusPlatformUtils] InitializeAndValidate Oculus Platform SDK");
            if (!await Init(onError))
            {
                return false;
            }

            if (!await CheckEntitlement(onError))
            {
                return false;
            }

            _ = await LoadLoggedInUser(onError);

            return true;
        }

        public static async Task<bool> Init(Action<string> onError = null)
        {
            if (Core.IsInitialized())
            {
                return true;
            }

            Debug.Log("Initializing Oculus Platform SDK");
            var coreInitResult = await Core.AsyncInitialize().Gen();
            if (coreInitResult.IsError)
            {
                LogError(INITIALIZED_ERROR_MSG, coreInitResult.GetError());
                onError?.Invoke(INITIALIZED_ERROR_MSG);
                return false;
            }

            Debug.Log("Oculus Platform SDK initialized successfully");

            return true;
        }

        public static async Task<bool> CheckEntitlement(Action<string> onError = null)
        {
            var isUserEntitled = await Entitlements.IsUserEntitledToApplication().Gen();
            if (isUserEntitled.IsError)
            {
                LogError(ENTITLEMENT_ERROR_MSG, isUserEntitled.GetError());
                onError?.Invoke(ENTITLEMENT_ERROR_MSG);
                return false;
            }

            Debug.Log("Oculus Platform SDK entitlement check success");

            return true;
        }

        public static async Task<User> GetLoggedInUser()
        {
            if (s_loggedInUser == null)
            {
                _ = await LoadLoggedInUser();
            }

            return s_loggedInUser;
        }
        
        /// <summary>
        /// Get the generated unique id of the user device. This will change on every session.
        /// </summary>
        /// <returns>generated unique id as a ulong</returns>
        public static ulong GetUserDeviceGeneratedUid()
        {
            s_userDeviceGeneratedUid ??= (ulong)Guid.NewGuid().GetHashCode();

            return s_userDeviceGeneratedUid.Value;
        }

        private static async Task<bool> LoadLoggedInUser(Action<string> onError = null)
        {
            // call Init in case it wasn't done yet, we don't await as the request will be queued
#pragma warning disable CS4014
            _ = Init();
#pragma warning restore CS4014

            var loggedInUserRequest = Users.GetLoggedInUser();
            if (loggedInUserRequest == null)
                return false;

            var loggedInUserResult = await loggedInUserRequest.Gen();
            if (loggedInUserResult.IsError)
            {
                LogError(LOGGED_IN_USER_ERROR_MSG, loggedInUserResult.GetError());
                onError?.Invoke(LOGGED_IN_USER_ERROR_MSG);
                return false;
            }

            if (loggedInUserResult.Type == Message.MessageType.User_GetLoggedInUser)
            {
                s_loggedInUser = loggedInUserResult.GetUser();
                Debug.Log(
                    $"OculusPlatform - Logged in as {s_loggedInUser.DisplayName} ({s_loggedInUser.OculusID}, {s_loggedInUser.ID})"
                );
                return true;
            }

            LogError(LOGGED_IN_USER_ERROR_MSG, loggedInUserResult.GetError());
            onError?.Invoke(LOGGED_IN_USER_ERROR_MSG);
            return false;
        }

        private static void LogError(string msg, Error error)
        {
            Debug.LogError($"[OculusPlatformUtils] {msg}: {error.Message}({error.Code})");
        }
    }
}