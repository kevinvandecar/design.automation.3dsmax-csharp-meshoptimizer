using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using forgeSample.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http;
using System.Net.Http.Headers;

namespace forgeSample.Controllers
{

    [ApiController]
    public class ShotgunController : ControllerBase
    {
        // Used to access the application folder (temp location for files & bundles)
        private IHostingEnvironment _env;
        // used to access the SignalR Hub
        private IHubContext<ShotgunHub> _hubContext;


        // Constructor, where env and hubContext are specified
        public ShotgunController(IHostingEnvironment env, IHubContext<ShotgunHub> hubContext)
        {
            _env = env;
            _hubContext = hubContext;
        }

        private string getAttributedShotgunTaskData(string attribute, JObject data)
        {
            data.GetValue(attribute);
            return null;
        }
        // GET: api/Shotgun
        [HttpGet]
        [Route("api/shotgun/gettask")]
        public async Task<IActionResult> GetShotgunTask()//int TaskId)
        {
            try
            {
                dynamic oauth = await OAuthController.GetShotgunAsync();

                string token = oauth.access_token; 

                string host_project = OAuthController.GetAppSetting("SHOTGUN_SITE");
                var client = new HttpClient();
                client.BaseAddress = new Uri(host_project);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // need to inprove this to get a task by name in specific project.
                var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/entity/tasks/2075/");
                var response = await client.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var contents = response.Content.ReadAsStringAsync();
                    string c = contents.Result.ToString();
                    JObject bodyJson = JObject.Parse(c);
                    string percents = bodyJson["data"]["attributes"]["sg_percentreduction"].ToString();

                    return Ok(new { task_data = percents.ToString() });
                }

            } catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }

            return BadRequest(); 
        }
    }


    /// <summary>
    /// Class uses for SignalR
    /// </summary>
    public class ShotgunHub : Microsoft.AspNetCore.SignalR.Hub
    {
        public string GetConnectionId() { return Context.ConnectionId; }
    }

}
