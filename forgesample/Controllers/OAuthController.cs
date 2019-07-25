/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Mvc;
using Autodesk.Forge;
using System.Net.Http;
using System.Net.Http.Headers;

namespace forgeSample.Controllers
{
    [ApiController]
    public class OAuthController : ControllerBase
    {
        // As both internal & public tokens are used for all visitors
        // we don't need to request a new token on every request, so let's
        // cache them using static variables. Note we still need to refresh
        // them after the expires_in time (in seconds)
        private static dynamic InternalToken { get; set; }

        /// <summary>
        /// Get access token with internal (write) scope
        /// </summary>
        [HttpGet]
        [Route("api/forge/oauth/token")]
        public static async Task<dynamic> GetInternalAsync()
        {
            if (InternalToken == null || InternalToken.ExpiresAt < DateTime.UtcNow)
            {
                InternalToken = await Get2LeggedTokenAsync(new Scope[] { Scope.BucketCreate, Scope.BucketRead, Scope.BucketDelete, Scope.DataRead, Scope.DataWrite, Scope.DataCreate, Scope.CodeAll, Scope.ViewablesRead });
                InternalToken.ExpiresAt = DateTime.UtcNow.AddSeconds(InternalToken.expires_in);
            }

            return InternalToken;
        }

        /// <summary>
        /// Get the access token from Autodesk
        /// </summary>
        private static async Task<dynamic> Get2LeggedTokenAsync(Scope[] scopes)
        {
            TwoLeggedApi oauth = new TwoLeggedApi();
            string grantType = "client_credentials";
            dynamic bearer = await oauth.AuthenticateAsync(
              GetAppSetting("FORGE_CLIENT_ID"),
              GetAppSetting("FORGE_CLIENT_SECRET"),
              grantType,
              scopes);
            return bearer;
        }

        /// <summary>
        /// Reads appsettings from web.config
        /// </summary>
        public static string GetAppSetting(string settingKey)
        {
            return Environment.GetEnvironmentVariable(settingKey);
        }

        //////// shotgun

        private static dynamic ShotgunToken { get; set; }

        /// <summary>
        /// Get Shotgun access token
        /// </summary>
        public static async Task<dynamic> GetShotgunAsync()
        {
            if (ShotgunToken == null || ShotgunToken.ExpiresAt < DateTime.UtcNow)
            {
                ShotgunToken = await GetShotgunTokenAsync();
                //ShotgunToken.ExpiresAt = DateTime.UtcNow.AddSeconds(ShotgunToken.expires_in);
            }

            return ShotgunToken;
        }


        [HttpGet]
        [Route("api/v1/auth/access_token")]
        public static async Task<dynamic> AuthenticateShotgunAsync(string clientLogin, string clientPass, string grantType, string host_project)
        {
           
            var client = new HttpClient();
            client.BaseAddress = new Uri(host_project);
            var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/access_token");

            var keyValues = new List<KeyValuePair<string, string>>();
            keyValues.Add(new KeyValuePair<string, string>("grant_type", "password"));
            keyValues.Add(new KeyValuePair<string, string>("username", clientLogin));
            keyValues.Add(new KeyValuePair<string, string>("password", clientPass));

            request.Content = new FormUrlEncodedContent(keyValues);
            var response = await client.SendAsync(request);
            var contents = await response.Content.ReadAsStringAsync();
            return JObject.Parse(contents); 
            //return 0;
        }

        /// <summary>
        /// Get the access token from Shotgun
        /// </summary>
        private static async Task<dynamic> GetShotgunTokenAsync()
        {
            //TwoLeggedApi oauth = new TwoLeggedApi();
            string grantType = "password";
            string login = GetAppSetting("SHOTGUN_LOGIN");
            string pass = GetAppSetting("SHOTGUN_PASSWORD");
            string host_project = GetAppSetting("SHOTGUN_SITE");

            dynamic bearer = await AuthenticateShotgunAsync(
              login,
              pass,
              grantType,
              host_project);

            return bearer;
        }



    }
}