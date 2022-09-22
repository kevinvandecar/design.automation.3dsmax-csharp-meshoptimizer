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

using Autodesk.Forge;
using Autodesk.Forge.DesignAutomation;
using Autodesk.Forge.DesignAutomation.Model;
using Autodesk.Forge.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Activity = Autodesk.Forge.DesignAutomation.Model.Activity;
using Alias = Autodesk.Forge.DesignAutomation.Model.Alias;
using AppBundle = Autodesk.Forge.DesignAutomation.Model.AppBundle;
using Parameter = Autodesk.Forge.DesignAutomation.Model.Parameter;
using WorkItem = Autodesk.Forge.DesignAutomation.Model.WorkItem;
using WorkItemStatus = Autodesk.Forge.DesignAutomation.Model.WorkItemStatus;


namespace forgeSample.Controllers
{
    [ApiController]
    public class DesignAutomationController : ControllerBase
    {
        // store the solutions needed.
        private static JArray numbers;
        private static bool SVFpreview;
        private static string localSolutionsFilenameBase = "solutions.zip";
        private static string localSolutionsFolderRoot = "wwwroot/models/";
        private static string currentJobFolder;
        private static string unique_jobid;
        private static string zipFileName = "ProOptimizerAutomation"; 
        private static string engineName = "Autodesk.3dsMax+2023";
        private static string appBundleName = zipFileName + "AppBundle";
        private static string activityName = zipFileName + "Activity";
        private static string maxcommandLine = "$(engine.path)\\3dsmaxbatch.exe -sceneFile \"$(args[inputFile].path)\" \"$(settings[script].path)\"";
        private static string maxscript = "da = dotNetClass(\"Autodesk.Forge.Sample.DesignAutomation.Max.RuntimeExecute\")\nda.ProOptimizeMesh()\n";



        // Used to access the application folder (temp location for files & bundles)
        private IWebHostEnvironment _env;
        // used to access the SignalR Hub
        private IHubContext<DesignAutomationHub> _hubContext;
        // Local folder for bundles
        public string LocalBundlesFolder { get { return Path.Combine(_env.WebRootPath, "bundles"); } }
        /// Prefix for AppBundles and Activities
        public static string NickName { get { return OAuthController.GetAppSetting("FORGE_CLIENT_ID"); } }
        /// Alias for the app (e.g. DEV, STG, PROD). This value may come from an environment variable
        public static string Alias { get { return "dev"; } }
        // Design Automation v3 API
        DesignAutomationClient _designAutomation;


        // Constructor, where env and hubContext are specified
        public DesignAutomationController(IWebHostEnvironment env, IHubContext<DesignAutomationHub> hubContext, DesignAutomationClient api)
        {
            _designAutomation = api;
            _env = env;
            _hubContext = hubContext;
        }


         /// <summary>
        /// PRESENTATION
        /// Define a new appbundle
        /// </summary>
        [HttpPost]
        [Route("api/forge/designautomation/initializeappbundle")]
        public async Task<IActionResult> InitializeAppBundle([FromBody]JObject appBundleSpecs)
        {

            // check if ZIP with bundle is here
            string packageZipPath = Path.Combine(LocalBundlesFolder, zipFileName + ".zip");
            if (!System.IO.File.Exists(packageZipPath)) throw new Exception("Appbundle not found at " + packageZipPath);

            // get defined app bundles
            Page<string> appBundles = await _designAutomation.GetAppBundlesAsync();

            // check if app bundle is already defined
            dynamic newAppVersion; 
            string qualifiedAppBundleId = string.Format("{0}.{1}+{2}", NickName, appBundleName, Alias);
            if (!appBundles.Data.Contains(qualifiedAppBundleId))
            {
                // create an appbundle (version 1)
                AppBundle appBundleSpec = new AppBundle()
                {
                    Package = appBundleName,
                    Engine = engineName,
                    Id = appBundleName,
                    Description = string.Format("Description for {0}", appBundleName),

                };
                newAppVersion = await _designAutomation.CreateAppBundleAsync(appBundleSpec);
                if (newAppVersion == null) throw new Exception("Cannot create new app");

                // create alias pointing to v1
                Alias aliasSpec = new Alias() { Id = Alias, Version = 1 };
                Alias newAlias = await _designAutomation.CreateAppBundleAliasAsync(appBundleName, aliasSpec);

                // upload the zip with .bundle
                RestClient uploadClient = new RestClient(newAppVersion.UploadParameters.EndpointURL);
                RestRequest request = new RestRequest(string.Empty, Method.POST);
                request.AlwaysMultipartFormData = true;
                foreach (KeyValuePair<string, string> x in newAppVersion.UploadParameters.FormData) request.AddParameter(x.Key, x.Value);
                request.AddFile("file", packageZipPath);
                request.AddHeader("Cache-Control", "no-cache");
                await uploadClient.ExecuteAsync(request);

            }

            return Ok(new { AppBundle = qualifiedAppBundleId });
        }

        /// <summary>
        /// Get all Activities defined for this account
        /// </summary>
        [HttpGet]
        [Route("api/forge/designautomation/definedactivities")]
        public async Task<List<string>> GetDefinedActivities()
        {
            // filter list of 
            Page<string> activities = await _designAutomation.GetActivitiesAsync();
            List<string> definedActivities = new List<string>();
            foreach (string activity in activities.Data)
                if (activity.StartsWith(NickName) && activity.IndexOf("$LATEST") == -1)
                    definedActivities.Add(activity.Replace(NickName + ".", String.Empty));

            return definedActivities;
        }

        /// <summary>
        /// PRESENTATION
        /// Define a new activity
        /// </summary>
        [HttpPost]
        [Route("api/forge/designautomation/activities")]
        public async Task<IActionResult> CreateActivity([FromBody]JObject activitySpecs)
        {

            // 
            Page<string> activities = await _designAutomation.GetActivitiesAsync();
            string qualifiedActivityId = string.Format("{0}.{1}+{2}", NickName, activityName, Alias);
            if (!activities.Data.Contains(qualifiedActivityId))
            {
                // define the activity
                //dynamic engineAttributes = EngineAttributes(engineName);
                string localCommandLine = string.Format(maxcommandLine, appBundleName);
                Activity activitySpec = new Activity()
                {
                    Id = activityName,
                    Appbundles = new List<string>() { string.Format("{0}.{1}+{2}", NickName, appBundleName, Alias) },
                    CommandLine = new List<string>() { localCommandLine },
                    Engine = engineName,
                    Parameters = new Dictionary<string, Parameter>()
                    {
                        { "inputFile", new Parameter() { Description = "input file", LocalName = "$(inputFile)", Ondemand = false, Required = true, Verb = Verb.Get, Zip = false } },
                        { "inputJson", new Parameter() { Description = "input json", LocalName = "params.json", Ondemand = false, Required = false, Verb = Verb.Get, Zip = false } },
                        { "outputFile", new Parameter() { Description = "output file", LocalName = "output.zip", Ondemand = false, Required = true, Verb = Verb.Put, Zip = false } }
                    },
                    Settings = new Dictionary<string, ISetting>()
                    {
                        { "script", new StringSetting(){ Value = maxscript } }
                    }
                };
                Activity newActivity = await _designAutomation.CreateActivityAsync(activitySpec);

                // specify the alias for this Activity
                Alias aliasSpec = new Alias() { Id = Alias, Version = 1 };
                Alias newAlias = await _designAutomation.CreateActivityAliasAsync(activityName, aliasSpec);

                return Ok(new { Activity = qualifiedActivityId });
            }

            // as this activity points to a AppBundle "dev" alias (which points to the last version of the bundle),
            // there is no need to update it (for this sample), but this may be extended for different contexts
            return Ok(new { Activity = "Activity already defined" });
        }

 
        /// <summary>
        /// Input for StartWorkitem
        /// </summary>
        public class StartWorkitemInput
        {
            public IFormFile inputFile { get; set; }
            public string data { get; set; }
        }

        /// <summary>
        /// PRESENTATION
        /// Start a new workitem
        /// </summary>
        [HttpPost]
        [Route("api/forge/designautomation/workitems")]
        public async Task<IActionResult> StartWorkitem([FromForm]StartWorkitemInput input)
        {
            // basic input validation
            JObject workItemData = JObject.Parse(input.data);            
            string percentParam = workItemData["percent"].Value<string>();
            string keepNormalsParam = workItemData["KeepNormals"].Value<string>();
            string keepUVParam = workItemData["KeepUV"].Value<string>();
            string collapseStackParam = workItemData["CollapseStack"].Value<string>();
            string createSVFPreviewParam = workItemData["CreateSVFPreview"].Value<string>();
            string localActivityName = string.Format("{0}.{1}+{2}", NickName, activityName, Alias);
            string browerConnectionId = workItemData["browerConnectionId"].Value<string>();
            unique_jobid = /*"constant"*/ browerConnectionId;

            // save the file on the server
            string fileSavePath = null;
            string input_fname = null;
            bool bUsingDefault = false;
            if (input.inputFile != null)
            {
                fileSavePath = Path.Combine(_env.ContentRootPath, Path.GetFileName(input.inputFile.FileName));
                using (var stream = new FileStream(fileSavePath, FileMode.Create)) await input.inputFile.CopyToAsync(stream);
            }
            else
            {
                bUsingDefault = true;
                input_fname = "default.max";
                fileSavePath = Path.Combine(_env.WebRootPath, Path.GetFileName(input_fname));
            }
            
            // OAuth token
            dynamic oauth = await OAuthController.GetInternalAsync();

            // upload file to OSS Bucket
            // 1. ensure bucket existis
            string bucketKey = NickName.ToLower() + "_designautomation";
            BucketsApi buckets = new BucketsApi();
            buckets.Configuration.AccessToken = oauth.access_token;
            try
            {
                PostBucketsPayload bucketPayload = new PostBucketsPayload(bucketKey, null, PostBucketsPayload.PolicyKeyEnum.Transient);
                await buckets.CreateBucketAsync(bucketPayload, "US");
            }
            catch { }; // in case bucket already exists
          
            // 2. upload inputFile
            string inputFileNameOSS = string.Format("{0}_input_{1}", DateTime.Now.ToString("yyyyMMddhhmmss"), Path.GetFileName(input_fname)); //
            ObjectsApi objects = new ObjectsApi();
            objects.Configuration.AccessToken = oauth.access_token;
            using (StreamReader streamReader = new StreamReader(fileSavePath))
                await objects.UploadObjectAsync(bucketKey, inputFileNameOSS, (int)streamReader.BaseStream.Length, streamReader.BaseStream, "application/octet-stream");
            if (!bUsingDefault)
                System.IO.File.Delete(fileSavePath);// delete server copy if it's user supplied

            // prepare workitem arguments
            // 1. input file
            XrefTreeArgument inputFileArgument = new XrefTreeArgument()
            {
                Url = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, inputFileNameOSS),
                Headers = new Dictionary<string, string>()
                 {
                     { "Authorization", "Bearer " + oauth.access_token }
                 }
            };
            // 2. input json
            dynamic inputJson = new JObject();
            numbers = new JArray(percentParam.Split(',').Select(r => Convert.ToString(r)).ToList());
            // Note these dyanmic variables are the names matching in the JSON and used in the plugin to receive data
            inputJson.VertexPercents = numbers;
            inputJson.KeepNormals = keepNormalsParam;
            inputJson.KeepUV = keepUVParam;
            inputJson.CollapseStack = collapseStackParam;
            inputJson.CreateSVFPreview = createSVFPreviewParam;
            SVFpreview = createSVFPreviewParam.Contains("True", StringComparison.CurrentCultureIgnoreCase) ? true : false;
            XrefTreeArgument inputJsonArgument = new XrefTreeArgument()
            {
                Url = "data:application/json, " + ((JObject)inputJson).ToString(Formatting.None).Replace("\"", "'")
            };
            // 3. output file
            string outputFileNameOSS = "output.zip";//string.Format("{0}_output_{1}", DateTime.Now.ToString("yyyyMMddhhmmss"), Path.GetFileName(input.inputFile.FileName)); // avoid overriding
            XrefTreeArgument outputFileArgument = new XrefTreeArgument()
            {
                Url = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, outputFileNameOSS),
                Verb = Verb.Put,
                Headers = new Dictionary<string, string>()
                   {
                       {"Authorization", "Bearer " + oauth.access_token }
                   }
            };

            // prepare & submit workitem
            string callbackUrl = string.Format("{0}/api/forge/callback/designautomation?id={1}&outputFileName={2}", OAuthController.GetAppSetting("FORGE_WEBHOOK_URL"), browerConnectionId, outputFileNameOSS);
            WorkItem workItemSpec = new WorkItem()
            {
                ActivityId = localActivityName,
                Arguments = new Dictionary<string, IArgument>()
                {
                    { "inputFile", inputFileArgument },
                    { "inputJson",  inputJsonArgument },
                    { "outputFile", outputFileArgument },
                    { "onComplete", new XrefTreeArgument { Verb = Verb.Post, Url = callbackUrl } }
                }
            };

            ObjectResult theresult;
            try
            {
                WorkItemStatus workItemStatus = await _designAutomation.CreateWorkItemAsync(workItemSpec);
                theresult = Ok(new { WorkItemId = workItemStatus.Id });
            } 
            catch (Exception e)
            {
                theresult = new BadRequestObjectResult(e);
            }

            return theresult;
        }

        /// <summary>
        /// PRESENTATION
        /// Callback from Design Automation Workitem (onProgress or onComplete)
        /// </summary>
        [HttpPost]
        [Route("/api/forge/callback/designautomation")]
        public async Task<IActionResult> OnCallback(string id, string outputFileName, [FromBody]dynamic body)
        {
            try
            {
                // your webhook should return immediately! we can use Hangfire to schedule a job
                JObject bodyJson = JObject.Parse((string)body.ToString());
                await _hubContext.Clients.Client(id).SendAsync("onComplete", bodyJson.ToString());

                var client = new RestClient(bodyJson["reportUrl"].Value<string>());
                var request = new RestRequest(string.Empty);

                byte[] bs = client.DownloadData(request);
                string report = System.Text.Encoding.Default.GetString(bs);
                await _hubContext.Clients.Client(id).SendAsync("onComplete", report);

                ObjectsApi objectsApi = new ObjectsApi();
                dynamic signedUrl = await objectsApi.CreateSignedResourceAsyncWithHttpInfo(NickName.ToLower() + "_designautomation", outputFileName, new PostBucketsSigned(10), "read");
                await _hubContext.Clients.Client(id).SendAsync("downloadResult", (string)(signedUrl.Data.signedUrl));
                //
                //
                if (SVFpreview)
                {
                    currentJobFolder = localSolutionsFolderRoot + unique_jobid + '/';
                    var local = new WebClient();
                    local.DownloadFile(signedUrl.Data.signedUrl, unique_jobid + localSolutionsFilenameBase);
                    System.IO.Compression.ZipFile.ExtractToDirectory(unique_jobid + localSolutionsFilenameBase, currentJobFolder);
                    numbers.AddFirst("1"); // make sure we also include the orginal SVF 100% (1.0) representation.
                    foreach (float vertexPercent in numbers)
                    {
                        var nvertexPercent = vertexPercent * 100;
                        string stringVertexPercent = nvertexPercent.ToString("F1");
                        stringVertexPercent = stringVertexPercent.Replace('.', '_');
                        string output = "outputFile-" + stringVertexPercent + ".zip";

                        System.IO.Compression.ZipFile.ExtractToDirectory(currentJobFolder + output, currentJobFolder + stringVertexPercent); 
                    }
                }

            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
            }

            // ALWAYS return ok (200)
            return Ok();
        }

        /// <summary>
        /// Delete the temporary files used during preview of job.
        /// </summary>
        public static void CleanUpServerFiles()
        {
            /*if (System.IO.File.Exists(localSolutionsFilenameBase))
                System.IO.File.Delete(localSolutionsFilenameBase);*/
            if (System.IO.Directory.Exists(localSolutionsFolderRoot))
                // warning, if changing locations, make sure this is safe in your environment. 
                // during debugging, you may accidentally delete files you did not intend to delete
                System.IO.Directory.Delete(localSolutionsFolderRoot, true);
        }

        /// <summary>
        /// Clear the accounts (for debugging purposes)
        /// </summary>
        [HttpDelete]
        [Route("api/forge/designautomation/clearaccount")]
        public async Task<IActionResult> ClearAccount()
        {
            // clear account
            await _designAutomation.DeleteForgeAppAsync("me");
            return Ok();
        }

    }

    /// <summary>
    /// Class uses for SignalR
    /// </summary>
    public class DesignAutomationHub : Microsoft.AspNetCore.SignalR.Hub
    {
        public string GetConnectionId() { return Context.ConnectionId; }

        // Overridden OnDisconnectedAsync that allows clean up of temp files used during preview of job.
        // This is triggered upon close of browser, or navigation away from page.
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            // remember, that certain events are not triggered in debugger, like this one.
            forgeSample.Controllers.DesignAutomationController.CleanUpServerFiles();
            await base.OnDisconnectedAsync(exception);
        }
    }

}