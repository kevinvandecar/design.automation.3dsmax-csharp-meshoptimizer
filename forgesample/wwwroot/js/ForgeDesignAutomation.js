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

    $('#refineActivityBundle').click(clearAccount);
    $('#startWorkitem').click(startWorkitem);
    $('#viewit').click(viewit);
    $('#wireframe').click(doWireframe);

    startConnection();    
});

function prepareLists() {
    list('activity', '/api/forge/designautomation/definedactivities');
    //list('engines', '/api/forge/designautomation/engines');
    //list('localBundles', '/api/appbundles');
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
        url: 'api/forge/designautomation/clearaccount',
        method: 'DELETE',
        success: function () {
            prepareLists();
            writeLog('Account cleared, all appbundles & activities deleted');
        }
    });
}


function createAppBundleActivity(createWorkitemCB) {
    startConnection(function () {
        writeLog("Defining appbundle and activity for Autodesk.3dsMax+2024"); // + $('#engines').val());
        //$("#defineActivityModal").modal('toggle');
        createAppBundle(function () {
            createActivity(createWorkitemCB);
        });
    });
}

function createAppBundle(cb) {
    jQuery.ajax({
        url: 'api/forge/designautomation/initializeappbundle',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({
            zipFileName: $('#localBundles').val(),
            engine: $('#engines').val()
        }),
        success: function (res) {
            writeLog('AppBundle: ' + res.appBundle); //+ ', v' + res.version);
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

     createAppBundleActivity(function () {
        var inputFileField = document.getElementById('inputFile');
        // We can use a "default.max" scene. So if nothing is input, we grab it locally (on local server)
        //if (inputFileField.files.length === 0) { alert('Please select an input file'); return; }
        //if ($('#activity').val() === null) { alert('Please select an activity'); return };

        var file = inputFileField.files[0];
        if (file != null)
            writeLog('Starting work item with input file: ' + file.name);
        else
            writeLog('Starting work item with input file: default scene');

        var checkboxKeepNormals = document.getElementById('KeepNormals');
        var checkboxKeepUV = document.getElementById('KeepUV');
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
                KeepUV: checkboxKeepUV.checked,
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


////////////////////////////////////////////////////////////////////////////////////////////////////
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
        var filename = "models/" + /*"constant"*/ connectionId + "/" + num2 + "/outputFile-" + num2 + ".svf";
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

var toggleWireframe = false;

// setup a new model in the viewer
function createViewer(modelName, viewer_id) {

    var options = {
        'document': modelName,
        'env': 'Local',
        'keepCurrentModels': 'false'
    };
    var viewerElement = document.getElementById(viewer_id);

    //var viewer = new Autodesk.Viewing.GuiViewer3D(viewerElement, {}); 
    var viewer = new Autodesk.Viewing.Viewer3D(viewerElement, {});

    Autodesk.Viewing.Initializer(options, function () {
        viewer.initialize();       
        viewer.addEventListener(Autodesk.Viewing.OBJECT_TREE_CREATED_EVENT, function (event) {
            setTimeout(function () {
                initViewerExtensions(viewer);
                updateViewerWireframe(viewer);
            }, 500);
        });
        viewer.loadModel(options.document);
    });

    return viewer;
}

function initViewerExtensions(viewer) {
    // put viewcube into viewer, but no other GUI controls.
    viewer.loadExtension("Autodesk.ViewCubeUi");
    // load wirframe extension
    viewer.loadExtension('Autodesk.Viewing.Wireframes');
}

function setupViewerBackground(viewer) {
    console.log("setupViewerBackground");
    viewer.setEnvMapBackground(false);
    // a shade matching 3ds Max logo color...
    viewer.setBackgroundColor(14, 167, 167, 255, 255, 255);
}

// helper function to allow the extension to resolve before using it.
const sleep = (milliseconds) => {
    return new Promise(resolve => setTimeout(resolve, milliseconds))
}


function loadViewerModelReduced() {
    var indexViewFile2 = document.getElementById("solutions").selectedIndex;
    var viewFile2 = files[indexViewFile2];

    if (viewer2 != null) {
        var ext = viewer2.getExtension("Autodesk.ViewCubeUi");
        ext.deactivate();
        ext = null;
        ext = viewer2.getExtension("Autodesk.Viewing.Wireframes");
        ext.deactivate();
        ext = null;
        viewer2.finish();
        viewer2 = null;
        viewer2 = createViewer(viewFile2, 'forgeViewer2');
    }
}

function doWireframe() {
    toggleWireframe = !toggleWireframe;
    updateViewerWireframe(viewer1);
    updateViewerWireframe(viewer2);
}

function updateViewerWireframe(viewer) {
    var ext = viewer.getExtension("Autodesk.Viewing.Wireframes");
    if (toggleWireframe == true) {
        viewer.setLightPreset(7);
        if (ext != null) {
            ext.activate();
            mat = new THREE.MeshBasicMaterial();
            mat.color.setRGB(1, 0, 0);  // red
            mat.flatShading = true;
            ext.setLinesMaterial(mat);
        }
    } else {
        viewer.setLightPreset(0);
        if (ext != null)
            ext.deactivate();
    }
    setupViewerBackground(viewer);
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

    // should always have a 100% result
    var viewFile1 = "models/" + /*"constant"*/ connectionId + "/100_0/outputFile-100_0.svf";
    var viewFile2 = files[0]; // start with lowest reduction...
    console.log(viewFile1);
    console.log(viewFile2);
    
    viewer1 = createViewer(viewFile1, 'forgeViewer1');
    viewer2 = createViewer(viewFile2, 'forgeViewer2');
}

