"use strict";

// global viewer

let viewer = null;

// setup a new model in the viewer
function viewit(modelName, lightPreset) {

     var options = {
        'document' : modelName, 
         'env':'Local', 
        };
    var viewerElement = document.getElementById('viewer3D');

    viewer = new Autodesk.Viewing.Private.GuiViewer3D (viewerElement, {}); //Autodesk.Viewing.Viewer3D(viewerElement, {});

    Autodesk.Viewing.Initializer(options,function() {
        viewer.initialize();
		viewer.addEventListener (Autodesk.Viewing.GEOMETRY_LOADED_EVENT, function (event) {
                    setTimeout (function () { orient_view(); }, 100) ;
                }) ;		
        viewer.load(options.document);
        viewer.setLightPreset(lightPreset);
    });
 
} 

// tell the viewer to fit the geometry to the view extents.
function orient_view () {
      if (viewer != null) {
		 var front = new THREE.Vector3(55, -75, 88);
		 viewer.navigation.setPosition(front);
		 viewer.fitToView (true) ;
		 viewer.createViewCube();
		 viewer.displayViewCube(true);
		 console.log('hello');
      } else{
		console.log("viewer is null");
	  }
}



function doit()
{
	var promise1 = viewer.loadExtension('Autodesk.Viewing.Wireframes');
 	promise1.then((successMessage) => {
		console.log("Yay! " + successMessage);
	});
 	//sleep(1000);
	var ext = viewer.getExtension("Autodesk.Viewing.Wireframes"); //Autodesk.Viewing.Wireframes");
	ext.activate();
	console.log('gettin down to it!');
}

//viewit('./70/70.svf', 7);
viewit('https://drive.google.com/open?id=1UZL-sXSx1fXcUiISvA4gDyMX5eP3UI01/50.svf', 7);




