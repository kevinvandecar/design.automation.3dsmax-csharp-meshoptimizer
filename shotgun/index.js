"use strict";
// junk tester file... Not used for project.
var login="kevin.vandecar@autodesk.com";
var password=""; // have to supply this for the test 
var host="https://devtechme.shotgunstudio.com"
let access_token = null;
let refresh_token = null;
let access_expires_at = null;
let refresh_expires_at = null;

function get_access_token(grant_type){
    var body = 'grant_type=' + grant_type

    if(grant_type == 'password'){
        body += '&username=' + login
             + '&password=' + password;
    }else if(grant_type == 'client_credentials'){
        body += '&client_id=' + pm.environment.get("script_name")
             + '&client_secret=' + pm.environment.get("script_key");
    }else if(grant_type == 'refresh'){
        body += '&refresh_token=' + refresh_token;
    }

    auth_request(body);
}


function auth_request(body) {
     pm.sendRequest({
        url: $host + '/api/v1/auth/access_token',
        method: 'POST',
        header: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'Accept': 'application/json'
        },
        body: {
            mode: 'raw',
            raw: body
        }
    }, function (err, res) {
        if (err) { console.log('ERROR!'); console.log(err); }
        access_token = res.json().access_token;
        refresh_token = res.json().refresh_token;
        access_expires_at = Date.now() + res.json().expires_in*1000;
        refresh_expires_at = Date.now() + 24*60*60*1000;
    });
}

function get_token()
{
	//console.log('Grant Type: ' + pm.environment.get("grant_type"));
	if(access_expires_at === null) {
		console.log('No Token!');
		get_access_token(grant_type);
	} else if(access_expires_at <= Date.now()) {
		console.log('Access Expired');
		if (refresh_expires_at <= Date.now()) {
			console.log('Refresh Expired');
			get_access_token(grant_type);
		} else {
			console.log('Using Refresh token');
			get_access_token('password');
		}
	}
}

function doit()
{
	//{{host}}/api/v1/entity/:entity/:record_id/
	var settings = {
	  "async": true,
	  "crossDomain": true,
	  "url": "https://devtechme.shotgunstudio.com/api/v1/entity/tasks/2075/",
	  "method": "GET",
	  "headers": {
		"Authorization": "Bearer eyJhbGciOiJIUzI1NiJ9.eyJqdGkiOiJmNGViY2M5MC1hMzI5LTExZTktYWQ3MS0wMjQyYWMxMTAwMDQiLCJpc3MiOiJkZXZ0ZWNobWUuc2hvdGd1bnN0dWRpby5jb20iLCJhdWQiOiJkZXZ0ZWNobWUuc2hvdGd1bnN0dWRpby5jb20iLCJleHAiOjE1NjI3NzQyMjAsImlhdCI6MTU2Mjc3MzYyMCwidXNlciI6eyJ0eXBlIjoiSHVtYW5Vc2VyIiwiaWQiOjM4fSwic3Vkb19hc19sb2dpbiI6bnVsbCwiYXV0aF90eXBlIjoibG9naW4ifQ.LVkZ2n5YOzn3SNuO5vQ0NweePGD3IMMBgqspYIqek7c",
		"User-Agent": "PostmanRuntime/7.15.2",
		"Accept": "*/*",
		"Cache-Control": "no-cache",
		"Postman-Token": "5a2a3818-583b-4d9c-a5fe-a3b562776e2c,3df3a7c8-962d-4338-9b47-b17b1b37c3bf",
		"Host": "devtechme.shotgunstudio.com",
		"Accept-Encoding": "gzip, deflate",
		"Connection": "keep-alive",
		"cache-control": "no-cache"
	  }
	}

	$.ajax(settings).done(function (response) {
	  console.log(response);
	});
	
	console.log('gettin down to it!');
}




