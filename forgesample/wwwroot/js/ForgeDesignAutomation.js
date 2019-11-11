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

$(document).ready(function () {
    prepareLists();

    $('#clearAccount').click(clearAccount);
    $('#defineActivityShow').click(defineActivityModal);
    $('#createAppBundleActivity').click(createAppBundleActivity);
    $('#getShotgunData').click(getShotgunData);
    $('#startWorkitem').click(startWorkitem);
    $('#viewit').click(viewit);
    $('#wireframe').click(doWireframe);

    startConnection();
    startShotgunConnection();
});

function prepareLists() {
    list('activity', '/api/forge/designautomation/activities');
    list('engines', '/api/forge/designautomation/engines');
    list('localBundles', '/api/appbundles');
}

function list(control, endpoint) {
    $('#' + control).find('option').remove().end();
    jQuery.ajax({
        url: endpoint,
        success: function (list) {
            if (list.length === 0)
                $('#' + control).append($('<option>', { disabled: true, text: 'Nothing found' }));
            else
                list.forEach(function (item) { $('#' + control).append($('<option>', { value: item, text: item })); })
        }
    });
}

function clearAccount() {
    if (!confirm('Clear existing activities & appbundles before start. ' +
        'This is useful if you believe there are wrong settings on your account.' +
        '\n\nYou cannot undo this operation. Proceed?')) return;

    jQuery.ajax({
        url: 'api/forge/designautomation/account',
        method: 'DELETE',
        success: function () {
            prepareLists();
            writeLog('Account cleared, all appbundles & activities deleted');
        }
    });
}

function defineActivityModal() {
    $("#defineActivityModal").modal();
}

function createAppBundleActivity() {
    startConnection(function () {
        writeLog("Defining appbundle and activity for " + $('#engines').val());
        $("#defineActivityModal").modal('toggle');
        createAppBundle(function () {
            createActivity(function () {
                prepareLists();
            })
        });
    });
}

function createAppBundle(cb) {
    jQuery.ajax({
        url: 'api/forge/designautomation/appbundles',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({
            zipFileName: $('#localBundles').val(),
            engine: $('#engines').val()
        }),
        success: function (res) {
            writeLog('AppBundle: ' + res.appBundle + ', v' + res.version);
            if (cb) cb();
        }
    });
}

function createActivity(cb) {
    jQuery.ajax({
        url: 'api/forge/designautomation/activities',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({
            zipFileName: $('#localBundles').val(),
            engine: $('#engines').val()
        }),
        success: function (res) {
            writeLog('Activity: ' + res.activity);
            if (cb) cb();
        }
    });
}

function startWorkitem() {
    clearPercentsDropdown();
    resetViewers();

    var inputFileField = document.getElementById('inputFile');
    // We can use a "default.max" scene. So if nothing is input, we grab it locally (on local server)
    //if (inputFileField.files.length === 0) { alert('Please select an input file'); return; }
    if ($('#activity').val() === null) { alert('Please select an activity'); return };

    var file = inputFileField.files[0];
    if (file != null)
        writeLog('Starting work item with input file: ' + file.name);
    else 
        writeLog('Starting work item with input file: default scene');

    var checkboxKeepNormals = document.getElementById('KeepNormals');
    var checkboxCollapseStack = document.getElementById('CollapseStack');
    var checkboxCreateSVFPreview = document.getElementById('CreateSVFPreview');
    var unique_jobid = Date.now();
    startConnection(function () {
        var formData = new FormData();
        formData.append('inputFile', file);
        formData.append('unique_jobid', unique_jobid);
        formData.append('data', JSON.stringify({
            percent: $('#percent').val(),
            KeepNormals: checkboxKeepNormals.checked,
            CollapseStack: checkboxCollapseStack.checked,
            CreateSVFPreview: checkboxCreateSVFPreview.checked,
            activityName: $('#activity').val(),
            browerConnectionId: connectionId
        }));
        writeLog('Uploading input file...');
        $.ajax({
            url: 'api/forge/designautomation/workitems',
            data: formData,
            processData: false,
            contentType: false,
            type: 'POST',
            success: function (res) {
                writeLog('Workitem started: ' + res.workItemId);
            }
        });
    });
}

function writeLog(text) {
  $('#outputlog').append('<div style="border-top: 1px dashed #C0C0C0">' + text + '</div>');
  var elem = document.getElementById('outputlog');
  elem.scrollTop = elem.scrollHeight;
}

var connection;
var connectionId;

function startConnection(onReady) {
    if (connection && connection.connectionState) { if (onReady) onReady(); return; }
    connection = new signalR.HubConnectionBuilder().withUrl("/api/signalr/designautomation").build();
    connection.start()
        .then(function () {
            connection.invoke('getConnectionId')
                .then(function (id) {
                    connectionId = id; // we'll need this...
                    if (onReady) onReady();
                });
        });

    connection.on("downloadResult", function (url) {
        writeLog('<a href="' + url +'">Download result file here</a>');
    });

    connection.on("onComplete", function (message) {
        writeLog(message);
    });
}

///// Shotgun connection

var sg_connection;
var sg_connectionId;

function startShotgunConnection(onReady) {
    if (sg_connection && sg_connection.connectionState) {
        if (onReady)
            onReady();
        return;
    }
    sg_connection = new signalR.HubConnectionBuilder().withUrl("/api/signalr/shotgun").build();
    sg_connection.start()
        .then(function () {
            sg_connection.invoke('getConnectionId')
                .then(function (id) {
                    sg_connectionId = id; // we'll need this...
                    if (onReady)
                        onReady();
                });
        });

    connection.on("onComplete", function (message) {
        writeLog(message);
    });
}

function getShotgunData()
{
    writeLog('starting shotgun request...');
    //GetShotgunTokenAsync();
    startShotgunConnection(function () {
        writeLog('making shotgun request...');
        $.ajax({
            url: 'api/shotgun/gettask',
            processData: false,
            contentType: false,
            type: 'GET',
            success: function (res) {
                writeLog('shotgun task: ' + res.task_data);
                var elem = document.getElementById('percent');
                elem.value = res.task_data;
            },
            error: function (res) {
                writeLog('shotgun fail: ' + res.status);
            }
        });
    });

//
    writeLog('finished shotgun request...');
}

/// viewer UI setup

function clearPercentsDropdown() {
    files = null;
    var select = document.getElementById('solutions');
    var length = select.options.length;
    for (i = length-1; i >= 0; i--) {
        select.remove(i);
    }
    select.disabled = true;
    //writeLog('options length ' + select.options.length);
}

function getPercentFilenames() {
    var select = document.getElementById('solutions');

    var percents = document.getElementById('percent').value;
    var files = new Array(0);
    // "models/100_0/outputFile-100_0.svf";
    var percentsValues = percents.split(/\s*,\s*/).forEach(function (myString) {
        var num = (parseFloat(myString) * 100).toFixed(1);
        var num2 = num.toString().replace(".", "_");
        var filename = "models/" + connectionId + "/" + num2 + "/outputFile-" + num2 + ".svf";
        files.push(filename);
        //console.log(myString);
    });;

    return files;

}

var files;
function populatePercentsDropdown() {
    clearPercentsDropdown();

    files = getPercentFilenames();
    var select = document.getElementById('solutions');
    var percents = document.getElementById('percent').value;
    var percentsValues = files.forEach(function (myString) {
        var option = document.createElement("option");
        option.text = myString.replace(/^.*[\\\/]/, '');
        //option.text = myString;
        select.add(option);
        //console.log(myString);
    });;
    select.disabled = false;
}

// global viewers handling
var viewer1 = null;
var viewer2 = null;

// setup a new model in the viewer
function createViewer(modelName, viewer_id) {

    var options = {
        'document': modelName,
        'env': 'Local',
    };
    var viewerElement = document.getElementById(viewer_id);

    //var viewer = new Autodesk.Viewing.Private.GuiViewer3D(viewerElement, {}); 
    var viewer = new Autodesk.Viewing.Viewer3D(viewerElement, {});

    Autodesk.Viewing.Initializer(options, function () {
        viewer.initialize();
        // Autodesk.Viewing.OBJECT_TREE_CREATED_EVENT, setupMyModel);
        
        viewer.addEventListener(Autodesk.Viewing.OBJECT_TREE_CREATED_EVENT, function (event) {
            setTimeout(function () { initViewerOptions(viewer);/*orientViewer(viewer);*/ }, 100);
        });
        viewer.load(options.document);
    });

    return viewer;
}

//var config = { 'startOnInitialize': true };
//viewer2.setUp(config);
//viewer2.setLightPreset(4);
//viewer2.setQualityLevel(false, false);
//viewer2.setGhosting(true);
//viewer2.setGroundShadow(true);
//viewer2.setGroundReflection(true);
//viewer2.setEnvMapBackground(false);
//viewer2.setProgressiveRendering(true);
//viewer2.setBackgroundColor(255, 0, 0, 255, 255, 255);
//viewer2.impl.sceneUpdated(true);
//viewer2.impl.unloadCurrentModel();

function initViewerOptions(viewer) {
    setupViewerOptions(viewer);
    var promise1 = viewer.loadExtension('Autodesk.Viewing.Wireframes');
    promise1.then((successMessage) => {
        console.log("loadExtension: " + successMessage);
    });
    //viewer.impl.sceneUpdated(true);
}

function setupViewerOptions(viewer) {
    viewer.setEnvMapBackground(false);
    // a shade matching 3ds Max logo color...
    viewer.setBackgroundColor(14, 167, 167, 255, 255, 255);
    viewer.createViewCube();
    viewer.displayViewCube(true);
}

// helper function to allow the extension to resolve before using it.
const sleep = (milliseconds) => {
    return new Promise(resolve => setTimeout(resolve, milliseconds))
}

function loadViewerModelReduced() {
    var indexViewFile2 = document.getElementById("solutions").selectedIndex;
    var viewFile2 = files[indexViewFile2];

    if (viewer2 != null) {
        var stateFilter = {
            seedURN: false,
            objectSet: false,
            viewport: true,
            renderOptions: false
        };
        var state = viewer2.getState(stateFilter);

        viewer2.tearDown();
        viewer2.load(viewFile2);
        sleep(600).then(() => {
            var b = viewer2.restoreState(state, stateFilter, true);
            updateViewerWireframe(viewer2);
        })
        
        console.log('loaded: ' + viewFile2);
    }
}

var toggleWireframe = false;
function doWireframe() {
    toggleWireframe = !toggleWireframe;
    updateViewerWireframe(viewer1);
    updateViewerWireframe(viewer2);
}

function updateViewerWireframe(viewer) {
    var ext = viewer.getExtension("Autodesk.Viewing.Wireframes");
    if (toggleWireframe == true) {
        ext.activate();      
    } else {
        ext.deactivate();
    }
   setupViewerOptions(viewer);
}

function resetViewers() {
    if (viewer1 != null) {
        var ext1 = viewer1.getExtension("Autodesk.Viewing.Wireframes");
        ext1.deactivate();
        viewer1.uninitialize();
        viewer1 = null;
    }
    if (viewer2 != null) {
        var ext2 = viewer2.getExtension("Autodesk.Viewing.Wireframes");
        ext2.deactivate();
        viewer2.uninitialize();
        viewer2 = null;
    }
    toggleWireframe = false;
}

function viewit() {
    populatePercentsDropdown();
    //return;
    //var viewFile = "https://kevinvandecar.github.io/assets/forge_logo/forge.SVF";
    //var viewFile2 = "https://kevinvandecar.github.io/assets/x-wing_max/svf/xwing.SVF";
    //var viewFile2 = "https://vandecar.s3.amazonaws.com/assets/svf/horse/50/50.svf";

    // should always have a 100% result
    var viewFile1 = "models/" + connectionId + "/100_0/outputFile-100_0.svf";
    //var viewFile2 = document.getElementById("solutions").value;
    var viewFile2 = files[0]; // start with lowest reduction...
    
    viewer1 = createViewer(viewFile1, 'forgeViewer1');
    viewer2 = createViewer(viewFile2, 'forgeViewer2');
}

